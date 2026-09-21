using System.Printing;
using FiscalDoc.Layout.DisplayList;

namespace FiscalDoc.Tests;

/// <summary>
/// A escolha de papel e de orientacao, presa por teste.
///
/// Era logica pura sem cobertura nenhuma, e foi onde morava o pior defeito
/// que a revisao encontrou: o documento em paisagem pede 297 x 210 mm, o
/// driver so lista papel em retrato, nenhuma A4 satisfazia 297 mm de largura,
/// e <b>todo DACTE e todo DANFE paisagem</b> caiam no papel padrao do driver,
/// em pe, reduzidos a cerca de 70% com a coluna dos totais fora da folha.
/// </summary>
public sealed class EscolhaDePapelTests
{
    private const double UnidadesPorMm = 96.0 / 25.4;

    private static PageMediaSize Mm(double larguraMm, double alturaMm) =>
        new(larguraMm * UnidadesPorMm, alturaMm * UnidadesPorMm);

    private static PageMediaSize A4 => new(PageMediaSizeName.ISOA4, 210.06 * UnidadesPorMm, 296.93 * UnidadesPorMm);

    private static PageMediaSize A3 => new(PageMediaSizeName.ISOA3, 297.0 * UnidadesPorMm, 420.0 * UnidadesPorMm);

    /// <summary>Uma laser de escritorio comum.</summary>
    private static PageMediaSize[] Laser => [A4, A3, Mm(215.9, 279.4), Mm(215.9, 355.6)];

    /// <summary>
    /// O caso que estava quebrado. A folha deitada tem de ser procurada com as
    /// medidas <b>giradas</b>, porque a rotacao vem da orientacao e nao do
    /// papel - e ai a A4 serve.
    /// </summary>
    [Fact]
    public void Documento_em_paisagem_escolhe_A4_e_nao_A3()
    {
        var deitado = new TamanhoPapel(297f, 210f);

        // Como o codigo faz: gira antes de procurar.
        var girado = new TamanhoPapel(deitado.AlturaMm, deitado.LarguraMm);

        PageMediaSize? escolhido = FiscalDoc.App.Impressao.EscolherMidia(Laser, girado);

        Assert.NotNull(escolhido);
        Assert.Equal(PageMediaSizeName.ISOA4, escolhido!.PageMediaSizeName);
    }

    /// <summary>
    /// Sem girar - que era o comportamento antigo - a A4 nao serve e a busca
    /// cai na A3, desperdicando metade da folha. Este teste existe para
    /// documentar o erro, nao para permiti-lo.
    /// </summary>
    [Fact]
    public void Sem_girar_a_busca_erra_e_cai_na_A3()
    {
        var deitadoSemGirar = new TamanhoPapel(297f, 210f);

        PageMediaSize? escolhido =
            FiscalDoc.App.Impressao.EscolherMidia(Laser, deitadoSemGirar);

        Assert.NotNull(escolhido);
        Assert.NotEqual(PageMediaSizeName.ISOA4, escolhido!.PageMediaSizeName);
    }

    [Fact]
    public void Danfe_retrato_escolhe_A4()
    {
        PageMediaSize? escolhido =
            FiscalDoc.App.Impressao.EscolherMidia(Laser, TamanhoPapel.A4);

        Assert.NotNull(escolhido);
        Assert.Equal(PageMediaSizeName.ISOA4, escolhido!.PageMediaSizeName);
    }

    /// <summary>
    /// Numa termica de balcao a bobina esta na lista, e ela ganha da A4 por
    /// ter a mesma largura - o cupom sai 1:1, sem margem lateral inventada.
    /// </summary>
    [Fact]
    public void Cupom_numa_termica_escolhe_a_bobina_e_nao_a_A4()
    {
        PageMediaSize bobina = Mm(80, 3276);
        PageMediaSize[] termica = [bobina, A4];

        var cupom = new TamanhoPapel(80f, 180f);

        PageMediaSize? escolhido = FiscalDoc.App.Impressao.EscolherMidia(termica, cupom);

        Assert.NotNull(escolhido);
        Assert.Equal(bobina.Width, escolhido!.Width);
    }

    /// <summary>
    /// Numa laser, o mesmo cupom nao acha bobina nenhuma e cai na A4 - onde
    /// sai 1:1 no alto da folha, que e o certo. O degrau do "menor que cabe"
    /// nao pode passar na frente da A4 e mandar o trabalho para um envelope.
    /// </summary>
    [Fact]
    public void Cupom_numa_laser_cai_na_A4()
    {
        PageMediaSize[] comEnvelope = [A4, A3, Mm(110, 220)];

        var cupom = new TamanhoPapel(80f, 180f);

        PageMediaSize? escolhido = FiscalDoc.App.Impressao.EscolherMidia(comEnvelope, cupom);

        Assert.NotNull(escolhido);
        Assert.Equal(PageMediaSizeName.ISOA4, escolhido!.PageMediaSizeName);
    }

    /// <summary>
    /// Driver sem papel nenhum, ou so com papel pequeno demais: devolve nulo e
    /// o chamador mantem o papel do proprio driver. Nao pode lancar.
    /// </summary>
    [Fact]
    public void Lista_vazia_ou_insuficiente_devolve_nulo()
    {
        Assert.Null(FiscalDoc.App.Impressao.EscolherMidia([], TamanhoPapel.A4));

        PageMediaSize[] soPequenos = [Mm(105, 148), Mm(74, 105)];
        Assert.Null(FiscalDoc.App.Impressao.EscolherMidia(soPequenos, TamanhoPapel.A4));
    }

    /// <summary>
    /// Papel com dimensao ausente ou zerada - alguns drivers virtuais fazem
    /// isso - e ignorado em vez de derrubar a escolha.
    /// </summary>
    [Fact]
    public void Papel_com_dimensao_invalida_e_ignorado()
    {
        PageMediaSize[] comLixo =
        [
            new PageMediaSize(PageMediaSizeName.Unknown),
            Mm(0, 0),
            A4,
        ];

        PageMediaSize? escolhido = FiscalDoc.App.Impressao.EscolherMidia(comLixo, TamanhoPapel.A4);

        Assert.NotNull(escolhido);
        Assert.Equal(PageMediaSizeName.ISOA4, escolhido!.PageMediaSizeName);
    }
}
