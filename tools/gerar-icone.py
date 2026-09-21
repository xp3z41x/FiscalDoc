"""
Gera src/FiscalDoc.App/FiscalDoc.ico.

O executavel precisa de dois icones embutidos, na ordem em que o registro do
Windows os referencia (ver installer/FiscalDoc.iss):

  indice 0 = icone do APLICATIVO      -> Applications\\FiscalDoc.exe\\DefaultIcon
  indice 1 = icone do DOCUMENTO       -> FiscalDoc.Document.1\\DefaultIcon

Icone embutido no .exe e mais confiavel que um .ico solto nos varios contextos
do shell, entao os dois vao para dentro do binario.

O ICO e montado a mao com payloads PNG (suportado desde o Vista), o que evita
depender de biblioteca de imagem: o arquivo e so um cabecalho de 6 bytes, uma
entrada de 16 bytes por tamanho, e os PNGs concatenados.

Uso:  python tools/gerar-icone.py
"""

import pathlib
import struct
import subprocess
import sys
import tempfile

RAIZ = pathlib.Path(__file__).resolve().parent.parent
TAMANHOS = [16, 24, 32, 48, 64, 128, 256]

# Desenha os dois glifos com System.Drawing, via PowerShell: a folha com a
# dobra no canto (documento) e a mesma folha com uma tarja (aplicativo).
PS = r'''
Add-Type -AssemblyName System.Drawing
function Desenhar([int]$s, [bool]$app) {
  $bmp = New-Object System.Drawing.Bitmap($s, $s)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.SmoothingMode = 'AntiAlias'
  $g.Clear([System.Drawing.Color]::Transparent)

  $m = [math]::Max(1, [int]($s * 0.10))
  $w = $s - 2*$m
  $h = $s - 2*$m
  $dobra = [int]($w * 0.34)

  # Corpo da folha, com o canto superior direito dobrado.
  $pts = New-Object 'System.Drawing.PointF[]' 5
  $pts[0] = New-Object System.Drawing.PointF($m, $m)
  $pts[1] = New-Object System.Drawing.PointF(($m + $w - $dobra), $m)
  $pts[2] = New-Object System.Drawing.PointF(($m + $w), ($m + $dobra))
  $pts[3] = New-Object System.Drawing.PointF(($m + $w), ($m + $h))
  $pts[4] = New-Object System.Drawing.PointF($m, ($m + $h))

  $corpo = if ($app) { [System.Drawing.Color]::FromArgb(255, 32, 70, 128) } else { [System.Drawing.Color]::White }
  $traco = if ($app) { [System.Drawing.Color]::FromArgb(255, 20, 46, 88) }  else { [System.Drawing.Color]::FromArgb(255, 70, 70, 74) }

  $b = New-Object System.Drawing.SolidBrush($corpo)
  $g.FillPolygon($b, $pts)
  $b.Dispose()

  $p = New-Object System.Drawing.Pen($traco, [float]([math]::Max(1.0, $s * 0.045)))
  $g.DrawPolygon($p, $pts)

  # A dobra.
  $g.DrawLine($p, ($m + $w - $dobra), $m, ($m + $w - $dobra), ($m + $dobra))
  $g.DrawLine($p, ($m + $w - $dobra), ($m + $dobra), ($m + $w), ($m + $dobra))
  $p.Dispose()

  # Linhas de texto: sugerem um documento preenchido sem virar enfeite.
  if ($s -ge 24) {
    $lb = New-Object System.Drawing.SolidBrush( $(if ($app) { [System.Drawing.Color]::FromArgb(255,235,240,250) } else { [System.Drawing.Color]::FromArgb(255,90,90,96) }) )
    $lx = $m + $w * 0.14
    $lw = $w * 0.56
    $lh = [math]::Max(1.0, $s * 0.055)
    for ($i = 0; $i -lt 4; $i++) {
      $ly = $m + $h * (0.50 + $i * 0.115)
      $largura = if ($i -eq 3) { $lw * 0.6 } else { $lw }
      $g.FillRectangle($lb, [float]$lx, [float]$ly, [float]$largura, [float]$lh)
    }
    $lb.Dispose()
  }

  $g.Dispose()
  return $bmp
}
$saida = $args[0]
foreach ($s in @(SIZES)) {
  foreach ($kind in @('app','doc')) {
    $bmp = Desenhar $s ($kind -eq 'app')
    $bmp.Save((Join-Path $saida "$kind-$s.png"), [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
  }
}
'''


def montar_ico(pngs: list[tuple[int, bytes]], destino: pathlib.Path) -> None:
    """Cabecalho ICO + uma entrada por tamanho + os PNGs concatenados."""
    n = len(pngs)
    cabecalho = struct.pack("<HHH", 0, 1, n)  # reservado, tipo 1 = icone, contagem

    offset = 6 + (16 * n)
    entradas = b""
    corpo = b""

    for tamanho, dados in pngs:
        # 256 e gravado como 0 no campo de um byte.
        largura = 0 if tamanho >= 256 else tamanho
        entradas += struct.pack(
            "<BBBBHHII",
            largura, largura,  # largura, altura
            0,                 # cores na paleta
            0,                 # reservado
            1,                 # planos
            32,                # bits por pixel
            len(dados),
            offset,
        )
        corpo += dados
        offset += len(dados)

    destino.write_bytes(cabecalho + entradas + corpo)


def main() -> None:
    with tempfile.TemporaryDirectory() as tmp:
        tmpdir = pathlib.Path(tmp)
        script = tmpdir / "desenhar.ps1"
        script.write_text(PS.replace("SIZES", ",".join(str(s) for s in TAMANHOS)), encoding="utf-8")

        r = subprocess.run(
            ["pwsh", "-NoProfile", "-NonInteractive", "-File", str(script), str(tmpdir)],
            capture_output=True, text=True)

        if r.returncode != 0:
            print(r.stdout)
            print(r.stderr, file=sys.stderr)
            raise SystemExit("falha ao desenhar os glifos")

        # Dois arquivos separados: o do aplicativo vira ApplicationIcon e
        # entra no .exe; o do documento e copiado para a pasta de instalacao.
        for nome, prefixo in (("FiscalDoc.ico", "app"), ("FiscalDocDocumento.ico", "doc")):
            pngs = [(s, (tmpdir / f"{prefixo}-{s}.png").read_bytes()) for s in TAMANHOS]
            destino = RAIZ / "src" / "FiscalDoc.App" / nome
            montar_ico(pngs, destino)
            print(f"  {nome}  ({destino.stat().st_size:,} bytes, {len(TAMANHOS)} tamanhos)")


if __name__ == "__main__":
    main()
