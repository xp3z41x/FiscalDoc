using System.Drawing;
using System.Drawing.Imaging;
using Gdi = System.Drawing.Imaging;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FiscalDoc.Layout.DisplayList;
using FiscalDoc.Render.Wpf;

namespace FiscalDoc.Tests;

/// <summary>
/// Rasteriza uma pagina para conferencia, pelos dois modos do renderizador.
///
/// Existe um unico renderizador no aplicativo - o <see cref="RenderizadorWpf"/> -
/// e ele desenha em dois modos: <b>tela</b>, escrevendo pixels num bitmap, e
/// <b>papel</b>, escrevendo vetor num contexto do XPS. Os testes precisam de
/// pixels nos dois casos, entao o modo de papel e rasterizado aqui numa
/// resolucao declarada, do mesmo jeito que o RIP da impressora faria.
/// </summary>
internal static class RenderTeste
{
    /// <summary>Unidades do WPF por milimetro.</summary>
    internal const double UnidadesPorMm = 96.0 / 25.4;

    /// <summary>Como a previa desenha: pixels, com encaixe na grade.</summary>
    internal static Bitmap Tela(Pagina pagina, TamanhoPapel papel, double pxPorMm)
    {
        int w = Math.Max(1, (int)Math.Round(papel.LarguraMm * pxPorMm));
        int h = Math.Max(1, (int)Math.Round(papel.AlturaMm * pxPorMm));
        return Tela(pagina, w, h, pxPorMm);
    }

    internal static Bitmap Tela(Pagina pagina, int larguraPx, int alturaPx, double pxPorMm)
    {
        var bmp = new Bitmap(larguraPx, alturaPx, Gdi.PixelFormat.Format32bppPArgb);

        BitmapData trava = bmp.LockBits(
            new Rectangle(0, 0, larguraPx, alturaPx), ImageLockMode.WriteOnly, bmp.PixelFormat);

        try
        {
            new RenderizadorWpf().Desenhar(
                pagina, pxPorMm, larguraPx, alturaPx, trava.Scan0, trava.Stride);
        }
        finally
        {
            bmp.UnlockBits(trava);
        }

        return bmp;
    }

    /// <summary>
    /// Como o papel recebe: vetor, sem encaixe, rasterizado depois na
    /// resolucao dada - que e o papel do RIP.
    /// </summary>
    internal static Bitmap Papel(
        Pagina pagina, TamanhoPapel papel, double dpi, AjusteImpressao? ajuste = null)
    {
        double pxPorMm = dpi / 25.4;
        int w = Math.Max(1, (int)Math.Round(papel.LarguraMm * pxPorMm));
        int h = Math.Max(1, (int)Math.Round(papel.AlturaMm * pxPorMm));

        var visual = new DrawingVisual();

        using (DrawingContext dc = visual.RenderOpen())
        {
            dc.DrawRectangle(
                System.Windows.Media.Brushes.White,
                null,
                // Cobre o bitmap INTEIRO, nao o retangulo em milimetros: a
                // largura em pixel e arredondada, entao a ultima coluna ficava
                // meio transparente e virava "tinta" na mascara.
                new System.Windows.Rect(
                    0, 0, w * 96.0 / dpi, h * 96.0 / dpi));

            new RenderizadorWpf().DesenharNoPapel(
                dc, pagina, dpi, ajuste ?? AjusteImpressao.Nenhum);
        }

        var alvo = new RenderTargetBitmap(w, h, dpi, dpi, System.Windows.Media.PixelFormats.Pbgra32);
        alvo.Render(visual);

        var bmp = new Bitmap(w, h, Gdi.PixelFormat.Format32bppPArgb);

        BitmapData trava = bmp.LockBits(
            new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, bmp.PixelFormat);

        try
        {
            alvo.CopyPixels(
                new System.Windows.Int32Rect(0, 0, w, h),
                trava.Scan0,
                trava.Stride * h,
                trava.Stride);
        }
        finally
        {
            bmp.UnlockBits(trava);
        }

        return bmp;
    }
}
