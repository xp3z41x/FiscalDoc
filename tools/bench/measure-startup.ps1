<#
.SYNOPSIS
  Linha de base de partida e memoria do FiscalDoc (plano 5).

.DESCRIPTION
  Mede do Start-Process ate a janela principal existir, o que inclui a
  inicializacao do host, do runtime e o carregamento de assemblies - que e
  justamente onde o tempo de um app .NET esta.

  ESTA E A MEDIDA BARATA, NAO A PRECISA. A medida precisa correlaciona o
  evento ETW de kernel Process/Start (t0) com o EventSource
  "FiscalDoc-Startup" / FirstPaintCompleted (t1) no PerfView. Use esta aqui
  para acompanhar regressao no dia a dia, e o PerfView quando o numero
  importar de verdade.

  Partida a frio precisa de reboot ou de limpeza da standby list entre
  execucoes; sem isso o que se mede e partida a quente. Meca sempre em par,
  com a protecao em tempo real do Defender ligada e desligada: conforme
  dotnet/runtime#78379 ela e a variavel dominante no frio.

.EXAMPLE
  .\measure-startup.ps1 -Iterations 15
  .\measure-startup.ps1 -Iterations 10 -DocumentPath "..\..	ests\Amostraseforma-com-is-e-total.xml"
#>
[CmdletBinding()]
param(
    [string] $Exe = "$PSScriptRoot\..\..\src\FiscalDoc.App\bin\Release\net10.0-windows\FiscalDoc.exe",
    [string] $DocumentPath = "",
    [int]    $Iterations = 10,
    [int]    $TimeoutMs = 15000,
    [int]    $SettleMs = 150
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path $Exe)) {
    throw "Executavel nao encontrado: $Exe`nCompile antes: dotnet build -c Release"
}
$Exe = (Resolve-Path $Exe).Path

$argList = @()
if ($DocumentPath) {
    if (-not (Test-Path $DocumentPath)) { throw "XML nao encontrado: $DocumentPath" }
    # Aspas obrigatorias: o caminho pode conter espacos (a propria pasta
    # "Exemplos XML" contem um). Sem elas o app recebe o caminho picado em
    # varios args - o mesmo motivo pelo qual shell\open\command usa "%1".
    $argList = @('"' + (Resolve-Path $DocumentPath).Path + '"')
}

Write-Host "Executavel : $Exe"
Write-Host "Documento  : $(if ($DocumentPath) { Split-Path $DocumentPath -Leaf } else { '(nenhum)' })"
Write-Host "Iteracoes  : $Iterations"

$defender = try {
    if ((Get-MpPreference -ErrorAction Stop).DisableRealtimeMonitoring) { 'desligada' } else { 'LIGADA' }
} catch { 'indeterminada' }
Write-Host "Defender RTP: $defender"
Write-Host ""

$times = [System.Collections.Generic.List[double]]::new()
$wsets = [System.Collections.Generic.List[double]]::new()
$psets = [System.Collections.Generic.List[double]]::new()

for ($i = 1; $i -le $Iterations; $i++) {
    $sw = [System.Diagnostics.Stopwatch]::StartNew()

    $p = if ($argList.Count -gt 0) {
        Start-Process -FilePath $Exe -ArgumentList $argList -PassThru
    } else {
        Start-Process -FilePath $Exe -PassThru
    }

    $shown = $false
    while ($sw.ElapsedMilliseconds -lt $TimeoutMs) {
        if ($p.HasExited) { break }
        $p.Refresh()
        if ($p.MainWindowHandle -ne [IntPtr]::Zero) { $shown = $true; break }
    }
    $ms = $sw.Elapsed.TotalMilliseconds
    $sw.Stop()

    if (-not $shown) {
        Write-Warning "Iteracao ${i}: janela nao apareceu em ${TimeoutMs}ms"
    } else {
        Start-Sleep -Milliseconds $SettleMs
        $p.Refresh()
        $ws = $p.WorkingSet64 / 1MB
        $pm = $p.PrivateMemorySize64 / 1MB
        $times.Add($ms); $wsets.Add($ws); $psets.Add($pm)
        Write-Host ("  {0,2}: {1,7:N1} ms   WS {2,6:N1} MB   Private {3,6:N1} MB" -f $i, $ms, $ws, $pm)
    }

    if (-not $p.HasExited) { $p.CloseMainWindow() | Out-Null }
    if (-not $p.WaitForExit(3000)) { $p.Kill($true); Write-Warning "Iteracao ${i}: precisou de Kill - a janela nao fechou sozinha" }
    $p.Dispose()
}

function Stat($values, $q) {
    $s = @($values | Sort-Object)
    if ($s.Count -eq 0) { return [double]::NaN }
    $idx = [math]::Min($s.Count - 1, [math]::Max(0, [int][math]::Ceiling($q * $s.Count) - 1))
    return $s[$idx]
}

if ($times.Count -eq 0) { throw "Nenhuma iteracao valida." }

Write-Host ""
Write-Host "--- Resultado ($($times.Count) amostras) ---"
Write-Host ("Partida -> janela   mediana {0,7:N1} ms    p95 {1,7:N1} ms    min {2,7:N1} ms" -f (Stat $times 0.5), (Stat $times 0.95), ($times | Measure-Object -Minimum).Minimum)
Write-Host ("Working set         mediana {0,7:N1} MB    p95 {1,7:N1} MB" -f (Stat $wsets 0.5), (Stat $wsets 0.95))
Write-Host ("Memoria privada     mediana {0,7:N1} MB    p95 {1,7:N1} MB" -f (Stat $psets 0.5), (Stat $psets 0.95))
Write-Host ""
Write-Host "Metas do plano 5: quente <= 250 ms, frio <= 800 ms, privada <= 80 MB."
