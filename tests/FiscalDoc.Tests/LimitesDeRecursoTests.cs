using FiscalDoc.App;
using FiscalDoc.Core.Parsing;
using FiscalDoc.Layout.DisplayList;

namespace FiscalDoc.Tests;

/// <summary>
/// Os tetos que impedem o aplicativo de se derrubar sozinho.
///
/// Nenhum deles aparece no uso normal - e esse e o ponto. Eles existem para o
/// arquivo de 500 MB que chega por e-mail e para o monitor de 4K a 200%, duas
/// coisas que ninguem tem a mao na hora de conferir.
/// </summary>
public sealed class LimitesDeRecursoTests
{
    private const double MegapixelsDoTeto = 24.0;

    /// <summary>
    /// O orcamento e respeitado em qualquer densidade de monitor.
    ///
    /// <para>96 dpi e o monitor comum; 144 e 192 sao 150% e 200%; 288 e um
    /// portatil de 300%. Sem teto, o A4 em 5x a 192 dpi pedia 89 megapixels -
    /// 356 MB por superficie, e sao duas por desenho.</para>
    /// </summary>
    [Theory]
    [InlineData(96)]
    [InlineData(120)]
    [InlineData(144)]
    [InlineData(192)]
    [InlineData(288)]
    [InlineData(480)]
    public void Zoom_maximo_respeita_o_orcamento_de_memoria(double dpi)
    {
        double pxPorMmBase = dpi / 25.4;

        float zoom = LimiteDeZoom.Maximo(TamanhoPapel.A4, pxPorMmBase, MegapixelsDoTeto);

        double mp = LimiteDeZoom.Megapixels(TamanhoPapel.A4, pxPorMmBase, zoom);

        Assert.True(
            mp <= MegapixelsDoTeto + 0.01,
            $"a {dpi} dpi o zoom {zoom:0.00}x produz {mp:0.0} Mpx, acima do teto de {MegapixelsDoTeto}");
    }

    /// <summary>
    /// Num monitor comum o teto nao pode atrapalhar: 5x tem de continuar
    /// disponivel, porque e assim que se le texto de 5 pt.
    /// </summary>
    [Fact]
    public void Num_monitor_comum_o_teto_nao_limita_o_zoom_util()
    {
        float zoom = LimiteDeZoom.Maximo(TamanhoPapel.A4, 96.0 / 25.4, MegapixelsDoTeto);

        Assert.Equal(PaginaView.ZoomMaximoAbsoluto, zoom);
    }

    /// <summary>
    /// Mesmo num monitor de altissima densidade ainda sobra zoom suficiente
    /// para o texto miudo ficar legivel - o teto corta o desperdicio, nao o
    /// recurso.
    /// </summary>
    [Theory]
    [InlineData(192)]
    [InlineData(288)]
    public void Mesmo_em_monitor_denso_sobra_zoom_para_ler(double dpi)
    {
        double pxPorMmBase = dpi / 25.4;

        float zoom = LimiteDeZoom.Maximo(TamanhoPapel.A4, pxPorMmBase, MegapixelsDoTeto);

        // 12 px/mm deixa o menor texto do MOC (5 pt = 1,76 mm de em) com mais
        // de 20 pixels de altura de em. Confortavel com folga.
        double pxPorMm = pxPorMmBase * zoom;

        Assert.True(pxPorMm >= 12.0, $"a {dpi} dpi sobraram so {pxPorMm:0.0} px/mm");
    }

    /// <summary>A bobina da NFC-e e pequena: cabe o zoom inteiro.</summary>
    [Fact]
    public void Bobina_da_nfce_nao_e_limitada()
    {
        var bobina = new TamanhoPapel(80f, 200f);

        Assert.Equal(
            PaginaView.ZoomMaximoAbsoluto,
            LimiteDeZoom.Maximo(bobina, 192.0 / 25.4, MegapixelsDoTeto));
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(210, 0)]
    public void Papel_degenerado_nao_quebra_a_conta(float largura, float altura)
    {
        float zoom = LimiteDeZoom.Maximo(
            new TamanhoPapel(largura, altura), 96.0 / 25.4, MegapixelsDoTeto);

        Assert.InRange(zoom, PaginaView.ZoomMinimo, PaginaView.ZoomMaximoAbsoluto);
    }

    /// <summary>
    /// A recusa por tamanho fala portugues e nao inventa um diagnostico de
    /// corrupcao para um arquivo que so e grande.
    /// </summary>
    [Fact]
    public void Arquivo_grande_demais_tem_mensagem_propria()
    {
        var r = new ResultadoLeitura.ArquivoGrandeDemais(500L * 1024 * 1024);

        string m = r.MensagemUsuario;

        Assert.Contains("grande demais", m, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Exception", m, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Parameter", m, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// O arquivo ilegivel nao pode despejar o texto do framework na area do
    /// documento: <c>File.OpenRead("")</c> devolve
    /// "The value cannot be an empty string. (Parameter 'path')" - em ingles,
    /// com nome de parametro.
    /// </summary>
    [Fact]
    public void Arquivo_ilegivel_nao_vaza_texto_de_framework()
    {
        var r = new ResultadoLeitura.ArquivoIlegivel(
            "The value cannot be an empty string. (Parameter 'path')");

        string m = r.MensagemUsuario;

        Assert.DoesNotContain("Parameter", m, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("cannot be", m, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Não foi possível ler o arquivo", m, StringComparison.Ordinal);
    }
}
