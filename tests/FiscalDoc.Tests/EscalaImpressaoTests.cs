using FiscalDoc.Layout.DisplayList;
using FiscalDoc.Render.Wpf;

namespace FiscalDoc.Tests;

/// <summary>
/// O encaixe do documento na area imprimivel.
///
/// Era logica pura, publica e sem teste nenhum, carregando uma promessa do
/// CLAUDE.md: <b>1:1 sempre que a impressora permitir</b>, e menos apenas na
/// medida exata que aquele equipamento exige. E foi onde morava um defeito
/// que nenhum teste pegava: comparar so a borda DIREITA resolvia a direita e
/// piorava a esquerda, porque reduzir em torno da origem puxa o conteudo para
/// dentro da faixa que a impressora nao marca.
/// </summary>
public sealed class EscalaImpressaoTests
{
    /// <summary>DANFE retrato: o conteudo vai de 2,5 a 208,2 mm na horizontal.</summary>
    private static RetanguloMm DanfeRetrato =>
        RetanguloMm.DeCantos(2.5f, 4.2f, 208.2f, 294.0f);

    /// <summary>DANFE paisagem: o canhoto comeca a 1,3 mm da borda.</summary>
    private static RetanguloMm DanfePaisagem =>
        RetanguloMm.DeCantos(1.3f, 4.2f, 294.6f, 207.0f);

    /// <summary>Laser comum: 4,23 mm que a impressora nao marca, em toda volta.</summary>
    private static RetanguloMm LaserA4 => new(4.233f, 4.233f, 201.6f, 288.5f);

    /// <summary>Margem zero - PDF, algumas termicas.</summary>
    private static RetanguloMm MargemZeroA4 => new(0f, 0f, 210f, 297f);

    private static void AssertCabeInteiro(
        RetanguloMm conteudo, RetanguloMm imprimivel, AjusteImpressao a)
    {
        float esq = (conteudo.X * a.Escala) + a.DeslocamentoXMm;
        float topo = (conteudo.Y * a.Escala) + a.DeslocamentoYMm;
        float dir = (conteudo.Direita * a.Escala) + a.DeslocamentoXMm;
        float baixo = (conteudo.Base * a.Escala) + a.DeslocamentoYMm;

        const float Folga = 0.01f;

        Assert.True(esq >= imprimivel.X - Folga, $"borda esquerda em {esq:0.000} mm, antes de {imprimivel.X:0.000}");
        Assert.True(topo >= imprimivel.Y - Folga, $"borda de cima em {topo:0.000} mm, antes de {imprimivel.Y:0.000}");
        Assert.True(dir <= imprimivel.Direita + Folga, $"borda direita em {dir:0.000} mm, depois de {imprimivel.Direita:0.000}");
        Assert.True(baixo <= imprimivel.Base + Folga, $"borda de baixo em {baixo:0.000} mm, depois de {imprimivel.Base:0.000}");
    }

    /// <summary>
    /// Numa impressora de margem zero o documento sai exatamente onde o MOC
    /// manda: 1:1 e sem deslocamento nenhum.
    /// </summary>
    [Fact]
    public void Margem_zero_imprime_um_para_um_na_posicao_absoluta()
    {
        AjusteImpressao a = EscalaImpressao.Calcular(DanfeRetrato, MargemZeroA4);

        Assert.Equal(1f, a.Escala);
        Assert.Equal(0f, a.DeslocamentoXMm);
        Assert.Equal(0f, a.DeslocamentoYMm);
        Assert.False(a.Altera);
    }

    /// <summary>
    /// O defeito que motivou o teste. Numa laser comum o DANFE perdia 1,76 mm
    /// do lado esquerdo - a moldura dos nove quadros e o primeiro caractere de
    /// cada linha da coluna CODIGO - enquanto o aviso na tela dizia 98,9%.
    /// </summary>
    [Fact]
    public void Numa_laser_comum_o_documento_cabe_inteiro_e_nada_e_cortado()
    {
        AjusteImpressao a = EscalaImpressao.Calcular(DanfeRetrato, LaserA4);

        Assert.True(a.Escala < 1f, "deveria reduzir");
        Assert.True(a.DeslocamentoXMm > 0f, "deveria empurrar para dentro da area imprimivel");

        AssertCabeInteiro(DanfeRetrato, LaserA4, a);
    }

    /// <summary>
    /// O paisagem comeca a 1,3 mm da borda - ainda mais perto do que o
    /// retrato -, entao e o caso mais exposto.
    /// </summary>
    [Fact]
    public void Paisagem_tambem_cabe_inteiro()
    {
        var laserDeitada = new RetanguloMm(4.233f, 4.233f, 288.5f, 201.6f);

        AjusteImpressao a = EscalaImpressao.Calcular(DanfePaisagem, laserDeitada);

        AssertCabeInteiro(DanfePaisagem, laserDeitada, a);
    }

    /// <summary>
    /// Uma margem esquerda grande nao pode AUMENTAR a escala. Comparando so a
    /// borda direita, margem maior dava area util menor pela direita, escala
    /// maior, e mais conteudo cortado pela esquerda - o contrario do que a
    /// intuicao diz.
    /// </summary>
    [Theory]
    [InlineData(4.233f)]
    [InlineData(8.5f)]
    [InlineData(12.7f)]
    [InlineData(20f)]
    public void Margem_maior_nunca_aumenta_a_escala(float margemMm)
    {
        var imprimivel = new RetanguloMm(
            margemMm, margemMm, 210f - (2 * margemMm), 297f - (2 * margemMm));

        AjusteImpressao a = EscalaImpressao.Calcular(DanfeRetrato, imprimivel);

        AssertCabeInteiro(DanfeRetrato, imprimivel, a);
    }

    [Fact]
    public void Nunca_amplia_mesmo_com_papel_de_sobra()
    {
        var a3 = new RetanguloMm(5f, 5f, 287f, 410f);

        Assert.Equal(1f, EscalaImpressao.Calcular(DanfeRetrato, a3).Escala);
    }

    /// <summary>
    /// Driver reportando area absurda nao pode espelhar nem colapsar a folha.
    /// </summary>
    [Theory]
    [InlineData(0f, 0f)]
    [InlineData(-10f, 100f)]
    [InlineData(float.NaN, 297f)]
    public void Area_imprimivel_absurda_cai_em_um_para_um(float largura, float altura)
    {
        AjusteImpressao a = EscalaImpressao.Calcular(
            DanfeRetrato, new RetanguloMm(0f, 0f, largura, altura));

        Assert.Equal(1f, a.Escala);
        Assert.False(a.Altera);
    }

    [Fact]
    public void Conteudo_vazio_nao_altera_nada()
    {
        AjusteImpressao a = EscalaImpressao.Calcular(
            new RetanguloMm(0f, 0f, 0f, 0f), LaserA4);

        Assert.Equal(1f, a.Escala);
        Assert.False(a.Altera);
    }
}
