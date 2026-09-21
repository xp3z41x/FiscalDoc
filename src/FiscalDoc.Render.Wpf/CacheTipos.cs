using System.Collections.Concurrent;
using System.Windows;
using System.Windows.Media;
using FiscalDoc.Layout.DisplayList;

namespace FiscalDoc.Render.Wpf;

/// <summary>
/// Cache de <see cref="Typeface"/> e de <see cref="Brush"/>.
///
/// Uma pagina de DANFE com 99 itens desenha alguns milhares de textos. O
/// <see cref="Typeface"/> em si e barato, mas e a chave para o
/// <c>GlyphTypeface</c>, que nao e - e o pincel congelado evita que o WPF
/// tenha de checar mutabilidade a cada primitiva.
/// </summary>
internal sealed class CacheTipos
{
    private readonly ConcurrentDictionary<EstiloTexto, Typeface> _tipos = new();
    private readonly Dictionary<Tinta, SolidColorBrush> _pinceis = [];

    internal CacheTipos()
    {
        foreach (Tinta t in Enum.GetValues<Tinta>())
        {
            var b = new SolidColorBrush(Cor(t));
            b.Freeze();
            _pinceis[t] = b;
        }
    }

    internal Typeface Obter(EstiloTexto estilo) =>
        _tipos.GetOrAdd(estilo, static e => new Typeface(
            new FontFamily(e.Familia),
            e.Italico ? FontStyles.Italic : FontStyles.Normal,
            e.Negrito ? FontWeights.Bold : FontWeights.Normal,
            FontStretches.Normal));

    internal SolidColorBrush Pincel(Tinta t) => _pinceis[t];

    /// <summary>
    /// A paleta do documento.
    ///
    /// Um DANFE e preto no branco; o cinza existe para a faixa de titulo de
    /// bloco e o vermelho para o estado de erro na tela - que nunca e
    /// impresso. Os nomes vem de <see cref="Tinta"/>, no Layout, que e quem
    /// decide; aqui so se resolve cada um para uma cor do WPF.
    /// </summary>
    private static Color Cor(Tinta t) => t switch
    {
        Tinta.Preto => Colors.Black,
        Tinta.CinzaClaro => Color.FromRgb(0xE8, 0xE8, 0xE8),
        Tinta.CinzaMedio => Color.FromRgb(0x9A, 0x9A, 0x9A),
        Tinta.Branco => Colors.White,
        Tinta.Vermelho => Color.FromRgb(0xB0, 0x1B, 0x1B),
        _ => Colors.Black,
    };
}
