namespace FiscalDoc.Core.Model.Nfe;

/// <summary>
/// Uma forma de pagamento (grupo YA, <c>detPag</c>).
///
/// O grupo existe desde o leiaute 4.00 tanto na NF-e quanto na NFC-e, mas so
/// o DANFE NFC-e tem quadro para ele: o Manual de Especificacoes Tecnicas do
/// DANFE NFC-e (v6.0) exige "Forma de Pagamento" e "Valor Pago" na divisao
/// III. O DANFE do modelo 55 nao tem esse quadro no Anexo II, entao os mesmos
/// dados sao lidos e simplesmente nao aparecem la.
/// </summary>
public sealed record Pagamento(
    string? Codigo,
    string? Descricao,
    decimal? Valor)
{
    /// <summary>
    /// Rotulo da forma de pagamento. Usa <c>xPag</c> quando o emissor
    /// informou a descricao dele (obrigatorio para tPag 99), e a tabela
    /// oficial caso contrario.
    /// </summary>
    public string Rotulo => !string.IsNullOrWhiteSpace(Descricao)
        ? Descricao!
        : Rotulos.FormaPagamento(Codigo);
}

/// <summary>
/// Grupo <c>pag</c> inteiro: as formas de pagamento e o troco.
///
/// O troco mora em <c>pag/vTroco</c>, irmao dos <c>detPag</c> e nao filho de
/// um deles - dai ele viver aqui, e nao em <see cref="Pagamento"/>.
/// </summary>
public sealed record Pagamentos(
    IReadOnlyList<Pagamento> Formas,
    decimal? Troco)
{
    public static Pagamentos Vazio { get; } = new([], null);
}

/// <summary>
/// Informacoes suplementares da NFC-e (grupo ZX, <c>infNFeSupl</c>).
///
/// Existe somente no modelo 65. Os dois campos sao obrigatorios la e dao ao
/// DANFE NFC-e as duas formas de consulta que o manual exige: o QR Code
/// (divisao V) e o endereco de consulta por chave de acesso (divisao IV).
/// </summary>
public sealed record InformacoesSuplementares(
    string? QrCode,
    string? UrlConsultaChave);
