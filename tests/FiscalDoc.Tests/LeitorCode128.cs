using System.Drawing;
using System.Globalization;
using System.Text;
using FiscalDoc.Layout.Barcode;

namespace FiscalDoc.Tests;

/// <summary>
/// O caminho inverso do <see cref="Code128"/>, para provar que o codigo que se
/// desenha le de volta a chave - e so a chave.
///
/// <para>Faz o que um leitor faz: confere Start, DV modulo 103 e Stop, e
/// interpreta cada simbolo no subconjunto corrente. Nao usa nada do
/// codificador alem da tabela de padroes e do calculo do DV, que tem testes
/// proprios.</para>
/// </summary>
internal static class LeitorCode128
{
    private static readonly Dictionary<string, int> PorPadrao =
        Enumerable.Range(0, 107).ToDictionary(Code128.Padrao);

    /// <summary>Os dados que os simbolos representam, do Start ao Stop.</summary>
    internal static string Dados(IReadOnlyList<int> simbolos)
    {
        Assert.True(simbolos.Count >= 3, "codigo sem Start, DV e Stop");
        Assert.Equal(Code128.StartC, simbolos[0]);
        Assert.Equal(Code128.Stop, simbolos[^1]);
        Assert.Equal(
            Code128.CalcularChecksum(simbolos.Take(simbolos.Count - 2).ToList()),
            simbolos[^2]);

        var dados = new StringBuilder();
        bool emC = true;

        for (int i = 1; i < simbolos.Count - 2; i++)
        {
            int v = simbolos[i];

            if (emC && v == Code128.CodeA)
            {
                emC = false;
            }
            else if (!emC && v == Code128.CodeC)
            {
                emC = true;
            }
            else if (emC)
            {
                Assert.InRange(v, 0, 99);
                dados.Append(v.ToString("D2", CultureInfo.InvariantCulture));
            }
            else
            {
                // Subconjunto A, do espaco ao sublinhado.
                Assert.InRange(v, 0, 63);
                dados.Append((char)(v + ' '));
            }
        }

        return dados.ToString();
    }

    /// <summary>Os simbolos que as larguras em modulos representam, margens incluidas.</summary>
    internal static IReadOnlyList<int> Simbolos(IReadOnlyList<int> larguras)
    {
        Assert.Equal(Code128.MargemClaraModulos, larguras[0]);
        Assert.Equal(Code128.MargemClaraModulos, larguras[^1]);

        return SimbolosDasFaixas(larguras.Skip(1).Take(larguras.Count - 2).ToList());
    }

    /// <summary>
    /// Le o codigo numa linha de pixels: mede cada faixa, da primeira barra a
    /// ultima, e converte em modulos pela largura total - que sao 11 modulos
    /// por simbolo e 13 no Stop.
    /// </summary>
    internal static IReadOnlyList<int> SimbolosDaImagem(Bitmap bmp, int y)
    {
        int primeiro = -1;
        int ultimo = -1;

        for (int x = 0; x < bmp.Width; x++)
        {
            if (Escuro(bmp, x, y))
            {
                primeiro = primeiro < 0 ? x : primeiro;
                ultimo = x;
            }
        }

        Assert.True(primeiro >= 0, "nenhuma barra na linha");

        var faixas = new List<int>();
        bool escuro = true;
        int corrida = 0;

        for (int x = primeiro; x <= ultimo; x++)
        {
            if (Escuro(bmp, x, y) == escuro)
            {
                corrida++;
            }
            else
            {
                faixas.Add(corrida);
                corrida = 1;
                escuro = !escuro;
            }
        }

        faixas.Add(corrida);

        // 6 faixas por simbolo e 7 no Stop.
        Assert.Equal(1, faixas.Count % 6);
        int simbolos = (faixas.Count - 7) / 6;
        double modulo = (ultimo - primeiro + 1) / ((11.0 * simbolos) + 13.0);

        return SimbolosDasFaixas(faixas.Select(f => (int)Math.Round(f / modulo)).ToList());
    }

    private static bool Escuro(Bitmap bmp, int x, int y) => bmp.GetPixel(x, y).R < 128;

    private static List<int> SimbolosDasFaixas(IReadOnlyList<int> faixas)
    {
        Assert.Equal(1, faixas.Count % 6);

        var simbolos = new List<int>();

        for (int i = 0; i < faixas.Count - 7; i += 6)
        {
            simbolos.Add(Simbolo(faixas, i, 6));
        }

        simbolos.Add(Simbolo(faixas, faixas.Count - 7, 7));
        return simbolos;
    }

    private static int Simbolo(IReadOnlyList<int> faixas, int de, int quantas)
    {
        string padrao = string.Concat(faixas.Skip(de).Take(quantas));

        Assert.True(PorPadrao.TryGetValue(padrao, out int valor), $"padrao {padrao} nao e Code 128");
        return valor;
    }
}
