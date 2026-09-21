namespace FiscalDoc.Layout.Barcode;

/// <summary>
/// Code 128, subconjunto C: dois digitos por simbolo.
///
/// Implementado a mao, e nao por biblioteca, por um motivo concreto: toda
/// biblioteca de codigo de barras devolve <b>bitmap</b>. Barra rasterizada e
/// depois reescalada para 600 dpi sai com larguras desiguais, e leitor recusa
/// barra desigual. Aqui a saida sao <b>larguras em modulos</b>, e quem decide
/// quantos pontos do dispositivo vale um modulo e o renderizador, que conhece
/// o DPI real. Ver plano 1.5 e 2.7.
///
/// Sao cerca de 110 linhas, contra uma dependencia inteira - e a tabela se
/// autovalida (ver <see cref="TabelaEhConsistente"/>).
/// </summary>
public static class Code128C
{
    /// <summary>Valor do simbolo Start C.</summary>
    public const int StartC = 105;

    /// <summary>Valor do simbolo Stop.</summary>
    public const int Stop = 106;

    /// <summary>
    /// Margem clara obrigatoria de cada lado, em modulos. Esquecer isto e a
    /// causa mais comum de "o leitor nao le".
    /// </summary>
    public const int MargemClaraModulos = 10;

    /// <summary>
    /// Padroes de largura dos 107 simbolos. Cada entrada alterna
    /// barra/espaco/barra/espaco/barra/espaco, com largura de 1 a 4 modulos.
    /// Os simbolos 0 a 105 somam 11 modulos; o Stop soma 13 (11 mais uma barra
    /// final de 2 modulos).
    /// </summary>
    private static readonly string[] Padroes =
    [
        "212222", "222122", "222221", "121223", "121322", "131222", "122213", "122312",
        "132212", "221213", "221312", "231212", "112232", "122132", "122231", "113222",
        "123122", "123221", "223211", "221132", "221231", "213212", "223112", "312131",
        "311222", "321122", "321221", "312212", "322112", "322211", "212123", "212321",
        "232121", "111323", "131123", "131321", "112313", "132113", "132311", "211313",
        "231113", "231311", "112133", "112331", "132131", "113123", "113321", "133121",
        "313121", "211331", "231131", "213113", "213311", "213131", "311123", "311321",
        "331121", "312113", "312311", "332111", "314111", "221411", "431111", "111224",
        "111422", "121124", "121421", "141122", "141221", "112214", "112412", "122114",
        "122411", "142112", "142211", "241211", "221114", "413111", "241112", "134111",
        "111242", "121142", "121241", "114212", "124112", "124211", "411212", "421112",
        "421211", "212141", "214121", "412121", "111143", "111341", "131141", "114113",
        "114311", "411113", "411311", "113141", "114131", "311141", "411131", "211412",
        "211214", "211232", "2331112",
    ];

    /// <summary>
    /// Codifica uma cadeia de digitos em larguras de modulo, ja com as margens
    /// claras nas duas pontas.
    ///
    /// A lista devolvida alterna <b>barra, espaco, barra, espaco...</b>
    /// comecando por <b>espaco</b> (a margem clara esquerda). O renderizador
    /// sabe disso.
    /// </summary>
    /// <param name="digitos">
    /// Quantidade par de digitos. A chave de acesso tem 44, que da 22 simbolos.
    /// </param>
    public static IReadOnlyList<int> Codificar(string digitos)
    {
        ArgumentNullException.ThrowIfNull(digitos);

        if (digitos.Length == 0 || digitos.Length % 2 != 0)
        {
            throw new ArgumentException(
                $"Code 128C exige quantidade par de digitos; recebeu {digitos.Length}.",
                nameof(digitos));
        }

        foreach (char c in digitos)
        {
            if (c is < '0' or > '9')
            {
                throw new ArgumentException(
                    $"Code 128C aceita somente digitos; encontrou '{c}'.", nameof(digitos));
            }
        }

        var valores = new List<int>((digitos.Length / 2) + 3) { StartC };

        for (int i = 0; i < digitos.Length; i += 2)
        {
            valores.Add(((digitos[i] - '0') * 10) + (digitos[i + 1] - '0'));
        }

        valores.Add(CalcularChecksum(valores));
        valores.Add(Stop);

        var larguras = new List<int> { MargemClaraModulos };

        foreach (int v in valores)
        {
            foreach (char largura in Padroes[v])
            {
                larguras.Add(largura - '0');
            }
        }

        // A margem clara direita e um espaco, e o simbolo Stop termina em
        // barra - entao basta acrescentar.
        larguras.Add(MargemClaraModulos);

        return larguras;
    }

    /// <summary>
    /// Checksum modulo 103: soma ponderada em que o Start entra com peso 1 e
    /// cada simbolo de dados com o peso da sua posicao, contada a partir de 1.
    /// </summary>
    /// <param name="valoresComStart">Start seguido dos simbolos de dados.</param>
    public static int CalcularChecksum(IReadOnlyList<int> valoresComStart)
    {
        int soma = valoresComStart[0];

        for (int i = 1; i < valoresComStart.Count; i++)
        {
            soma += valoresComStart[i] * i;
        }

        return soma % 103;
    }

    /// <summary>Total de modulos que a codificacao ocupa, margens incluidas.</summary>
    public static int TotalModulos(int quantidadeDigitos)
    {
        // margens (10 + 10) + start (11) + dados (n/2 * 11) + dv (11) + stop (13)
        return (2 * MargemClaraModulos) + 11 + (quantidadeDigitos / 2 * 11) + 11 + 13;
    }

    /// <summary>
    /// Autoteste da tabela, derivado da propria especificacao: em todo simbolo
    /// as barras somam numero <b>par</b> (4, 6 ou 8) e os espacos somam
    /// <b>impar</b> (3, 5 ou 7); os simbolos 0 a 105 somam 11 modulos e o Stop
    /// soma 13.
    ///
    /// Um digito trocado na transcricao da tabela quebra a paridade, entao
    /// esta regra e uma verificacao completa de graca - sem ela, o erro so
    /// apareceria quando alguem tentasse ler o codigo com um scanner.
    /// </summary>
    public static bool TabelaEhConsistente(out string? erro)
    {
        if (Padroes.Length != 107)
        {
            erro = $"a tabela deveria ter 107 simbolos e tem {Padroes.Length}";
            return false;
        }

        for (int v = 0; v < Padroes.Length; v++)
        {
            string p = Padroes[v];
            int esperadoDigitos = v == Stop ? 7 : 6;

            if (p.Length != esperadoDigitos)
            {
                erro = $"simbolo {v}: esperava {esperadoDigitos} larguras, tem {p.Length}";
                return false;
            }

            int barras = 0;
            int espacos = 0;

            for (int i = 0; i < p.Length; i++)
            {
                int largura = p[i] - '0';

                if (largura is < 1 or > 4)
                {
                    erro = $"simbolo {v}: largura {largura} fora da faixa de 1 a 4";
                    return false;
                }

                if (i % 2 == 0)
                {
                    barras += largura;
                }
                else
                {
                    espacos += largura;
                }
            }

            if (v != Stop)
            {
                if (barras + espacos != 11)
                {
                    erro = $"simbolo {v}: soma {barras + espacos}, esperava 11";
                    return false;
                }

                if (barras % 2 != 0)
                {
                    erro = $"simbolo {v}: barras somam {barras}, deveria ser par";
                    return false;
                }

                if (espacos % 2 == 0)
                {
                    erro = $"simbolo {v}: espacos somam {espacos}, deveria ser impar";
                    return false;
                }
            }
            else if (barras + espacos != 13)
            {
                erro = $"Stop: soma {barras + espacos}, esperava 13";
                return false;
            }
        }

        erro = null;
        return true;
    }
}
