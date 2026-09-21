namespace FiscalDoc.Core.Model.Nfe;

/// <summary>Grupo ICMSTot (W02): o quadro CALCULO DO IMPOSTO do DANFE.</summary>
public sealed record TotaisIcms(
    decimal? BaseCalculoIcms,
    decimal? ValorIcms,
    decimal? BaseCalculoIcmsSt,
    decimal? ValorIcmsSt,
    decimal? ValorTotalProdutos,
    decimal? ValorFrete,
    decimal? ValorSeguro,
    decimal? ValorDesconto,
    decimal? OutrasDespesas,
    decimal? ValorIpi,
    decimal? ValorPis,
    decimal? ValorCofins,
    decimal? ValorIi,
    decimal? ValorIcmsDesonerado,
    decimal? ValorFcp,
    decimal? ValorFcpSt,
    decimal? ValorIpiDevolvido,
    decimal? ValorTotalTributos,
    decimal? ValorTotalNota);

/// <summary>Grupo ISSQNtot (W17): quadro CALCULO DO ISSQN, opcional.</summary>
public sealed record TotaisIssqn(
    decimal? ValorTotalServicos,
    decimal? BaseCalculo,
    decimal? ValorIssqn);

/// <summary>
/// Totais da Reforma Tributaria.
///
/// <para>Os tres primeiros grupos vem de <c>total/IBSCBSTot</c>, mas os dois
/// ultimos campos moram <b>fora</b> dele, como irmaos de ICMSTot no elemento
/// <c>total</c>: <c>ISTot/vIS</c> e <c>vNFTot</c>. Estao reunidos aqui porque
/// pertencem ao mesmo assunto - este e o modelo do FiscalDoc, nao o formato do
/// XML.</para>
///
/// <para>Repare que o grupo do IBS se chama <c>gIBS</c>, sem sufixo "Tot",
/// dentro de <c>IBSCBSTot</c>. E uma armadilha de nome conhecida.</para>
/// </summary>
public sealed record TotaisIbsCbs(
    decimal? BaseCalculo,
    decimal? ValorIbs,
    decimal? ValorIbsUf,
    decimal? ValorIbsMun,
    decimal? ValorCbs,
    decimal? ValorIs,
    decimal? ValorTotalNotaComTributos)
{
    /// <summary>
    /// Imposto Seletivo, que so existe em operacao sujeita a ele.
    /// Tambem e cobrado "por fora".
    /// </summary>
    public bool TemImpostoSeletivo => ValorIs is > 0m;

    /// <summary>
    /// <c>vNFTot</c> e o total da NF-e <b>com</b> IBS, CBS e IS - que sao
    /// tributos "por fora" e, portanto, somam ao valor da nota.
    ///
    /// <para>Em 2026 ele vem igual a <c>vNF</c> em todo documento real, porque
    /// o art. 348 da LC 214/2025 dispensa o recolhimento no ano de teste. Vale
    /// exibi-lo somente quando de fato divergir: repetir o mesmo numero em dois
    /// campos seria ruido, e escondê-lo quando divergir seria omitir o valor
    /// que o destinatario realmente deve.</para>
    /// </summary>
    public bool TotalDivergeDoValorDaNota(decimal? valorNota) =>
        ValorTotalNotaComTributos is { } t
        && valorNota is { } v
        && Math.Abs(t - v) >= 0.01m;
}

/// <summary>Grupo cobr (Y): FATURA / DUPLICATAS.</summary>
public sealed record Fatura(
    string? Numero,
    decimal? ValorOriginal,
    decimal? ValorDesconto,
    decimal? ValorLiquido);

public sealed record Duplicata(
    string? Numero,
    DateOnly? Vencimento,
    decimal? Valor);

/// <summary>Grupo infAdic (Z): DADOS ADICIONAIS.</summary>
public sealed record InformacoesAdicionais(
    string? FiscoInteresse,
    string? Complementares);

/// <summary>
/// Grupo protNFe/infProt. Ausente em documento nao autorizado - e o MOC proibe
/// sintetizar protocolo, entao o campo e nulo e o layout imprime o que a
/// modalidade de emissao manda no lugar.
/// </summary>
public sealed record Protocolo(
    string? Numero,
    DateTimeOffset? DataHoraRecebimento,
    string? CodigoStatus,
    string? Motivo,
    string? ChaveConfirmada);
