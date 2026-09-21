using System.Globalization;
using System.Windows;
using System.Windows.Media;
using FiscalDoc.Layout;
using FiscalDoc.Layout.DisplayList;

namespace FiscalDoc.Render.Wpf;

/// <summary>
/// Medicao de texto pelo DirectWrite, em milimetros.
///
/// <para><b>Por que nao ha mais uma superficie de referencia.</b> A medicao
/// antiga acontecia contra um bitmap GDI+ de 600 dpi: resolucao fixa de
/// proposito, para que a paginacao fosse propriedade do documento e nao do
/// dispositivo. O DirectWrite em
/// <see cref="TextFormattingMode.Ideal"/> nao precisa desse truque - ele nao
/// encaixa glifo em grade de pixel, entao a metrica ja e <b>invariante de
/// escala</b>: medir a 4 px/mm ou a 600 dpi da o mesmo milimetro. Nao ha
/// superficie, nao ha DPI, nao ha nada a fixar.</para>
///
/// <para><b>E a mesma metrica que desenha.</b> Este medidor e o
/// <see cref="RenderizadorWpf"/> usam o mesmo motor, o mesmo modo de
/// formatacao e a mesma cultura. A linha que o layout quebrou aqui e
/// exatamente a linha que cabe la - na tela e no papel. Enquanto a medicao
/// era GDI+ e o desenho DirectWrite, as duas divergiam de 0,72 % na mediana e
/// 3,58 % no pior caso, e uma linha ja quebrada podia ser quebrada de novo na
/// hora de desenhar.</para>
/// </summary>
public sealed class MedidorTextoWpf : IMedidorTexto
{
    /// <summary>
    /// Unidades do WPF por milimetro. Uma unidade e 1/96 de polegada; este
    /// fator so existe para que a conta volte em milimetro no fim.
    /// </summary>
    private const double UnidadesPorMm = 96.0 / 25.4;

    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("pt-BR");

    private readonly CacheTipos _tipos = new();

    public float LarguraMm(string texto, EstiloTexto estilo)
    {
        if (string.IsNullOrEmpty(texto))
        {
            return 0f;
        }

        return (float)(Montar(texto, estilo).WidthIncludingTrailingWhitespace / UnidadesPorMm);
    }

    public float AlturaLinhaMm(EstiloTexto estilo)
    {
        ArgumentNullException.ThrowIfNull(estilo);

        // Uma unica linha de referencia: o "M" nao tem descendente, mas a
        // altura do FormattedText e a da LINHA, que ja inclui o espacamento
        // natural da familia. E o mesmo numero que o renderizador usa para
        // empilhar as linhas de uma descricao de produto.
        return (float)(Montar("M", estilo).Height / UnidadesPorMm);
    }

    public IReadOnlyList<string> Quebrar(string texto, EstiloTexto estilo, float larguraMm)
    {
        if (string.IsNullOrEmpty(texto) || larguraMm <= 0)
        {
            return [];
        }

        var linhas = new List<string>();

        // Quebra explicita do proprio dado vem antes da quebra por largura:
        // infCpl costuma trazer paragrafos com quebra de linha de verdade.
        foreach (string paragrafo in texto.Split(['\r', '\n'], StringSplitOptions.None))
        {
            if (paragrafo.Length == 0)
            {
                linhas.Add(string.Empty);
                continue;
            }

            QuebrarParagrafo(paragrafo, estilo, larguraMm, linhas);
        }

        return linhas;
    }

    private FormattedText Montar(string texto, EstiloTexto estilo) =>
        new(
            texto,
            Cultura,
            FlowDirection.LeftToRight,
            _tipos.Obter(estilo),
            estilo.TamanhoMm * UnidadesPorMm,
            Brushes.Black,
            null,
            TextFormattingMode.Ideal,
            1.0);

    private void QuebrarParagrafo(
        string paragrafo, EstiloTexto estilo, float larguraMm, List<string> saida)
    {
        var atual = new System.Text.StringBuilder();

        foreach (string palavra in paragrafo.Split(' '))
        {
            string candidato = atual.Length == 0 ? palavra : $"{atual} {palavra}";

            if (LarguraMm(candidato, estilo) <= larguraMm)
            {
                atual.Clear().Append(candidato);
                continue;
            }

            if (atual.Length > 0)
            {
                saida.Add(atual.ToString());
                atual.Clear();
            }

            // Palavra sozinha maior que a coluna: codigo de produto e NCM nao
            // tem espaco e ainda assim precisam caber. Quebra no meio.
            if (LarguraMm(palavra, estilo) > larguraMm)
            {
                QuebrarPalavra(palavra, estilo, larguraMm, saida, atual);
            }
            else
            {
                atual.Append(palavra);
            }
        }

        if (atual.Length > 0)
        {
            saida.Add(atual.ToString());
        }
    }

    private void QuebrarPalavra(
        string palavra,
        EstiloTexto estilo,
        float larguraMm,
        List<string> saida,
        System.Text.StringBuilder resto)
    {
        var pedaco = new System.Text.StringBuilder();

        foreach (char c in palavra)
        {
            pedaco.Append(c);

            if (LarguraMm(pedaco.ToString(), estilo) > larguraMm && pedaco.Length > 1)
            {
                pedaco.Length--;
                saida.Add(pedaco.ToString());
                pedaco.Clear().Append(c);
            }
        }

        if (pedaco.Length > 0)
        {
            resto.Append(pedaco);
        }
    }
}
