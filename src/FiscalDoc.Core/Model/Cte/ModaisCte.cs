namespace FiscalDoc.Core.Model.Cte;

/// <summary>
/// Informacoes especificas do modal (infCTeNorm/infModal): o quadro
/// "INFORMACOES ESPECIFICAS DO MODAL ..." do DACTE.
///
/// <para>Cada modal tem o seu schema (cteModalRodoviario, cteModalAereo...) e
/// o seu quadro no manual do DACTE (MOC CT-e 4.00, Anexo II, secoes 3 e 5).
/// Os codigos de dominio ja chegam aqui por extenso - "1 - Mutuo", nao "1" -,
/// porque e assim que o quadro os imprime e nao ha regra que os use como
/// numero.</para>
/// </summary>
public abstract record DetalheModal;

/// <summary>
/// Rodoviario: as ordens de coleta associadas. O RNTRC, o outro campo do
/// grupo, fica em <see cref="CteDocumento.Rntrc"/>, que ja o procura em
/// qualquer modal e versao do leiaute.
/// </summary>
public sealed record DetalheRodoviario(
    IReadOnlyList<OrdemColeta> OrdensColeta) : DetalheModal;

/// <summary>Ordem de coleta associada ao CT-e rodoviario (rodo/occ).</summary>
public sealed record OrdemColeta(
    string? Serie,
    string? Numero,
    DateOnly? DataEmissao,
    string? CnpjEmitente,
    string? CodigoInterno,
    string? InscricaoEstadual,
    string? Uf,
    string? Fone);

/// <summary>Aereo: minuta, conhecimento aereo, tarifa, natureza da carga e perigosos.</summary>
public sealed record DetalheAereo(
    string? NumeroMinuta,
    string? NumeroOperacional,
    DateOnly? DataPrevistaEntrega,
    string? Dimensao,
    IReadOnlyList<string> InformacoesManuseio,
    string? ClasseTarifa,
    string? CodigoTarifa,
    decimal? ValorTarifa,
    IReadOnlyList<ArtigoPerigoso> ArtigosPerigosos) : DetalheModal;

/// <summary>
/// Artigo perigoso (aereo/peri). O manual do DACTE (2.21.4) manda imprimir o
/// quadro "INFORMACOES SOBRE OS ARTIGOS PERIGOSOS" sempre que houver.
/// </summary>
public sealed record ArtigoPerigoso(
    string? NumeroOnu,
    string? QuantidadeVolumes,
    decimal? QuantidadeTotal,
    string? Unidade);

/// <summary>Aquaviario: navio, AFRMM, balsas e conteineres.</summary>
public sealed record DetalheAquaviario(
    decimal? ValorBaseAfrmm,
    decimal? ValorAfrmm,
    string? Navio,
    IReadOnlyList<string> Balsas,
    string? NumeroViagem,
    string? Direcao,
    string? Irin,
    string? TipoNavegacao,
    IReadOnlyList<Conteiner> Conteineres) : DetalheModal;

/// <summary>
/// Conteiner do modal aquaviario (aquav/detCont), com os lacres e os
/// documentos que carrega - o manual (2.21.4) admite trocar o quadro de
/// documentos originarios por este.
/// </summary>
public sealed record Conteiner(
    string? Numero,
    IReadOnlyList<string> Lacres,
    IReadOnlyList<DocumentoReferenciado> Documentos);

/// <summary>Ferroviario: trafego, fluxo, faturamento e ferrovias envolvidas.</summary>
public sealed record DetalheFerroviario(
    string? TipoTrafego,
    string? Fluxo,
    string? ResponsavelFaturamento,
    string? FerroviaEmitente,
    decimal? ValorFrete,
    string? ChaveCteFerroviaOrigem,
    IReadOnlyList<FerroviaEnvolvida> Ferrovias) : DetalheModal;

/// <summary>Ferrovia envolvida no trafego mutuo (ferrov/trafMut/ferroEnv).</summary>
public sealed record FerroviaEnvolvida(
    string? Cnpj,
    string? CodigoInterno,
    string? InscricaoEstadual,
    string? RazaoSocial);

/// <summary>Dutoviario: tarifa, periodo e pontos do servico.</summary>
public sealed record DetalheDutoviario(
    decimal? ValorTarifa,
    DateOnly? DataInicio,
    DateOnly? DataFim,
    string? Classificacao,
    string? TipoContratacao,
    string? PontoEntrada,
    string? PontoSaida,
    string? Contrato) : DetalheModal;

/// <summary>Multimodal: certificado do operador, negociabilidade e seguro.</summary>
public sealed record DetalheMultimodal(
    string? Cotm,
    string? Negociavel,
    string? Seguradora,
    string? CnpjSeguradora,
    string? Apolice,
    string? Averbacao) : DetalheModal;
