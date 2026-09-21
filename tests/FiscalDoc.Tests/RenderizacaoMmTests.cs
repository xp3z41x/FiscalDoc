using System.Drawing;
using System.Drawing.Imaging;
using FiscalDoc.Layout;
using FiscalDoc.Layout.DisplayList;
using FiscalDoc.Render.Wpf;

namespace FiscalDoc.Tests;

/// <summary>
/// Verificacao da Fase 2: um milimetro no layout e um milimetro no
/// dispositivo.
///
/// A prova e feita contra um bitmap de resolucao conhecida, o que e mais
/// rigoroso do que medir papel com regua: a 600 dpi, um milimetro sao
/// 23,62 pixels, e o teste consegue afirmar a posicao de cada borda com
/// tolerancia de um pixel. Se esta cadeia estiver certa aqui, esta certa na
/// impressora - e o mesmo codigo, com o mesmo PageUnit.
/// </summary>
public sealed class RenderizacaoMmTests
{
    private const float Dpi = 600f;
    private const float PxPorMm = Dpi / 25.4f;

    private static int Px(float mm) => (int)MathF.Round(mm * PxPorMm);

    /// <summary>Desenha primitivas num bitmap de A4 a 600 dpi.</summary>
    private static Bitmap Renderizar(params Primitiva[] primitivas)
    {
        TamanhoPapel a4 = TamanhoPapel.A4;
        // Exatamente o que a impressora recebe - o modo papel do renderizador -
        // revelado a 600 dpi para que o teste possa medir cada borda.
        return RenderTeste.Papel(new Pagina(1, primitivas), a4, Dpi);
    }

    private static bool EhEscuro(Color c) => c.R < 128 && c.G < 128 && c.B < 128;

    [Fact]
    public void Retangulo_preenchido_cai_exatamente_onde_o_layout_mandou()
    {
        // 50 x 30 mm comecando em (40, 60).
        var caixa = new RetanguloMm(40f, 60f, 50f, 30f);

        using Bitmap bmp = Renderizar(new Primitiva.Preenchimento(caixa, Tinta.Preto));

        // Dentro, bem longe das bordas: escuro.
        Assert.True(EhEscuro(bmp.GetPixel(Px(65f), Px(75f))), "o centro deveria estar preenchido");

        // Fora, com 1 mm de folga: branco.
        Assert.False(EhEscuro(bmp.GetPixel(Px(39f), Px(75f))), "1 mm a esquerda deveria estar limpo");
        Assert.False(EhEscuro(bmp.GetPixel(Px(91f), Px(75f))), "1 mm a direita deveria estar limpo");
        Assert.False(EhEscuro(bmp.GetPixel(Px(65f), Px(59f))), "1 mm acima deveria estar limpo");
        Assert.False(EhEscuro(bmp.GetPixel(Px(65f), Px(91f))), "1 mm abaixo deveria estar limpo");
    }

    [Fact]
    public void Cem_milimetros_desenhados_medem_cem_milimetros_no_dispositivo()
    {
        // O teste que a pagina de calibracao faz no papel, feito aqui em
        // pixels: uma barra de exatamente 100 mm.
        var barra = new RetanguloMm(50f, 100f, 100f, 4f);

        using Bitmap bmp = Renderizar(new Primitiva.Preenchimento(barra, Tinta.Preto));

        int y = Px(102f);
        int primeiro = -1;
        int ultimo = -1;

        for (int x = 0; x < bmp.Width; x++)
        {
            if (!EhEscuro(bmp.GetPixel(x, y)))
            {
                continue;
            }

            if (primeiro < 0)
            {
                primeiro = x;
            }

            ultimo = x;
        }

        Assert.True(primeiro >= 0, "a barra nao foi desenhada");

        int larguraPx = ultimo - primeiro + 1;
        int esperadoPx = Px(100f); // 2362 px a 600 dpi

        // Tolerancia de 2 px = 0,085 mm. Bem abaixo do que qualquer regua le.
        Assert.InRange(larguraPx, esperadoPx - 2, esperadoPx + 2);

        // E a posicao de inicio tambem tem de bater.
        Assert.InRange(primeiro, Px(50f) - 2, Px(50f) + 2);
    }

    [Fact]
    public void Quadrado_sai_quadrado_nos_dois_eixos()
    {
        // Se o DPI horizontal e o vertical divergissem, isto viraria retangulo.
        var quadrado = new RetanguloMm(30f, 30f, 60f, 60f);

        using Bitmap bmp = Renderizar(new Primitiva.Preenchimento(quadrado, Tinta.Preto));

        int larguraPx = ContarEscurosNaLinha(bmp, Px(60f));
        int alturaPx = ContarEscurosNaColuna(bmp, Px(60f));

        Assert.InRange(larguraPx, alturaPx - 2, alturaPx + 2);
        Assert.InRange(larguraPx, Px(60f) - 2, Px(60f) + 2);
    }

    [Fact]
    public void Texto_e_recortado_pela_caixa_e_nao_invade_o_campo_vizinho()
    {
        // Um campo do DANFE que transborda para cima do vizinho e pior do que
        // um campo truncado: passa a mentir sobre o valor do outro campo.
        var caixa = new RetanguloMm(20f, 50f, 20f, 6f);

        using Bitmap bmp = Renderizar(new Primitiva.Texto(
            new string('M', 200),
            caixa,
            new EstiloTexto(EstiloTexto.FamiliaPadrao, 10f)));

        // Logo a direita da caixa, dentro da faixa vertical do texto: limpo.
        for (int x = Px(41f); x < Px(60f); x++)
        {
            for (int y = Px(50f); y < Px(56f); y++)
            {
                Assert.False(
                    EhEscuro(bmp.GetPixel(x, y)),
                    $"texto vazou para {x / PxPorMm:0.0} mm, fora da caixa que termina em 40 mm");
            }
        }
    }

    [Fact]
    public void Texto_em_pontos_do_moc_tem_altura_fisica_esperada()
    {
        // O MOC especifica minimos em pontos tipograficos. 1 pt = 1/72 pol,
        // entao 10 pt = 3,53 mm de altura de em. A altura desenhada de uma
        // maiuscula fica em torno de 70% disso; o teste garante a ordem de
        // grandeza certa, provando que a fonte nao foi criada na unidade errada
        // (o erro classico faria o texto sair varias vezes maior ou menor).
        using Bitmap bmp = Renderizar(new Primitiva.Texto(
            "H",
            new RetanguloMm(20f, 50f, 40f, 20f),
            new EstiloTexto(EstiloTexto.FamiliaPadrao, 10f)));

        int topo = -1;
        int baixo = -1;

        for (int y = Px(45f); y < Px(75f); y++)
        {
            bool temEscuro = false;
            for (int x = Px(19f); x < Px(60f); x++)
            {
                if (EhEscuro(bmp.GetPixel(x, y)))
                {
                    temEscuro = true;
                    break;
                }
            }

            if (!temEscuro)
            {
                continue;
            }

            if (topo < 0)
            {
                topo = y;
            }

            baixo = y;
        }

        Assert.True(topo >= 0, "o texto nao foi desenhado");

        float alturaMm = (baixo - topo + 1) / PxPorMm;

        // Altura da caixa do "H" em Times New Roman 10 pt: ~2,4 mm.
        // A faixa e generosa de proposito - o que se testa e a unidade, nao a
        // metrica exata da familia.
        Assert.InRange(alturaMm, 1.8f, 3.2f);
    }

    [Fact]
    public void Pagina_de_calibracao_desenha_sem_erro_e_cabe_no_papel()
    {
        ConjuntoPaginas c = PaginaCalibracao.Construir(TamanhoPapel.A4);

        Assert.Equal(1, c.Total);
        Assert.NotEmpty(c.Paginas[0].Primitivas);

        using Bitmap bmp = Renderizar([.. c.Paginas[0].Primitivas]);

        // A moldura fica a 10 mm da borda; a 5 mm nao pode haver nada.
        for (int x = 0; x < bmp.Width; x += 50)
        {
            Assert.False(EhEscuro(bmp.GetPixel(x, Px(5f))), "algo foi desenhado na margem superior");
        }

        // E a moldura tem de existir de fato a 10 mm.
        Assert.True(
            EhEscuro(bmp.GetPixel(Px(105f), Px(10f)))
            || EhEscuro(bmp.GetPixel(Px(105f), Px(10f) + 1))
            || EhEscuro(bmp.GetPixel(Px(105f), Px(10f) - 1)),
            "a moldura de 10 mm nao foi encontrada");
    }

    [Fact]
    public void Rotacao_de_noventa_graus_troca_largura_por_altura()
    {
        // O canhoto e as faixas de titulo do DANFE em paisagem dependem disso.
        var barra = new RetanguloMm(100f, 100f, 40f, 4f);

        using Bitmap bmp = Renderizar(new Primitiva.Rotacionado(
            90f,
            new PontoMm(100f, 100f),
            [new Primitiva.Preenchimento(barra, Tinta.Preto)]));

        // Depois de girar 90 graus em torno de (100,100), a barra que ia para
        // a direita passa a descer, e a espessura que ia para baixo passa a
        // ir para a esquerda: o retangulo ocupa x de 96 a 100 e y de 100 a 140.
        (int x0, int y0, int x1, int y1) = CaixaDosEscuros(bmp);

        Assert.InRange(x0 / PxPorMm, 95.5f, 96.5f);
        Assert.InRange(x1 / PxPorMm, 99.5f, 100.5f);
        Assert.InRange(y0 / PxPorMm, 99.5f, 100.5f);
        Assert.InRange(y1 / PxPorMm, 139.5f, 140.5f);

        // E o comprimento continua sendo 40 mm, agora na vertical.
        Assert.InRange((y1 - y0 + 1) / PxPorMm, 39.5f, 40.5f);
    }

    /// <summary>Caixa delimitadora de tudo que foi desenhado em escuro.</summary>
    private static (int X0, int Y0, int X1, int Y1) CaixaDosEscuros(Bitmap bmp)
    {
        int x0 = int.MaxValue, y0 = int.MaxValue, x1 = -1, y1 = -1;

        for (int y = 0; y < bmp.Height; y++)
        {
            for (int x = 0; x < bmp.Width; x++)
            {
                if (!EhEscuro(bmp.GetPixel(x, y)))
                {
                    continue;
                }

                if (x < x0) { x0 = x; }
                if (y < y0) { y0 = y; }
                if (x > x1) { x1 = x; }
                if (y > y1) { y1 = y; }
            }
        }

        Assert.True(x1 >= 0, "nada foi desenhado");
        return (x0, y0, x1, y1);
    }

    private static int ContarEscurosNaLinha(Bitmap bmp, int y)
    {
        int n = 0;
        for (int x = 0; x < bmp.Width; x++)
        {
            if (EhEscuro(bmp.GetPixel(x, y)))
            {
                n++;
            }
        }

        return n;
    }

    private static int ContarEscurosNaColuna(Bitmap bmp, int x)
    {
        int n = 0;
        for (int y = 0; y < bmp.Height; y++)
        {
            if (EhEscuro(bmp.GetPixel(x, y)))
            {
                n++;
            }
        }

        return n;
    }
}
