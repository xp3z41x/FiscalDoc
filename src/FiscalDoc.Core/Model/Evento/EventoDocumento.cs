using FiscalDoc.Core.Model.Nfe;
using FiscalDoc.Core.Values;

namespace FiscalDoc.Core.Model.Evento;

/// <summary>
/// Evento de NF-e, CT-e ou MDF-e, tratado como <b>documento independente</b>:
/// o FiscalDoc abre o arquivo do evento sozinho, sem procurar a nota de origem.
///
/// <para><b>Nao existe layout obrigatorio para eventos.</b> Nenhum dos tres
/// MOCs define representacao grafica de carta de correcao, cancelamento ou
/// manifestacao - eles tratam eventos como servico de XML. A Carta de Correcao
/// tem sim um texto legal de redacao fixa, o <c>xCondUso</c>, mas essa e uma
/// obrigacao do <i>arquivo</i>, nao de um impresso. Imprimir CC-e e convencao
/// de mercado.</para>
///
/// <para>O layout usado pelo FiscalDoc e, portanto, desenho proprio.</para>
/// </summary>
public sealed record EventoDocumento(
    FamiliaDocumento FamiliaOrigem,
    ChaveAcesso? ChaveReferenciada,
    string? Orgao,
    Ambiente Ambiente,
    string? CnpjAutor,
    string? CpfAutor,
    string? CodigoEvento,
    string? DescricaoEvento,
    int? SequenciaEvento,
    DateTimeOffset? DataHoraEvento,
    string? VersaoEvento,
    IReadOnlyList<(string Rotulo, string Valor)> Detalhes,
    string? TextoCorrecao,
    string? CondicoesUso,
    string? Justificativa,
    string? ProtocoloReferenciado,
    RetornoEvento? Retorno) : DocumentoFiscal
{
    public override FamiliaDocumento Familia => FamiliaOrigem switch
    {
        FamiliaDocumento.Cte => FamiliaDocumento.EventoCte,
        FamiliaDocumento.Mdfe => FamiliaDocumento.EventoMdfe,
        _ => FamiliaDocumento.EventoNfe,
    };

    public override string TituloCurto
    {
        get
        {
            string tipo = DescricaoEvento ?? TiposEvento.Descrever(CodigoEvento);
            return SequenciaEvento is > 1 ? $"{tipo} ({SequenciaEvento}ª)" : tipo;
        }
    }

    public string? DocumentoAutor => CnpjAutor ?? CpfAutor;

    public bool Registrado => Retorno?.Protocolo is not null;

    public bool ExigeSemValorFiscal => Ambiente == Ambiente.Homologacao;

    /// <summary>Carta de Correcao: o unico evento com corpo de texto livre longo.</summary>
    public bool EhCartaCorrecao => CodigoEvento is "110110" or "110111"
        ? CodigoEvento == "110110"
        : !string.IsNullOrWhiteSpace(TextoCorrecao);
}

/// <summary>Grupo retEvento: a resposta da SEFAZ ao registro do evento.</summary>
public sealed record RetornoEvento(
    string? CodigoStatus,
    string? Motivo,
    string? Protocolo,
    DateTimeOffset? DataHoraRegistro,
    string? Orgao);

/// <summary>
/// Codigos de evento. A lista cobre os que aparecem em arquivo de
/// contribuinte; qualquer outro e exibido pelo proprio codigo, em vez de
/// virar "desconhecido".
/// </summary>
public static class TiposEvento
{
    public static string Descrever(string? codigo) => codigo switch
    {
        // NF-e
        "110110" => "Carta de Correção Eletrônica",
        "110111" => "Cancelamento de NF-e",
        "110112" => "Cancelamento por Substituição",
        "110140" => "EPEC - Evento Prévio de Emissão em Contingência",
        "111500" => "Pedido de Prorrogação",
        "111501" => "Pedido de Prorrogação (2º prazo)",
        "210200" => "Confirmação da Operação",
        "210210" => "Ciência da Operação",
        "210220" => "Desconhecimento da Operação",
        "210240" => "Operação não Realizada",

        // CT-e
        "110113" => "EPEC do CT-e",
        "110160" => "Comprovante de Entrega do CT-e",
        "110161" => "Cancelamento do Comprovante de Entrega",
        "110170" => "Insucesso na Entrega do CT-e",

        // MDF-e
        "110114" => "Inclusão de Condutor",
        "110115" => "Inclusão de DF-e",
        "110116" => "Pagamento da Operação de Transporte",
        "110117" => "Confirmação do Serviço de Transporte",
        "110118" => "Alteração do Pagamento do Serviço",
        "132000" => "Encerramento do MDF-e",

        null or "" => "Evento",
        _ => $"Evento {codigo}",
    };

    public const string CartaCorrecao = "110110";
}
