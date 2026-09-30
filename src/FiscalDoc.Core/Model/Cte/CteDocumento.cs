using System.Globalization;
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

/// <summary>
/// Participante do CT-e. O mesmo formato serve aos cinco papeis.
///
/// <para>A inscricao na SUFRAMA so existe no destinatario (dest/ISUF): e o
/// que o manual do DACTE chama de "Inscricao SUFRAMA Destinatario".</para>
/// </summary>
public sealed record ParticipanteCte(
    string? RazaoSocial,
    string? NomeFantasia,
    string? Cnpj,
    string? Cpf,
    string? InscricaoEstadual,
    string? Fone,
    string? Email,
    Endereco Endereco,
    string? InscricaoSuframa = null)
{
    public string? Documento => Cnpj ?? Cpf;
}

/// <summary>Componente do valor da prestacao (grupo Comp).</summary>
public sealed record ComponenteValor(string? Nome, decimal? Valor);

/// <summary>
/// Documento que o CT-e cita: a nota da carga (quadro "Documentos
/// Originarios"), o conhecimento do transportador anterior, o CT-e que este
/// complementa ou substitui.
///
/// <para>O manual do DACTE (MOC CT-e 4.00, Anexo II, secao 3) imprime o
/// documento originario em tres colunas - TP DOC, CNPJ/CPF EMITENTE e
/// SERIE/No DOCUMENTO -, e e por elas que o leitor reconhece a nota. Mostrar
/// so a chave de 44 digitos, como se fazia antes, e mostrar o numero da nota
/// de um jeito que nenhum humano le: a reclamacao que motivou esta forma veio
/// do financeiro de um usuario, que nao achava no DACTE a nota do frete.</para>
///
/// <para>Para documento eletronico os tres vem da propria chave de acesso -
/// ver <see cref="Eletronico"/>. Isso nao e imprimir o que nao esta no
/// arquivo (MOC 2.1): a chave esta no XML, e a composicao dela e normativa.
/// Para documento em papel, o manual manda tirar o CNPJ/CPF do remetente.</para>
/// </summary>
public sealed record DocumentoReferenciado(
    string Tipo,
    string? DocumentoEmitente,
    string? Serie,
    string? Numero,
    string? ChaveInformada = null,
    DateOnly? DataEmissao = null)
{
    /// <summary>A chave, quando o texto do arquivo forma uma. Null em papel.</summary>
    public ChaveAcesso? Chave => ChaveAcesso.DeAtributoId(ChaveInformada);

    /// <summary>
    /// Documento eletronico citado pela chave: emitente, serie e numero saem
    /// dela. Uma chave malformada nao some - fica em
    /// <see cref="ChaveInformada"/>, como veio, e o resto fica em branco.
    /// </summary>
    public static DocumentoReferenciado Eletronico(
        string tipo,
        string? chave,
        string? documentoEmitente = null,
        DateOnly? dataEmissao = null)
    {
        ChaveAcesso? c = ChaveAcesso.DeAtributoId(chave);

        return new DocumentoReferenciado(
            tipo,
            documentoEmitente ?? c?.DocumentoEmitente,
            c?.Serie,
            c?.Numero,
            chave,
            dataEmissao);
    }
}

/// <summary>
/// Quem emitiu os documentos de transporte anterior (infCTeNorm/docAnt/
/// emiDocAnt) - na subcontratacao e no redespacho, o transportador que levou
/// a carga antes - e os documentos dele, em papel ou CT-e. O agrupamento e o
/// do XML: o emitente vem uma vez, os documentos vem abaixo.
/// </summary>
public sealed record EmissorDocumentoAnterior(
    string? Nome,
    string? Documento,
    string? InscricaoEstadual,
    string? Uf,
    IReadOnlyList<DocumentoReferenciado> Documentos);

/// <summary>Quantidade de carga (grupo infQ).</summary>
public sealed record QuantidadeCarga(string? Unidade, string? TipoMedida, decimal? Quantidade);

/// <summary>
/// ICMS do CT-e, achatado do grupo de escolha.
///
/// <para><see cref="Descricao"/> e o CST por extenso, de acordo com o grupo em
/// que veio: o mesmo CST 90 quer dizer "outros" em ICMS90, "devido a UF de
/// origem" em ICMSOutraUF e Simples Nacional em ICMSSN, e so o grupo
/// desempata.</para>
/// </summary>
public sealed record IcmsCte(
    string? Cst,
    decimal? BaseCalculo,
    decimal? Aliquota,
    decimal? Valor,
    decimal? PercentualReducaoBc,
    string? Descricao);

/// <summary>
/// Campo livre do emitente (compl/ObsCont) ou de interesse do fisco
/// (compl/ObsFisco): o nome vem no atributo xCampo e o conteudo em xTexto.
/// </summary>
public sealed record CampoLivre(string? Nome, string? Texto)
{
    public string Linha => string.IsNullOrWhiteSpace(Nome) ? Texto ?? string.Empty : $"{Nome}: {Texto}";
}

/// <summary>Previsao do fluxo da carga (compl/fluxo): origem, passagens e destino.</summary>
public sealed record FluxoCarga(
    string? Origem,
    IReadOnlyList<string> Passagens,
    string? Destino,
    string? Rota);

/// <summary>
/// Previsao de entrega (compl/Entrega). O leiaute separa a data e a hora em
/// dois grupos de escolha, cada um com o seu tipo - "ate a data", "a partir
/// do horario", "no intervalo" - e e o tipo que diz como ler o valor.
/// </summary>
public sealed record PrevisaoEntrega(
    int? TipoData,
    DateOnly? DataProgramada,
    DateOnly? DataInicial,
    DateOnly? DataFinal,
    int? TipoHora,
    TimeOnly? HoraProgramada,
    TimeOnly? HoraInicial,
    TimeOnly? HoraFinal)
{
    public string DescricaoData => TipoData switch
    {
        0 => "Sem data definida",
        1 => $"Em {Formatos.Data(DataProgramada)}",
        2 => $"Até {Formatos.Data(DataProgramada)}",
        3 => $"A partir de {Formatos.Data(DataProgramada)}",
        4 => $"De {Formatos.Data(DataInicial)} a {Formatos.Data(DataFinal)}",
        _ => string.Empty,
    };

    public string DescricaoHora => TipoHora switch
    {
        0 => "Sem hora definida",
        1 => $"Às {Hora(HoraProgramada)}",
        2 => $"Até as {Hora(HoraProgramada)}",
        3 => $"A partir das {Hora(HoraProgramada)}",
        4 => $"Das {Hora(HoraInicial)} às {Hora(HoraFinal)}",
        _ => string.Empty,
    };

    /// <summary>HH:mm, e os segundos so quando o arquivo os usa de fato.</summary>
    private static string Hora(TimeOnly? h) => h is not { } v
        ? string.Empty
        : v.ToString(v.Second == 0 ? "HH:mm" : "HH:mm:ss", CultureInfo.InvariantCulture);
}

/// <summary>Dados complementares operacionais (grupo compl).</summary>
public sealed record ComplementoCte(
    string? CaracteristicaTransporte,
    string? CaracteristicaServico,
    FluxoCarga? Fluxo,
    PrevisaoEntrega? Entrega,
    IReadOnlyList<CampoLivre> CamposContribuinte,
    IReadOnlyList<CampoLivre> CamposFisco);

/// <summary>Cobranca do frete (infCTeNorm/cobr): fatura e duplicatas.</summary>
public sealed record CobrancaCte(Fatura? Fatura, IReadOnlyList<Duplicata> Duplicatas);

/// <summary>
/// Veiculo novo transportado (infCTeNorm/veicNovos). O manual do DACTE
/// (2.21.4, "casos especificos") manda imprimir estes dados quando existirem.
/// </summary>
public sealed record VeiculoNovo(
    string? Chassi,
    string? CodigoCor,
    string? Cor,
    string? MarcaModelo,
    decimal? ValorUnitario,
    decimal? FreteUnitario);

/// <summary>
/// CT-e que este documento cita por ser de um tipo especial: o complementado
/// (tpCTe 1), o substituido (tpCTe 3), o anulado (tpCTe 2, so no leiaute
/// 3.00) e os multimodais a que um servico vinculado se prende (tpServ 4).
/// </summary>
public sealed record ReferenciasCte(
    IReadOnlyList<DocumentoReferenciado> Complementados,
    DocumentoReferenciado? Substituido,
    bool AlteraTomador,
    DocumentoReferenciado? Anulado,
    IReadOnlyList<DocumentoReferenciado> Multimodais)
{
    public static ReferenciasCte Nenhuma { get; } = new([], null, false, null, []);
}

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
    bool Globalizado,
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
    TotaisIbsCbs? IbsCbs,
    decimal? ValorCarga,
    string? ProdutoPredominante,
    string? OutrasCaracteristicasCarga,
    IReadOnlyList<QuantidadeCarga> Quantidades,
    IReadOnlyList<DocumentoReferenciado> DocumentosOriginarios,
    IReadOnlyList<EmissorDocumentoAnterior> DocumentosAnteriores,
    ReferenciasCte Referencias,
    string? InformacoesGlobalizado,
    IReadOnlyList<VeiculoNovo> VeiculosNovos,
    CobrancaCte? Cobranca,
    string? Rntrc,
    DetalheModal? DetalheModal,
    ComplementoCte Complemento,
    string? Observacoes,
    string? ObservacoesFisco,
    DateTimeOffset? DataHoraContingencia,
    string? JustificativaContingencia,
    bool RecebedorRetira,
    string? DetalhesRetirada,
    string? QrCode,
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

    /// <summary>
    /// Quem paga o frete, com os dados completos.
    ///
    /// <para>MOC CT-e 4.00, Anexo II, secao 3: "Se toma, de toma4 (...). Se
    /// toma, de toma3 informado, pegar estes campos da pessoa referenciada".
    /// Quando o tomador e o remetente, o quadro do tomador repete o remetente -
    /// e e isso mesmo que o manual pede, porque o financeiro procura quem paga
    /// no quadro do tomador, e nao num codigo de uma posicao.</para>
    /// </summary>
    public ParticipanteCte? TomadorEfetivo => CodigoTomador switch
    {
        0 => Remetente,
        1 => Expedidor,
        2 => Recebedor,
        3 => Destinatario,
        _ => Tomador,
    };
}
