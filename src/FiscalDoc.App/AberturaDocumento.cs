using FiscalDoc.Core.Model;
using FiscalDoc.Core.Model.Cte;
using FiscalDoc.Core.Model.Evento;
using FiscalDoc.Core.Model.Mdfe;
using FiscalDoc.Core.Model.Nfe;
using FiscalDoc.Core.Parsing;
using FiscalDoc.Layout;
using FiscalDoc.Layout.Dacte;
using FiscalDoc.Layout.Damdfe;
using FiscalDoc.Layout.Danfe;
using FiscalDoc.Layout.Evento;
using FiscalDoc.Layout.Nfce;
using FiscalDoc.Layout.DisplayList;
using FiscalDoc.Render.Wpf;

namespace FiscalDoc.App;

/// <summary>
/// Liga as tres camadas: caminho de arquivo -> modelo -> paginas em milimetro.
///
/// E o unico lugar do aplicativo que conhece as tres ao mesmo tempo. A
/// interface nao sabe o que e um parser, o parser nao sabe o que e um layout,
/// e o layout nao sabe o que e uma janela.
/// </summary>
internal sealed class AberturaDocumento
{
    private readonly MedidorTextoWpf _medidor = new();

    /// <summary>Resultado da abertura, ja pronto para a tela.</summary>
    internal sealed record Resultado(
        ConjuntoPaginas? Paginas,
        string? Mensagem,
        string Titulo);

    internal Resultado Abrir(string caminho)
    {
        ResultadoLeitura leitura = LeitorDocumento.Ler(caminho);

        if (leitura is not ResultadoLeitura.Ok ok)
        {
            return new Resultado(null, leitura.MensagemUsuario, Mensagens.TituloJanela);
        }

        try
        {
            return Montar(ok.Documento);
        }
        catch (Exception ex) when (ex is ArgumentException
                                      or InvalidOperationException
                                      or FormatException
                                      or OverflowException
                                      or IndexOutOfRangeException
                                      or KeyNotFoundException)
        {
            // O leitor ja garante que nenhum arquivo derruba o aplicativo, mas
            // a MONTAGEM tambem le dado de arquivo, e ha pelo menos um ponto
            // onde isso lanca: o QR da NFC-e vem inteiro do XML, e um
            // infNFeSupl/qrCode acima de 2331 bytes faz QrCode.Codificar
            // recusar. Sem esta rede, um unico arquivo malformado trocava a
            // mensagem desenhada na area do documento por uma caixa de
            // "erro inesperado" com pilha de chamadas - exatamente o que o
            // plano 2.9 proibe.
            //
            // Nao se captura Exception nua: falha de memoria ou do proprio
            // runtime tem de continuar subindo.
            return new Resultado(null, Mensagens.LayoutFalhou, Mensagens.TituloJanela);
        }
    }

    private Resultado Montar(DocumentoFiscal documento) =>
        documento switch
        {
            NfeDocumento nfe => MontarNfe(nfe),

            CteDocumento cte => Montar(
                DacteLayout.Construir(cte, _medidor), cte.TituloCurto),

            MdfeDocumento mdfe => Montar(
                DamdfeLayout.Construir(mdfe, _medidor), mdfe.TituloCurto),

            EventoDocumento ev => Montar(
                EventoLayout.Construir(ev, _medidor), ev.TituloCurto),

            _ => new Resultado(
                null,
                Mensagens.SemLayoutAinda(documento.Familia),
                Mensagens.TituloJanela),
        };

    private static Resultado Montar(ConjuntoPaginas paginas, string titulo) =>
        new(paginas, null, $"{Mensagens.TituloJanela} - {titulo}");

    private Resultado MontarNfe(NfeDocumento nfe)
    {
        // O modelo decide o documento auxiliar, e nao o tpImp: a NFC-e sempre
        // sai como DANFE NFC-e em bobina, mesmo que o emissor tenha gravado
        // tpImp 5 (mensagem eletronica) ou um valor fora do dominio. Para a
        // NF-e, tpImp decide entre retrato e paisagem.
        if (nfe.EhNfce)
        {
            return Montar(DanfeNfce.Construir(nfe, _medidor), nfe.TituloCurto);
        }

        ConjuntoPaginas paginas = nfe.Paisagem
            ? DanfePaisagem.Construir(nfe, _medidor)
            : DanfeRetrato.Construir(nfe, _medidor);

        return Montar(paginas, nfe.TituloCurto);
    }

}

/// <summary>
/// Todo texto que o usuario le, num lugar so. Facilita conferir tom e
/// ortografia, e impede que uma mensagem nasca escondida no meio do codigo.
/// </summary>
internal static class Mensagens
{
    internal const string TituloJanela = "FiscalDoc";

    internal const string FiltroAbertura =
        "Documentos fiscais (*.xml)|*.xml|Todos os arquivos (*.*)|*.*";

    internal const string SemDocumento =
        "Abra um XML de NF-e, NFC-e, CT-e, MDF-e ou evento."
        + "\n\nVocê também pode arrastar o arquivo para esta janela.";

    /// <summary>
    /// O arquivo foi lido e reconhecido, mas nao foi possivel montar a
    /// representacao. Acontece com dado fora do que o schema admite - um
    /// qrCode grande demais para caber num simbolo QR, por exemplo.
    /// </summary>
    internal const string LayoutFalhou =
        "Este documento foi lido, mas não foi possível montar a representação gráfica."
        + "\n\nO arquivo provavelmente tem algum campo fora do padrão.";

    /// <summary>
    /// O zoom pedido nao cabe na memoria desta maquina. A previa volta
    /// para "ajustar pagina", que sempre cabe.
    /// </summary>
    internal const string ZoomAlemDaMemoria =
        "Este zoom exige mais memória do que há disponível."
        + "\n\nA visualização voltou para \"ajustar página\".";

    /// <summary>
    /// Falha ao imprimir. Nao se afirma a causa: pode ser ausencia de
    /// impressora, driver recusando, fila que sumiu no meio do trabalho ou
    /// o subsistema de impressao do Windows.
    /// </summary>
    internal const string ImpressaoFalhou =
        "Não foi possível imprimir. Verifique se há uma impressora disponível e tente novamente.";

    internal static string SemLayoutAinda(FamiliaDocumento familia)
    {
        string nome = familia switch
        {
            FamiliaDocumento.Cte => "CT-e",
            FamiliaDocumento.Mdfe => "MDF-e",
            FamiliaDocumento.EventoNfe => "evento de NF-e",
            FamiliaDocumento.EventoCte => "evento de CT-e",
            FamiliaDocumento.EventoMdfe => "evento de MDF-e",
            _ => "este documento",
        };

        return $"O arquivo é um {nome} válido, mas a representação gráfica "
             + "deste tipo ainda não está disponível nesta versão.";
    }

    internal static string AvisoEscala(float escala) =>
        $"A impressora selecionada não comporta a largura total do documento. "
        + $"A página será reduzida para {escala * 100f:0.#}% para caber na área imprimível.";
}
