using System.Diagnostics.Tracing;

namespace FiscalDoc.App;

/// <summary>
/// Marcadores ETW para medicao de partida (plano 5).
///
/// t0 e o evento de kernel Process/Start, coletado por PerfView/WPR; estes sao
/// os t1. Stopwatch dentro do Main nao serve: ja exclui a inicializacao do
/// host, do runtime e o carregamento de assemblies, que e onde o tempo esta.
///
/// Nao e telemetria. Nada sai da maquina, nada e gravado em disco e o app nao
/// cria listener nenhum: sem um coletor ETW externo ligado, escrever um evento
/// aqui e essencialmente gratuito.
/// </summary>
[EventSource(Name = "FiscalDoc-Startup")]
internal sealed class StartupTrace : EventSource
{
    internal static readonly StartupTrace Log = new();

    private StartupTrace()
    {
    }

    /// <summary>Primeira linha do Main.</summary>
    [Event(1, Level = EventLevel.Informational)]
    internal void MainEntered() => WriteEvent(1);

    /// <summary>Fim do primeiro OnPaint completo da superficie de pagina.</summary>
    [Event(2, Level = EventLevel.Informational)]
    internal void FirstPaintCompleted() => WriteEvent(2);
}
