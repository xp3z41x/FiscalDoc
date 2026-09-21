using FiscalDoc.Core.Model.Nfe;
using FiscalDoc.Core.Values;

namespace FiscalDoc.Core.Model.Mdfe;

/// <summary>
/// Modal do MDF-e. Note que <b>nao ha dutoviario</b> aqui: o conjunto de
/// modais do MDF-e difere do conjunto do CT-e, e o MOC do MDF-e so publica
/// modelos de DAMDFE para rodoviario, aereo, aquaviario e ferroviario.
/// </summary>
public enum ModalMdfe
{
    Rodoviario = 1,
    Aereo = 2,
    Aquaviario = 3,
    Ferroviario = 4,
}

/// <summary>tpEmit: quem emite o manifesto.</summary>
public enum TipoEmitenteMdfe
{
    PrestadorServicoTransporte = 1,
    CargaPropria = 2,
    CteGlobalizado = 3,
}

public static class RotulosMdfe
{
    public static string Modal(ModalMdfe m) => m switch
    {
        ModalMdfe.Rodoviario => "Rodoviário",
        ModalMdfe.Aereo => "Aéreo",
        ModalMdfe.Aquaviario => "Aquaviário",
        ModalMdfe.Ferroviario => "Ferroviário",
        _ => "—",
    };

    public static string Emitente(TipoEmitenteMdfe t) => t switch
    {
        TipoEmitenteMdfe.PrestadorServicoTransporte => "1 - Prestador de serviço de transporte",
        TipoEmitenteMdfe.CargaPropria => "2 - Transporte de carga própria",
        TipoEmitenteMdfe.CteGlobalizado => "3 - CT-e Globalizado",
        _ => "—",
    };
}

/// <summary>Condutor do veiculo de tracao.</summary>
public sealed record Condutor(string? Nome, string? Cpf);

/// <summary>Veiculo de tracao ou reboque.</summary>
public sealed record VeiculoMdfe(
    string? Placa,
    string? Renavam,
    string? Uf,
    decimal? Tara,
    decimal? CapacidadeKg,
    IReadOnlyList<Condutor> Condutores);

/// <summary>Documento vinculado, agrupado pelo municipio de descarga.</summary>
public sealed record DocumentoDescarga(
    string? Municipio,
    string? CodigoMunicipio,
    string? Tipo,
    string? Chave);

/// <summary>Totais do manifesto (grupo tot).</summary>
public sealed record TotaisMdfe(
    int? QuantidadeCte,
    int? QuantidadeNfe,
    int? QuantidadeMdfe,
    decimal? ValorCarga,
    string? UnidadeMedida,
    decimal? PesoCarga);

/// <summary>MDF-e modelo 58, leiaute 3.00.</summary>
public sealed record MdfeDocumento(
    ChaveAcesso? Chave,
    string? VersaoLeiaute,
    string? Numero,
    string? Serie,
    DateTimeOffset? DataHoraEmissao,
    ModalMdfe Modal,
    TipoEmitenteMdfe TipoEmitente,
    TipoImpressao TipoImpressao,
    TipoEmissao TipoEmissao,
    Ambiente Ambiente,
    string? UfInicio,
    string? UfFim,
    IReadOnlyList<string> MunicipiosCarregamento,
    IReadOnlyList<string> Percurso,
    DateTimeOffset? DataHoraInicioViagem,
    ParticipanteMdfe Emitente,
    string? Rntrc,
    VeiculoMdfe? VeiculoTracao,
    IReadOnlyList<VeiculoMdfe> Reboques,
    IReadOnlyList<DocumentoDescarga> Documentos,
    TotaisMdfe Totais,
    IReadOnlyList<string> Lacres,
    string? ProdutoPredominante,
    string? Observacoes,
    DateTimeOffset? DataHoraContingencia,
    string? JustificativaContingencia,
    Protocolo? Protocolo) : DocumentoFiscal
{
    public override FamiliaDocumento Familia => FamiliaDocumento.Mdfe;

    public override string TituloCurto => $"MDF-e {Numero ?? "s/n"} - série {Serie ?? "0"}";

    public bool SemProtocolo => Protocolo?.Numero is null;

    /// <summary>MOC MDF-e 2.6: frase obrigatória na área do protocolo.</summary>
    public bool ExigeSemValorFiscal => Ambiente == Ambiente.Homologacao;

    /// <summary>
    /// MOC MDF-e 2.5: em contingência é obrigatório imprimir
    /// "EMISSÃO EM CONTINGÊNCIA", em destaque, no lugar reservado ao protocolo.
    ///
    /// <para>O domínio de <c>tpEmis</c> do MDF-e tem três valores, e só um
    /// deles é contingência: 1 Normal, 2 Contingência Off-Line, 3 Regime
    /// Especial NFF. Testar <c>!= Normal</c> carimbava "EMISSÃO EM
    /// CONTINGÊNCIA" sobre o protocolo de um manifesto NFF <b>autorizado</b>,
    /// junto com o aviso de transmissão em 168 horas que não se aplica a
    /// ele.</para>
    ///
    /// <para>A comparacao e pelo <b>valor</b> 2 porque o enum e o da NF-e,
    /// onde 2 se chama <c>ContingenciaFsIa</c>. Os tres documentos
    /// compartilham o tipo mas nao o dominio: no MDF-e o 2 e a contingencia
    /// off-line. O nome do membro mente aqui; o numero, nao.</para>
    /// </summary>
    public bool EmContingencia => (int)TipoEmissao == 2;

    /// <summary>
    /// O MOC do MDF-e admite retrato e paisagem sem preferir nenhum
    /// ("papel comum nas orientações retrato ou paisagem"). O retrato é o
    /// praticado, porque a lista de documentos vinculados é vertical.
    /// </summary>
    public bool Paisagem => TipoImpressao == TipoImpressao.Paisagem;
}

/// <summary>Emitente do MDF-e.</summary>
public sealed record ParticipanteMdfe(
    string? RazaoSocial,
    string? NomeFantasia,
    string? Cnpj,
    string? Cpf,
    string? InscricaoEstadual,
    Endereco Endereco)
{
    public string? Documento => Cnpj ?? Cpf;
}
