using FiscalDoc.Core.Model.Nfe;
using FiscalDoc.Core.Values;

namespace FiscalDoc.Core.Model.Cte;

/// <summary>Modal do CT-e (ide/modal).</summary>
public enum ModalCte
{
    Rodoviario = 1,
    Aereo = 2,
    Aquaviario = 3,
    Ferroviario = 4,
    Dutoviario = 5,
    Multimodal = 6,
}

/// <summary>tpCTe: tipo do conhecimento.</summary>
public enum TipoCte
{
    Normal = 0,
    Complemento = 1,
    Anulacao = 2,
    Substituto = 3,
}

/// <summary>tpServ: natureza do servico prestado.</summary>
public enum TipoServicoCte
{
    Normal = 0,
    Subcontratacao = 1,
    Redespacho = 2,
    RedespachoIntermediario = 3,
    ServicoVinculadoMultimodal = 4,
}

public static class RotulosCte
{
    /// <summary>
    /// Dizer de contingencia do <b>CT-e</b>.
    ///
    /// <para>Convencao da casa, e nao transcricao: diferente do DANFE, cujo
    /// dizer o MOC 7.0 Anexo III fixa palavra por palavra, o manual do DACTE
    /// traz apenas figuras. O que nao pode e o que acontecia antes - reusar o
    /// texto da NF-e e estampar a palavra <b>DANFE</b> num CT-e.</para>
    ///
    /// <para>Normal e Regime Especial NFF nao tem dizer: o primeiro porque
    /// nao e contingencia, o segundo porque e regime proprio e autorizado.</para>
    /// </summary>
    public static string? DizerContingencia(TipoEmissao t) => t switch
    {
        TipoEmissao.ContingenciaFsIa or TipoEmissao.ContingenciaFsDa =>
            "DACTE em contingência - impresso em decorrência de problemas técnicos",

        TipoEmissao.ContingenciaEpec =>
            "DACTE impresso em contingência - EPEC regularmente recebido",

        _ => null,
    };

    public static string Modal(ModalCte m) => m switch
    {
        ModalCte.Rodoviario => "Rodoviário",
        ModalCte.Aereo => "Aéreo",
        ModalCte.Aquaviario => "Aquaviário",
        ModalCte.Ferroviario => "Ferroviário",
        ModalCte.Dutoviario => "Dutoviário",
        ModalCte.Multimodal => "Multimodal",
        _ => "—",
    };

    public static string Tipo(TipoCte t) => t switch
    {
        TipoCte.Normal => "0 - Normal",
        TipoCte.Complemento => "1 - Complemento de Valores",
        TipoCte.Anulacao => "2 - Anulação de Valores",
        TipoCte.Substituto => "3 - Substituto",
        _ => "—",
    };

    public static string Servico(TipoServicoCte t) => t switch
    {
        TipoServicoCte.Normal => "0 - Normal",
        TipoServicoCte.Subcontratacao => "1 - Subcontratação",
        TipoServicoCte.Redespacho => "2 - Redespacho",
        TipoServicoCte.RedespachoIntermediario => "3 - Redespacho Intermediário",
        TipoServicoCte.ServicoVinculadoMultimodal => "4 - Serviço Vinculado a Multimodal",
        _ => "—",
    };

    /// <summary>
    /// Tomador do servico: o leiaute o indica por um codigo em toma3/toma, e
    /// quando e "outros" (4) os dados vem no grupo toma4.
    /// </summary>
    public static string Tomador(int? codigo) => codigo switch
    {
        0 => "Remetente",
        1 => "Expedidor",
        2 => "Recebedor",
        3 => "Destinatário",
        4 => "Outros",
        _ => "—",
    };
}

/// <summary>Participante do CT-e. O mesmo formato serve aos cinco papeis.</summary>
public sealed record ParticipanteCte(
    string? RazaoSocial,
    string? NomeFantasia,
    string? Cnpj,
    string? Cpf,
    string? InscricaoEstadual,
    string? Fone,
    string? Email,
    Endereco Endereco)
{
    public string? Documento => Cnpj ?? Cpf;
}

/// <summary>Componente do valor da prestacao (grupo Comp).</summary>
public sealed record ComponenteValor(string? Nome, decimal? Valor);

/// <summary>Documento originario transportado.</summary>
public sealed record DocumentoOriginario(
    string? Tipo,
    string? Chave,
    string? Numero,
    string? Serie,
    decimal? Valor);

/// <summary>Quantidade de carga (grupo infQ).</summary>
public sealed record QuantidadeCarga(string? Unidade, string? TipoMedida, decimal? Quantidade);

/// <summary>ICMS do CT-e, achatado do grupo de escolha.</summary>
public sealed record IcmsCte(
    string? Cst,
    decimal? BaseCalculo,
    decimal? Aliquota,
    decimal? Valor,
    decimal? PercentualReducaoBc,
    string? Motivo);

/// <summary>CT-e modelo 57, leiaute 3.00 ou 4.00.</summary>
public sealed record CteDocumento(
    ChaveAcesso? Chave,
    string? VersaoLeiaute,
    string? Numero,
    string? Serie,
    DateTimeOffset? DataHoraEmissao,
    string? NaturezaOperacao,
    string? Cfop,
    TipoCte Tipo,
    TipoServicoCte Servico,
    ModalCte Modal,
    TipoImpressao TipoImpressao,
    TipoEmissao TipoEmissao,
    Ambiente Ambiente,
    string? MunicipioEnvio,
    string? UfEnvio,
    string? MunicipioInicio,
    string? UfInicio,
    string? MunicipioFim,
    string? UfFim,
    int? CodigoTomador,
    ParticipanteCte Emitente,
    ParticipanteCte? Remetente,
    ParticipanteCte? Expedidor,
    ParticipanteCte? Recebedor,
    ParticipanteCte? Destinatario,
    ParticipanteCte? Tomador,
    decimal? ValorTotalPrestacao,
    decimal? ValorReceber,
    IReadOnlyList<ComponenteValor> Componentes,
    IcmsCte Icms,
    decimal? ValorTotalTributos,
    decimal? ValorCarga,
    string? ProdutoPredominante,
    IReadOnlyList<QuantidadeCarga> Quantidades,
    IReadOnlyList<DocumentoOriginario> DocumentosOriginarios,
    string? Rntrc,
    string? Observacoes,
    string? ObservacoesFisco,
    DateTimeOffset? DataHoraContingencia,
    string? JustificativaContingencia,
    Protocolo? Protocolo) : DocumentoFiscal
{
    public override FamiliaDocumento Familia => FamiliaDocumento.Cte;

    public override string TituloCurto => $"CT-e {Numero ?? "s/n"} - série {Serie ?? "0"}";

    public bool SemProtocolo => Protocolo?.Numero is null;

    /// <summary>
    /// MOC CT-e 2.20: em homologação é obrigatória a frase, centralizada e em
    /// caixa alta, na área do protocolo.
    /// </summary>
    public bool ExigeSemValorFiscal => Ambiente == Ambiente.Homologacao;

    /// <summary>
    /// O DACTE é publicado em paisagem nos modelos do MOC, e é como o mercado
    /// imprime. tpImp = 1 força retrato quando o emitente pediu.
    /// </summary>
    public bool Paisagem => TipoImpressao != TipoImpressao.Retrato;

    public string NomeTomador => RotulosCte.Tomador(CodigoTomador);
}
