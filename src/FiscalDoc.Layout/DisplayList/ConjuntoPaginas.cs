namespace FiscalDoc.Layout.DisplayList;

/// <summary>Tamanho de papel em milimetros exatos.</summary>
public readonly record struct TamanhoPapel(float LarguraMm, float AlturaMm)
{
    /// <summary>
    /// A4 exato pela ISO 216. O driver reporta 827 x 1169 centesimos de
    /// polegada, que e 210,06 x 296,93 mm - arredondado. O layout usa o valor
    /// exato e deixa a conversao para o renderizador. Ver plano 2.3.
    /// </summary>
    public static readonly TamanhoPapel A4 = new(210f, 297f);

    public TamanhoPapel Girado => new(AlturaMm, LarguraMm);

    public RetanguloMm Folha => new(0, 0, LarguraMm, AlturaMm);
}

/// <summary>Uma pagina pronta para desenho.</summary>
public sealed record Pagina(int Numero, IReadOnlyList<Primitiva> Primitivas);

/// <summary>
/// O documento inteiro, paginado, em milimetros.
///
/// Repare que as paginas existem <b>todas</b> antes de qualquer pixel ser
/// desenhado. E isso que torna "FOLHA nn/nn" trivial: o total ja e conhecido
/// quando a pagina 1 e montada. Desenhar direto no canvas exigiria um passo de
/// medicao separado para descobrir o mesmo numero. Ver plano 2.1.
/// </summary>
public sealed record ConjuntoPaginas(
    IReadOnlyList<Pagina> Paginas,
    TamanhoPapel Papel,
    RetanguloMm ExtensaoUsada)
{
    public int Total => Paginas.Count;

    /// <summary>
    /// Conjunto de uma pagina so, para os casos que nunca paginam: pagina de
    /// calibracao, estado de erro, representacao de evento.
    /// </summary>
    public static ConjuntoPaginas UmaPagina(
        IReadOnlyList<Primitiva> primitivas,
        TamanhoPapel papel,
        RetanguloMm extensao) =>
        new([new Pagina(1, primitivas)], papel, extensao);
}
