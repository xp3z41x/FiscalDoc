"""
Gera os icones do aplicativo a partir de ICON.png, na raiz do projeto.

    ICON.png (512x512 RGBA)
        -> src/FiscalDoc.App/FiscalDoc.ico            icone do APLICATIVO
        -> src/FiscalDoc.App/FiscalDocDocumento.ico   icone do DOCUMENTO
        -> installer/imagens/assistente-NN.bmp        assistente do INSTALADOR

Os tres saem da mesma arte. O ICON.png ja E um documento, entao serve as duas
associacoes: o aplicativo na barra de tarefas e no menu Iniciar, e o .xml que
ele abre no Explorer. Se algum dia forem arte diferente, e so trocar a origem
de `DOCUMENTO` aqui embaixo - o resto do script nao muda.

O aplicativo embute o seu como recurso Win32 (`ApplicationIcon` no .csproj) E
como recurso gerenciado (`EmbeddedResource`), porque sao duas caras
diferentes: o recurso Win32 e a cara do ARQUIVO no Explorer, e o gerenciado e
a cara da JANELA, que o WinForms so poe se alguem atribuir `Form.Icon`. O
icone do documento vai como arquivo ao lado do executavel, porque o MSBuild so
embute UM grupo de icone. O instalador aponta o registro para os dois:

    Applications\\FiscalDoc.exe\\DefaultIcon  ->  {app}\\FiscalDoc.exe,0
    FiscalDoc.Document.1\\DefaultIcon         ->  {app}\\FiscalDocDocumento.ico

e usa o proprio .ico no `SetupIconFile` e os .bmp no `WizardSmallImageFile`.

Uso:  python tools/gerar-icone.py

=====================================================================
 POR QUE O SCRIPT E ASSIM
=====================================================================

**Sem biblioteca de imagem.** PNG de 8 bits sem entrelacamento e zlib mais
umas trinta linhas de desfiltragem, e ICO e um cabecalho de 6 bytes com uma
entrada de 16 por tamanho. Pedir Pillow para isso deixaria o script sem rodar
numa maquina recem-clonada, que e justamente quando alguem vai querer rodar.

**A reducao acontece em alfa PREMULTIPLICADO.** Reduzir RGBA direto mistura a
cor de pixels totalmente transparentes na borda do desenho - e como o PNG
exportado costuma trazer preto por baixo do transparente, o resultado sao
auras escuras em volta de cada traco, visiveis justamente nos tamanhos
pequenos. Premultiplicar antes e dividir depois elimina isso.

**Media de area, e nao bicubico.** A arte e feita de tracos finos. Filtro com
lobulo negativo (bicubico, Lanczos) produz halo e serrilha em traco fino; a
media de area pondera cada pixel de origem pela fracao que ele ocupa no pixel
de destino, que e exatamente o que se quer numa reducao de 512 para 16.

**Tamanhos <= 48 saem como DIB, >= 64 como PNG.** O ICO aceita PNG desde o
Vista, mas varios caminhos do shell e do Win32 classico (ExtractIconEx,
listas de imagem antigas, caixas de dialogo) ainda esperam DIB nos tamanhos
pequenos - e quando nao encontram, mostram icone generico ou nada. PNG nos
grandes evita um 256x256 de 256 KB descompactado dentro do binario.
"""

import pathlib
import struct
import zlib

RAIZ = pathlib.Path(__file__).resolve().parent.parent
ORIGEM = RAIZ / "ICON.png"
DESTINO = RAIZ / "src" / "FiscalDoc.App"
DESTINO_ASSISTENTE = RAIZ / "installer" / "imagens"

# O conjunto que o Windows pede de fato:
#   16   lista, detalhes, barra de titulo
#   20   os mesmos 16 a 125 %
#   24   menu Iniciar, alguns modos de lista
#   32   icones medios, area de trabalho a 100 %
#   40   os mesmos 32 a 125 %
#   48   icones grandes a 100 %
#   64   os mesmos 48 a 125 %, e blocos pequenos
#   96   icones extragrandes em escalas intermediarias
#  128   extragrande
#  256   "jumbo" e a previa de Propriedades (Vista+)
TAMANHOS = [16, 20, 24, 32, 40, 48, 64, 96, 128, 256]

# Abaixo deste limite a entrada sai em DIB; deste tamanho para cima, em PNG.
LIMITE_PNG = 64

# A imagem que o Inno Setup desenha no alto de TODA pagina do assistente. Ele
# nao aceita PNG nem ICO ali - so .bmp - e escolhe, da lista que receber, a
# que melhor serve a escala do monitor: 55 px e a medida a 100 %, 83 a 150 %,
# 110 a 200 %. Dar os tres evita que ele estique um unico arquivo.
TAMANHOS_ASSISTENTE = [55, 83, 110]


# ---------------------------------------------------------------------
#  PNG: leitura e escrita
# ---------------------------------------------------------------------


def ler_png(caminho: pathlib.Path) -> tuple[int, int, bytearray]:
    """Le um PNG RGBA de 8 bits, sem entrelacamento, e devolve os pixels."""
    dados = caminho.read_bytes()

    if dados[:8] != b"\x89PNG\r\n\x1a\x0a":
        raise SystemExit(f"{caminho.name} nao e um PNG")

    largura = altura = 0
    comprimido = b""
    i = 8

    while i < len(dados):
        tamanho = struct.unpack(">I", dados[i : i + 4])[0]
        tipo = dados[i + 4 : i + 8]
        corpo = dados[i + 8 : i + 8 + tamanho]

        if tipo == b"IHDR":
            largura, altura, profundidade, cor, _, _, entrelacado = struct.unpack(
                ">IIBBBBB", corpo
            )
            if (profundidade, cor, entrelacado) != (8, 6, 0):
                raise SystemExit(
                    f"{caminho.name}: esperado RGBA de 8 bits sem entrelacamento "
                    f"(profundidade={profundidade}, tipo={cor}, entrelacado={entrelacado})"
                )
        elif tipo == b"IDAT":
            comprimido += corpo
        elif tipo == b"IEND":
            break

        i += 12 + tamanho

    return largura, altura, _desfiltrar(zlib.decompress(comprimido), largura, altura)


def _desfiltrar(cru: bytes, largura: int, altura: int) -> bytearray:
    """Desfaz os filtros por linha do PNG (ISO 15948, secao 9)."""
    canais = 4
    passo = largura * canais
    saida = bytearray(passo * altura)
    anterior = bytearray(passo)
    p = 0

    for y in range(altura):
        filtro = cru[p]
        p += 1
        linha = bytearray(cru[p : p + passo])
        p += passo

        if filtro == 1:  # Sub
            for x in range(canais, passo):
                linha[x] = (linha[x] + linha[x - canais]) & 0xFF
        elif filtro == 2:  # Up
            for x in range(passo):
                linha[x] = (linha[x] + anterior[x]) & 0xFF
        elif filtro == 3:  # Average
            for x in range(passo):
                esq = linha[x - canais] if x >= canais else 0
                linha[x] = (linha[x] + ((esq + anterior[x]) >> 1)) & 0xFF
        elif filtro == 4:  # Paeth
            for x in range(passo):
                esq = linha[x - canais] if x >= canais else 0
                cima = anterior[x]
                diag = anterior[x - canais] if x >= canais else 0
                linha[x] = (linha[x] + _paeth(esq, cima, diag)) & 0xFF
        elif filtro != 0:
            raise SystemExit(f"filtro PNG desconhecido: {filtro}")

        saida[y * passo : (y + 1) * passo] = linha
        anterior = linha

    return saida


def _paeth(a: int, b: int, c: int) -> int:
    p = a + b - c
    pa, pb, pc = abs(p - a), abs(p - b), abs(p - c)
    if pa <= pb and pa <= pc:
        return a
    return b if pb <= pc else c


def escrever_png(pixels: bytearray, lado: int) -> bytes:
    """Monta um PNG RGBA de 8 bits, todas as linhas com filtro 0."""

    def pedaco(tipo: bytes, corpo: bytes) -> bytes:
        return (
            struct.pack(">I", len(corpo))
            + tipo
            + corpo
            + struct.pack(">I", zlib.crc32(tipo + corpo) & 0xFFFFFFFF)
        )

    passo = lado * 4
    cru = bytearray()

    for y in range(lado):
        cru.append(0)
        cru += pixels[y * passo : (y + 1) * passo]

    return (
        b"\x89PNG\r\n\x1a\x0a"
        + pedaco(b"IHDR", struct.pack(">IIBBBBB", lado, lado, 8, 6, 0, 0, 0))
        + pedaco(b"IDAT", zlib.compress(bytes(cru), 9))
        + pedaco(b"IEND", b"")
    )


# ---------------------------------------------------------------------
#  Reducao
# ---------------------------------------------------------------------


def reduzir(pixels: bytearray, origem: int, destino: int) -> bytearray:
    """
    Media de area em alfa premultiplicado.

    Cada pixel de destino soma os pixels de origem que o cobrem, cada um
    pesado pela fracao de area que ocupa - inclusive as fracoes das bordas,
    que e o que faz 512 -> 20 sair tao limpo quanto 512 -> 16.
    """
    escala = origem / destino
    saida = bytearray(destino * destino * 4)

    # As bordas de cada coluna de destino, uma vez so.
    faixas = []
    for d in range(destino):
        inicio, fim = d * escala, (d + 1) * escala
        primeiro, ultimo = int(inicio), min(int(fim - 1e-9), origem - 1)
        faixas.append((inicio, fim, primeiro, ultimo))

    for dy in range(destino):
        y0, y1, py0, py1 = faixas[dy]

        for dx in range(destino):
            x0, x1, px0, px1 = faixas[dx]

            soma_r = soma_g = soma_b = soma_a = peso_total = 0.0

            for sy in range(py0, py1 + 1):
                cobertura_y = min(y1, sy + 1) - max(y0, sy)
                if cobertura_y <= 0:
                    continue

                base = (sy * origem + px0) * 4

                for sx in range(px0, px1 + 1):
                    cobertura_x = min(x1, sx + 1) - max(x0, sx)
                    if cobertura_x <= 0:
                        base += 4
                        continue

                    peso = cobertura_x * cobertura_y
                    a = pixels[base + 3]
                    # Premultiplica: cor de pixel transparente nao pode
                    # contaminar a borda.
                    pa = peso * a
                    soma_r += pixels[base] * pa
                    soma_g += pixels[base + 1] * pa
                    soma_b += pixels[base + 2] * pa
                    soma_a += pa
                    peso_total += peso
                    base += 4

            i = (dy * destino + dx) * 4

            if soma_a > 0.0:
                saida[i] = min(255, int(soma_r / soma_a + 0.5))
                saida[i + 1] = min(255, int(soma_g / soma_a + 0.5))
                saida[i + 2] = min(255, int(soma_b / soma_a + 0.5))
                saida[i + 3] = min(255, int(soma_a / peso_total + 0.5))

    return saida


# ---------------------------------------------------------------------
#  ICO
# ---------------------------------------------------------------------


def adensar(pixels: bytearray, lado: int) -> bytearray:
    """
    Recupera a densidade do traco fino nos tamanhos pequenos.

    <para>O traco da arte tem 7,8 % do lado. A 16 px isso da 1,25 px: a media
    de area - que esta certa - espalha esse traco por dois pixels, cada um com
    pouco mais da metade do alfa, e o icone aparece lavado, um fantasma do
    desenho. Quem faz icone a mao resolve redesenhando o 16 com traco cheio de
    1 px; aqui o equivalente automatico e devolver o alfa perdido.</para>

    <para>O ganho e aplicado DEPOIS da reducao, que e onde mora a cobertura
    parcial - aplicado antes nao muda nada, porque a arte de origem ja e quase
    toda opaca ou toda transparente. E decai continuamente ate 32 px, onde o
    traco ja tem 2,5 px e nao precisa de ajuda: um corte seco deixaria um
    degrau visivel entre o 24 e o 32 na mesma lista do Explorer.</para>
    """
    ganho = 1.45 - (lado - 16) * (0.45 / 16.0)

    if ganho <= 1.0:
        return pixels

    for i in range(3, len(pixels), 4):
        a = pixels[i]
        if 0 < a < 255:
            pixels[i] = min(255, int(a * ganho + 0.5))

    return pixels


def dib(pixels: bytearray, lado: int) -> bytes:
    """
    Entrada no formato classico: BITMAPINFOHEADER, BGRA de baixo para cima,
    mais a mascara AND.

    <para>A altura no cabecalho e o DOBRO do lado - a norma conta a imagem
    somada a mascara. Windows moderno ignora a mascara num icone de 32 bits,
    mas ela tem de estar la, com o tamanho certo, ou o icone nao carrega.</para>
    """
    cabecalho = struct.pack(
        "<IiiHHIIiiII",
        40,          # tamanho do cabecalho
        lado,        # largura
        lado * 2,    # altura: imagem + mascara
        1,           # planos
        32,          # bits por pixel
        0,           # sem compressao
        lado * lado * 4,
        0, 0, 0, 0,  # resolucao e paleta
    )

    cores = bytearray()
    for y in range(lado - 1, -1, -1):
        base = y * lado * 4
        for x in range(lado):
            i = base + x * 4
            cores += bytes(
                (pixels[i + 2], pixels[i + 1], pixels[i], pixels[i + 3])
            )

    # Mascara AND: 1 bit por pixel, linha alinhada em 4 bytes, bit ligado =
    # transparente.
    bytes_por_linha = ((lado + 31) // 32) * 4
    mascara = bytearray()

    for y in range(lado - 1, -1, -1):
        linha = bytearray(bytes_por_linha)
        for x in range(lado):
            if pixels[(y * lado + x) * 4 + 3] < 128:
                linha[x >> 3] |= 0x80 >> (x & 7)
        mascara += linha

    return cabecalho + bytes(cores) + bytes(mascara)


def montar_ico(entradas: list[tuple[int, bytes]], destino: pathlib.Path) -> None:
    """Cabecalho ICO + uma entrada de diretorio por tamanho + os corpos."""
    n = len(entradas)
    cabecalho = struct.pack("<HHH", 0, 1, n)  # reservado, tipo 1 = icone, contagem

    deslocamento = 6 + (16 * n)
    diretorio = b""
    corpo = b""

    for lado, dados in entradas:
        # 256 e gravado como 0 no campo de um byte.
        medida = 0 if lado >= 256 else lado
        diretorio += struct.pack(
            "<BBBBHHII",
            medida, medida,
            0,   # cores na paleta
            0,   # reservado
            1,   # planos
            32,  # bits por pixel
            len(dados),
            deslocamento,
        )
        corpo += dados
        deslocamento += len(dados)

    destino.write_bytes(cabecalho + diretorio + corpo)


# ---------------------------------------------------------------------
#  BMP (assistente do instalador)
# ---------------------------------------------------------------------


def bmp(pixels: bytearray, lado: int) -> bytes:
    """
    BMP de 32 bits, de baixo para cima, com alfa PREMULTIPLICADO.

    <para>O Inno Setup so aceita .bmp nas imagens do assistente. O fundo da
    arte e transparente, e o unico jeito de manter isso num BMP e o canal
    alfa - com `WizardImageAlphaFormat=premultiplied` no script do
    instalador. Sem o par, o alfa e ignorado e cada pixel transparente
    aparece com o RGB que se esconde sob ele, que neste PNG e preto: um
    quadrado preto em volta do desenho.</para>

    <para>Cabecalho classico de 40 bytes e BI_RGB, e nao BITFIELDS nem V5: o
    carregador do Inno (VCL) so reconhece o canal alfa na forma simples, e um
    cabecalho estendido faz a imagem cair para 24 bits sem aviso.</para>
    """
    corpo = bytearray()

    for y in range(lado - 1, -1, -1):
        base = y * lado * 4
        for x in range(lado):
            i = base + x * 4
            a = pixels[i + 3]
            corpo += bytes(
                (
                    pixels[i + 2] * a // 255,
                    pixels[i + 1] * a // 255,
                    pixels[i] * a // 255,
                    a,
                )
            )

    info = struct.pack(
        "<IiiHHIIiiII",
        40,          # tamanho do cabecalho
        lado,        # largura
        lado,        # altura positiva: linhas de baixo para cima
        1,           # planos
        32,          # bits por pixel
        0,           # BI_RGB, sem compressao
        len(corpo),
        2835, 2835,  # ~72 dpi nos dois eixos
        0, 0,        # sem paleta
    )

    arquivo = struct.pack(
        "<2sIHHI", b"BM", 14 + len(info) + len(corpo), 0, 0, 14 + len(info)
    )

    return arquivo + info + corpo


# ---------------------------------------------------------------------


def main() -> None:
    if not ORIGEM.is_file():
        raise SystemExit(f"arte nao encontrada: {ORIGEM}")

    largura, altura, pixels = ler_png(ORIGEM)

    if largura != altura:
        raise SystemExit(f"{ORIGEM.name} precisa ser quadrado ({largura}x{altura})")

    if largura < max(TAMANHOS):
        raise SystemExit(
            f"{ORIGEM.name} tem {largura} px; ampliar para {max(TAMANHOS)} borraria"
        )

    print(f"{ORIGEM.name}: {largura}x{altura} RGBA")

    entradas = []
    for lado in TAMANHOS:
        reduzido = pixels if lado == largura else adensar(reduzir(pixels, largura, lado), lado)
        corpo = escrever_png(reduzido, lado) if lado >= LIMITE_PNG else dib(reduzido, lado)
        entradas.append((lado, corpo))
        print(f"  {lado:>3} px  {'PNG' if lado >= LIMITE_PNG else 'DIB'}  {len(corpo):>7,} bytes")

    for nome in ("FiscalDoc.ico", "FiscalDocDocumento.ico"):
        caminho = DESTINO / nome
        montar_ico(entradas, caminho)
        print(f"{nome}: {caminho.stat().st_size:,} bytes, {len(entradas)} tamanhos")

    # Nenhum `adensar` aqui: a partir de 32 px o traco ja tem largura de sobra,
    # e o ganho seria negativo de qualquer forma.
    DESTINO_ASSISTENTE.mkdir(parents=True, exist_ok=True)

    for lado in TAMANHOS_ASSISTENTE:
        dados = bmp(reduzir(pixels, largura, lado), lado)
        caminho = DESTINO_ASSISTENTE / f"assistente-{lado}.bmp"
        caminho.write_bytes(dados)
        print(f"{caminho.name}: {len(dados):,} bytes, {lado}x{lado} BMP 32 bits")


if __name__ == "__main__":
    main()
