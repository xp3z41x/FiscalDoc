using System.Drawing;
using FiscalDoc.Core.Model.Nfe;
using FiscalDoc.Core.Parsing;
using FiscalDoc.Layout.Barcode;
using FiscalDoc.Layout.Danfe;
using FiscalDoc.Layout.DisplayList;
using FiscalDoc.Render.Wpf;

namespace FiscalDoc.Tests;

/// <summary>
/// A trava dos dois modos do renderizador.
///
/// Existe um unico reprodutor da display list, e ele desenha de dois jeitos:
/// <b>tela</b>, em pixels encaixados na grade, e <b>papel</b>, em vetor com o
/// milimetro exato. O encaixe e uma escolha de tela - sem ele a folha parece
/// desfocada a 4 px/mm - mas ele nao pode <b>mover</b> nada: a geometria que
/// aparece na previa tem de ser a que sai na impressora.
///
/// <para>A exigencia e que toda tinta de um modo encontre tinta do outro a no
/// maximo um pixel. Um pixel e a folga do encaixe; qualquer deslocamento maior
/// significa que a previa esta mentindo sobre o papel.</para>
/// </summary>
public sealed class ParidadeTelaPapelTests
{
    /// <summary>A4 em "ajustar pagina" numa tela de 1440 linhas: 4,38 px/mm.</summary>
    private const double PxPorMm = 4.38;

    /// <summary>
    /// A mesma densidade, expressa como resolucao - e o que o modo papel
    /// recebe, ja que la nao existe pixel ate o RIP rasterizar.
    /// </summary>
    private const double DpiEquivalente = PxPorMm * 25.4;

    private static bool[,] Mascara(Bitmap bmp)
    {
        var m = new bool[bmp.Width, bmp.Height];

        for (int y = 0; y < bmp.Height; y++)
        {
            for (int x = 0; x < bmp.Width; x++)
            {
                m[x, y] = bmp.GetPixel(x, y).R < 200;
            }
        }

        return m;
    }

    private static int ContarTinta(bool[,] m)
    {
        int n = 0;

        foreach (bool b in m)
        {
            if (b)
            {
                n++;
            }
        }

        return n;
    }

    private static (int SoNoA, int SoNoB) Divergencia(bool[,] a, bool[,] b)
    {
        int larg = Math.Min(a.GetLength(0), b.GetLength(0));
        int alt = Math.Min(a.GetLength(1), b.GetLength(1));

        return (Contar(a, b, larg, alt), Contar(b, a, larg, alt));

        static int Contar(bool[,] origem, bool[,] outro, int larg, int alt)
        {
            int n = 0;

            for (int y = 0; y < alt; y++)
            {
                for (int x = 0; x < larg; x++)
                {
                    if (origem[x, y] && !TemVizinho(outro, x, y, larg, alt))
                    {
                        n++;
                    }
                }
            }

            return n;
        }

        static bool TemVizinho(bool[,] m, int x, int y, int larg, int alt)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int vx = x + dx;
                    int vy = y + dy;

                    if (vx >= 0 && vy >= 0 && vx < larg && vy < alt && m[vx, vy])
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }

    /// <summary>Remove o texto, recursivamente. Sobra so a geometria.</summary>
    private static IReadOnlyList<Primitiva> SemTexto(IReadOnlyList<Primitiva> lista)
    {
        var saida = new List<Primitiva>();

        foreach (Primitiva p in lista)
        {
            switch (p)
            {
                case Primitiva.Texto:
                    break;

                case Primitiva.Rotacionado r:
                    saida.Add(r with { Filhos = SemTexto(r.Filhos) });
                    break;

                default:
                    saida.Add(p);
                    break;
            }
        }

        return saida;
    }

    private static NfeDocumento Ler(string caminho)
    {
        var ok = Assert.IsType<ResultadoLeitura.Ok>(LeitorDocumento.Ler(caminho));
        return Assert.IsType<NfeDocumento>(ok.Documento);
    }

    [TeoriaComCorpusReal]
    [InlineData("retrato medio")]
    [InlineData("retrato longo")]
    [InlineData("paisagem")]
    public void Geometria_do_danfe_e_a_mesma_na_tela_e_no_papel(string amostra)
    {
        var medidor = new MedidorTextoWpf();

        // A paisagem e pedida por caracteristica, e nao por nome de arquivo:
        // o nome do arquivo de uma NF-e e a chave de acesso, e este
        // repositorio e publico. Ver Amostras.RealNfePor.
        string caminho = amostra switch
        {
            "retrato medio" => Amostras.Real("RENASCENCA"),
            "retrato longo" => Amostras.Real("ZOUIL"),
            _ => Amostras.PaisagemComMaisItens(),
        };

        NfeDocumento nfe = Ler(caminho);

        ConjuntoPaginas c = nfe.Paisagem
            ? DanfePaisagem.Construir(nfe, medidor)
            : DanfeRetrato.Construir(nfe, medidor);

        var pagina = new Pagina(1, SemTexto(c.Paginas[0].Primitivas));

        using Bitmap tela = RenderTeste.Tela(pagina, c.Papel, PxPorMm);
        using Bitmap papel = RenderTeste.Papel(pagina, c.Papel, DpiEquivalente);

        bool[,] mascaraTela = Mascara(tela);
        bool[,] mascaraPapel = Mascara(papel);

        // Sem isto, dois bitmaps em BRANCO passariam: Divergencia devolve
        // (0,0) quando nao ha tinta em lugar nenhum, e um early-return no
        // renderizador viraria build verde.
        Assert.Equal(tela.Width, papel.Width);
        Assert.Equal(tela.Height, papel.Height);
        Assert.True(ContarTinta(mascaraTela) > 1000, "a tela nao desenhou nada");
        Assert.True(ContarTinta(mascaraPapel) > 1000, "o papel nao desenhou nada");

        (int soNaTela, int soNoPapel) = Divergencia(mascaraTela, mascaraPapel);

        Assert.True(
            soNaTela == 0 && soNoPapel == 0,
            $"a geometria divergiu entre os modos: {soNaTela} px so na tela, "
            + $"{soNoPapel} px so no papel (tolerancia: um pixel de vizinhanca)");
    }

    /// <summary>
    /// Pega quem acrescentar uma primitiva e tratar so um dos modos: a pagina
    /// traz uma de cada tipo, e os dois modos tem de desenhar todas.
    /// </summary>
    [Fact]
    public void Todos_os_tipos_de_primitiva_sao_desenhados_nos_dois_modos()
    {
        var papel = new TamanhoPapel(80f, 60f);

        Primitiva[] uma =
        [
            new Primitiva.Linha(new PontoMm(4, 6), new PontoMm(76, 6), 0.15f),
            new Primitiva.Contorno(new RetanguloMm(4, 10, 30, 10), 0.15f),
            new Primitiva.Preenchimento(new RetanguloMm(40, 10, 30, 10), Tinta.CinzaMedio),
            new Primitiva.Texto(
                "TEXTO", new RetanguloMm(4, 24, 30, 5),
                new EstiloTexto(EstiloTexto.FamiliaPadrao, 10f)),
            new Primitiva.CodigoBarras(
                new RetanguloMm(4, 32, 60, 8), Code128C.Codificar("3526094231641600"), 0.2f),
            new Primitiva.CodigoQr(
                new RetanguloMm(4, 42, 16, 16), QrCode.Codificar("https://www.fazenda.gov.br"), 4),
            new Primitiva.Rotacionado(
                90f, new PontoMm(60, 50),
                [new Primitiva.Preenchimento(new RetanguloMm(50, 48, 20, 4), Tinta.Preto)]),
        ];

        foreach (Primitiva p in uma)
        {
            var pagina = new Pagina(1, [p]);

            using Bitmap naTela = RenderTeste.Tela(pagina, papel, PxPorMm);
            using Bitmap noPapel = RenderTeste.Papel(pagina, papel, DpiEquivalente);

            Assert.True(ContarTinta(naTela) > 0, $"a tela nao desenhou {p.GetType().Name}");
            Assert.True(ContarTinta(noPapel) > 0, $"o papel nao desenhou {p.GetType().Name}");
        }

        static int ContarTinta(Bitmap bmp)
        {
            int n = 0;

            for (int y = 0; y < bmp.Height; y++)
            {
                for (int x = 0; x < bmp.Width; x++)
                {
                    if (bmp.GetPixel(x, y).R < 200)
                    {
                        n++;
                    }
                }
            }

            return n;
        }
    }
}
