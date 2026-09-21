using System.Drawing;
using System.Drawing.Imaging;
using FiscalDoc.Layout.DisplayList;
using FiscalDoc.Render.Wpf;

namespace FiscalDoc.Tests;

/// <summary>
/// Verificacao de que a previa de tela rasteriza com nitidez.
///
/// A geometria ja e garantida por <see cref="RenderizacaoMmTests"/>: um
/// milimetro do layout e um milimetro no dispositivo. O que se prova aqui e a
/// outra metade - que essa geometria correta chega ao monitor <b>legivel</b>.
///
/// <para>A densidade usada e a do caso ruim de verdade: um A4 em "ajustar
/// pagina" numa tela de 1440 linhas cabe em cerca de 4,4 pixels por
/// milimetro. E ai que o fio de 0,15 mm da moldura e o rotulo de 5 pt do MOC
/// 3.7 vivem ou morrem.</para>
/// </summary>
public sealed class NitidezTelaTests
{
    /// <summary>A4 em "ajustar pagina" numa tela de 1440 linhas.</summary>
    private const double PxPorMmAjustarPagina = 4.38;

    /// <summary>
    /// Renderiza pelo caminho de tela do aplicativo: <see cref="RenderizadorWpf"/>
    /// escrevendo direto nos bytes de um bitmap pre-multiplicado, exatamente
    /// como o <c>PaginaView</c> faz.
    /// </summary>
    private static Bitmap Renderizar(
        double pxPorMm, float larguraMm, float alturaMm, params Primitiva[] primitivas)
    {
        int w = Math.Max(1, (int)Math.Round(larguraMm * pxPorMm));
        int h = Math.Max(1, (int)Math.Round(alturaMm * pxPorMm));

        var bmp = new Bitmap(w, h, PixelFormat.Format32bppPArgb);

        BitmapData trava = bmp.LockBits(
            new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, bmp.PixelFormat);

        try
        {
            new RenderizadorWpf().Desenhar(
                new Pagina(1, primitivas), pxPorMm, w, h, trava.Scan0, trava.Stride);
        }
        finally
        {
            bmp.UnlockBits(trava);
        }

        return bmp;
    }

    private static int ContarTinta(Bitmap bmp, int limiar)
    {
        int n = 0;

        for (int y = 0; y < bmp.Height; y++)
        {
            for (int x = 0; x < bmp.Width; x++)
            {
                if (bmp.GetPixel(x, y).R < limiar)
                {
                    n++;
                }
            }
        }

        return n;
    }

    /// <summary>
    /// O fio da moldura do DANFE tem 0,15 mm. A 4,4 px/mm isso e meio pixel, e
    /// a borda quase nunca cai numa fronteira inteira de pixel. Sem encaixe na
    /// grade, cada fio vira duas fileiras de cinza - e um DANFE e quase todo
    /// moldura, entao a folha inteira parece desfocada.
    ///
    /// <para>Na tela o fio tem de sair com um numero <b>inteiro</b> de
    /// fileiras - a espessura arredondada para pixel cheio - e <b>todas</b>
    /// pretas. Nenhuma franja de meio-tom, em nenhum zoom.</para>
    /// </summary>
    [Theory]
    [InlineData(PxPorMmAjustarPagina)]
    [InlineData(3.78)]
    [InlineData(5.24)]
    [InlineData(9.45)]
    [InlineData(18.9)]
    public void Fio_da_moldura_sai_em_pixels_cheios_e_pretos(double pxPorMm)
    {
        var caixa = new RetanguloMm(5f, 5f, 30f, 20f);

        using Bitmap bmp = Renderizar(
            pxPorMm, 40f, 30f, new Primitiva.Contorno(caixa, 0.15f));

        // Coluna no meio do lado de cima, longe dos cantos. So a metade
        // superior, para nao pegar tambem o fio de baixo.
        int x = bmp.Width / 2;

        var fileiras = new List<(int Y, int Tom)>();

        for (int y = 0; y < bmp.Height / 2; y++)
        {
            int tom = bmp.GetPixel(x, y).R;
            if (tom < 250)
            {
                fileiras.Add((y, tom));
            }
        }

        // 0,15 mm arredondado para pixel cheio, nunca menos que um.
        int esperado = Math.Max(1, (int)Math.Round(0.15 * pxPorMm));

        Assert.Equal(esperado, fileiras.Count);
        Assert.All(fileiras, f => Assert.Equal(0, f.Tom));

        // E contiguas: um fio com buraco no meio nao e um fio.
        for (int i = 1; i < fileiras.Count; i++)
        {
            Assert.Equal(fileiras[i - 1].Y + 1, fileiras[i].Y);
        }
    }

    /// <summary>
    /// O mesmo para o fio vertical: o encaixe tem de valer nos dois eixos, ou
    /// as colunas do quadro de produtos saem em cinza enquanto as linhas saem
    /// pretas.
    /// </summary>
    [Fact]
    public void Fio_vertical_tambem_sai_em_pixel_cheio_e_preto()
    {
        using Bitmap bmp = Renderizar(
            PxPorMmAjustarPagina, 40f, 30f,
            new Primitiva.Linha(new PontoMm(17.3f, 4f), new PontoMm(17.3f, 26f), 0.15f));

        int y = bmp.Height / 2;

        var colunas = new List<(int X, int Tom)>();

        for (int x = 0; x < bmp.Width; x++)
        {
            int tom = bmp.GetPixel(x, y).R;
            if (tom < 250)
            {
                colunas.Add((x, tom));
            }
        }

        Assert.Single(colunas);
        Assert.Equal(0, colunas[0].Tom);
    }

    /// <summary>
    /// Um rotulo de 5 pt numa caixa justa - o caso de "CHAVE DE ACESSO" no
    /// DANFE - nao pode perder a base das letras para o recorte. A caixa justa
    /// tem de desenhar a mesma tinta que uma caixa folgada.
    /// </summary>
    [Fact]
    public void Rotulo_de_cinco_pontos_nao_perde_a_base_na_caixa_justa()
    {
        // Precisa de DESCENDENTE. Com "CHAVE DE ACESSO" - tudo maiuscula -
        // a tinta para na linha de base e o teste passava mesmo sem folga
        // nenhuma no recorte, que e justamente o que ele existe para prender.
        const string Rotulo = "Chave de Acesso (pág.)";
        var estilo = new EstiloTexto(EstiloTexto.FamiliaPadrao, 5f);

        // 1,76 mm de em: a caixa justa tem a altura de uma linha e nada mais.
        var justa = new RetanguloMm(1f, 1f, 30f, 2.03f);
        var folgada = new RetanguloMm(1f, 1f, 30f, 6f);

        using Bitmap comJusta = Renderizar(
            PxPorMmAjustarPagina, 34f, 10f, new Primitiva.Texto(Rotulo, justa, estilo));

        using Bitmap comFolgada = Renderizar(
            PxPorMmAjustarPagina, 34f, 10f, new Primitiva.Texto(Rotulo, folgada, estilo));

        int naJusta = ContarTinta(comJusta, 128);
        int naFolgada = ContarTinta(comFolgada, 128);

        Assert.True(naJusta > 0, "o rotulo nao foi desenhado");
        Assert.Equal(naFolgada, naJusta);
    }

    /// <summary>
    /// A folga do recorte na tela e de um pixel, e nao mais: existe para
    /// absorver o arredondamento do rasterizador, nao para relaxar o recorte.
    /// Um campo comprido continua sem poder invadir o vizinho - o que num
    /// DANFE seria pior do que faltar informacao.
    /// </summary>
    [Fact]
    public void Texto_comprido_continua_recortado_pela_caixa()
    {
        var caixa = new RetanguloMm(5f, 5f, 20f, 6f);

        using Bitmap bmp = Renderizar(
            PxPorMmAjustarPagina, 60f, 16f,
            new Primitiva.Texto(
                new string('M', 200), caixa, new EstiloTexto(EstiloTexto.FamiliaPadrao, 10f)));

        // Um pixel de folga e tolerado; dois ja seriam invasao.
        int limite = (int)Math.Ceiling(caixa.Direita * PxPorMmAjustarPagina) + 1;

        for (int x = limite; x < bmp.Width; x++)
        {
            for (int y = 0; y < bmp.Height; y++)
            {
                Assert.True(
                    bmp.GetPixel(x, y).R > 200,
                    $"texto vazou para {x / PxPorMmAjustarPagina:0.00} mm, "
                    + $"fora da caixa que termina em {caixa.Direita:0.00} mm");
            }
        }
    }

    /// <summary>
    /// O papel nao encaixa na grade. O mesmo renderizador desenha os dois,
    /// mas no modo papel a geometria vai em milimetro exato: a 600 dpi o fio
    /// de 0,15 mm continua medindo 0,15 mm - e nao uma fileira encaixada, que
    /// e escolha de tela e so de tela.
    /// </summary>
    [Fact]
    public void Papel_mantem_a_espessura_normativa_do_fio()
    {
        const float Dpi = 600f;

        Bitmap bmp = RenderTeste.Papel(
            new Pagina(1, [new Primitiva.Contorno(new RetanguloMm(5f, 5f, 30f, 20f), 0.15f)]),
            new TamanhoPapel(40f, 30f),
            Dpi);

        using (bmp)
        {
            int x = bmp.Width / 2;
            int fileiras = 0;

            for (int y = 0; y < bmp.Height / 2; y++)
            {
                if (bmp.GetPixel(x, y).R < 250)
                {
                    fileiras++;
                }
            }

            // 0,15 mm a 600 dpi = 3,54 px. Com as bordas suavizadas, 4 ou 5
            // fileiras marcadas; o que nao pode e virar 1 (encaixe de tela) ou
            // 0 (fio sumido).
            Assert.InRange(fileiras, 3, 6);
        }
    }
}
