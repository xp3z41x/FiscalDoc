using FiscalDoc.Layout.DisplayList;

namespace FiscalDoc.App;

/// <summary>
/// O teto de zoom da previa, em funcao da memoria.
///
/// <para>Rasterizar uma pagina aloca <b>duas</b> superficies do tamanho dela:
/// o bitmap que fica em cache e a superficie do WPF que o produz. A superficie
/// do WPF nao e descartavel - ela guarda os pixels em memoria nativa atras de
/// um objeto gerenciado minusculo, entao o coletor quase nao ve pressao e
/// demora a recolher.</para>
///
/// <para>Sem teto, a A4 em 5x num monitor de 192 dpi da 89 megapixels: 356 MB
/// por superficie, 713 MB de pico a cada marca da roda. Segurar Ctrl e girar
/// acumulava mais de um gigabyte de memoria nativa antes de qualquer coleta,
/// e o que o usuario via era a janela congelando e o zoom voltando sozinho.
/// </para>
///
/// <para>Calculo separado da janela porque e aritmetica pura e merece
/// teste - o defeito que ele corrige so aparece em monitor de alta densidade,
/// que e justamente o que ninguem tem a mao na hora de conferir.</para>
/// </summary>
internal static class LimiteDeZoom
{
    /// <summary>
    /// Maior zoom cuja pagina rasterizada cabe no orcamento.
    /// </summary>
    /// <param name="papel">Tamanho da pagina, em milimetros.</param>
    /// <param name="pxPorMmBase">Pixels por milimetro em zoom 1 (DPI / 25,4).</param>
    /// <param name="megapixels">Orcamento, em megapixels.</param>
    internal static float Maximo(TamanhoPapel papel, double pxPorMmBase, double megapixels)
    {
        double areaMm = (double)papel.LarguraMm * papel.AlturaMm;

        if (areaMm <= 0 || pxPorMmBase <= 0 || megapixels <= 0)
        {
            return PaginaView.ZoomMaximoAbsoluto;
        }

        // pixels = area_mm * (pxPorMmBase * zoom)^2
        double zoom = Math.Sqrt(megapixels * 1e6 / (areaMm * pxPorMmBase * pxPorMmBase));

        return (float)Math.Clamp(zoom, PaginaView.ZoomMinimo, PaginaView.ZoomMaximoAbsoluto);
    }

    /// <summary>Megapixels de uma pagina naquele zoom - o que o teto limita.</summary>
    internal static double Megapixels(TamanhoPapel papel, double pxPorMmBase, double zoom)
    {
        double pxPorMm = pxPorMmBase * zoom;
        return (double)papel.LarguraMm * papel.AlturaMm * pxPorMm * pxPorMm / 1e6;
    }
}
