using FiscalDoc.Core.Values;

namespace FiscalDoc.Core.Model.Nfe;

/// <summary>Grupo ide (B): identificacao da NF-e.</summary>
public sealed record Identificacao(
    string? CodigoUf,
    string? NaturezaOperacao,
    string? Modelo,
    string? Serie,
    string? Numero,
    DateTimeOffset? DataHoraEmissao,
    DateTimeOffset? DataHoraSaidaEntrada,
    TipoOperacao TipoOperacao,
    TipoImpressao TipoImpressao,
    TipoEmissao TipoEmissao,
    Ambiente Ambiente,
    string? MunicipioFatoGerador,
    DateTimeOffset? DataHoraContingencia,
    string? JustificativaContingencia);

/// <summary>
/// NF-e modelo 55 ou NFC-e modelo 65, pronta para o layout. Tudo que o DANFE
/// e o DANFE NFC-e precisam esta aqui, e nada aqui sabe o que e um milimetro.
///
/// <para>Um unico tipo para os dois modelos porque o leiaute e literalmente o
/// mesmo arquivo: a NFC-e e a NF-e com <c>mod=65</c>, mais o grupo
/// <c>infNFeSupl</c>, e com algumas restricoes de preenchimento. Separar em
/// dois registros duplicaria trinta campos identicos para distinguir dois. O
/// que muda de verdade e o <i>desenho</i>, e essa escolha fica no layout, que
/// consulta <see cref="EhNfce"/>.</para>
/// </summary>
public sealed record NfeDocumento(
    ChaveAcesso? Chave,
    string? VersaoLeiaute,
    Identificacao Ide,
    Emitente Emitente,
    Destinatario Destinatario,
    IReadOnlyList<ItemNfe> Itens,
    TotaisIcms Totais,
    TotaisIssqn? Issqn,
    TotaisIbsCbs? IbsCbs,
    ModalidadeFrete Frete,
    Transportador? Transportador,
    Veiculo? Veiculo,
    IReadOnlyList<Volume> Volumes,
    Fatura? Fatura,
    IReadOnlyList<Duplicata> Duplicatas,
    InformacoesAdicionais? InfoAdicionais,
    Pagamentos Pagamentos,
    InformacoesSuplementares? Suplementares,
    Protocolo? Protocolo) : DocumentoFiscal
{
    public override FamiliaDocumento Familia => FamiliaDocumento.Nfe;

    /// <summary>Modelo 65. Decide qual dos dois documentos auxiliares sai.</summary>
    public bool EhNfce => Ide.Modelo == ModeloFiscal.Nfce;

    public override string TituloCurto
    {
        get
        {
            string numero = Ide.Numero ?? "s/n";
            string serie = Ide.Serie ?? "0";
            string sigla = EhNfce ? "NFC-e" : "NF-e";
            return $"{sigla} {numero} - série {serie}";
        }
    }

    /// <summary>
    /// Documento sem protocolo de autorizacao. Nao e erro: em contingencia
    /// FS/FS-DA o protocolo so existe depois, e o DANFE e impresso antes.
    /// </summary>
    public bool SemProtocolo => Protocolo?.Numero is null;

    /// <summary>
    /// Homologacao exige a frase "SEM VALOR FISCAL" em Informacoes
    /// Complementares ou em marca d'agua destacada (MOC Anexo II, secao 3).
    /// </summary>
    public bool ExigeSemValorFiscal => Ide.Ambiente == Ambiente.Homologacao;

    /// <summary>Retrato (1) ou paisagem (2). Qualquer outro valor cai em retrato.</summary>
    public bool Paisagem => Ide.TipoImpressao == TipoImpressao.Paisagem;

    /// <summary>
    /// Contingencia offline da NFC-e (tpEmis 9). O Manual do DANFE NFC-e
    /// (v6.0, divisao VIII) manda imprimir "EMITIDA EM CONTINGÊNCIA / Pendente
    /// de autorização" em dois lugares do documento, e suprimir o protocolo.
    /// </summary>
    public bool ContingenciaOffline => Ide.TipoEmissao == TipoEmissao.ContingenciaOfflineNfce;
}
