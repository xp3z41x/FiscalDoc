namespace FiscalDoc.Layout.Barcode;

/// <summary>
/// Code 128 da chave de acesso: subconjunto C, dois digitos por simbolo, e
/// subconjunto A para as letras do CNPJ alfanumerico.
///
/// <para>Chave so de digitos sai exatamente como sempre saiu: Start C e 22
/// pares, o CODE-128C do MOC 7.0 Anexo II, secao 2. Com letra - o CNPJ
/// alfanumerico da IN RFB 2.229/2024 -, e o "modelo hibrido" da NT Conjunta
/// 2025.001, item 6: comeca em C e, diante de caractere nao numerico, passa ao
/// subconjunto A, que codifica letra maiuscula e digito um a um.</para>
///
/// Implementado a mao, e nao por biblioteca, por um motivo concreto: toda
/// biblioteca de codigo de barras devolve <b>bitmap</b>. Barra rasterizada e
/// depois reescalada para 600 dpi sai com larguras desiguais, e leitor recusa
/// barra desigual. Aqui a saida sao <b>larguras em modulos</b>, e quem decide
/// quantos pontos do dispositivo vale um modulo e o renderizador, que conhece
/// o DPI real. Ver plano 1.5 e 2.7.
///
/// A tabela se autovalida (ver <see cref="TabelaEhConsistente"/>). A da NT nao
/// serve de conferencia: la todo simbolo, fora o Stop, soma 10 modulos, e nao
/// 11 - e a propria NT remete o conjunto de caracteres a especificacao do
/// Code 128.
/// </summary>
public static class Code128
{
    /// <summary>Valor do simbolo Start C.</summary>
    public const int StartC = 105;

    /// <summary>No subconjunto C, o simbolo que passa ao subconjunto A.</summary>
    public const int CodeA = 101;

    /// <summary>No subconjunto A, o simbolo que passa ao subconjunto C.</summary>
    public const int CodeC = 99;

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
    /// Codifica a chave em larguras de modulo, ja com as margens claras nas
    /// duas pontas.
    ///
    /// A lista devolvida alterna <b>barra, espaco, barra, espaco...</b>
    /// comecando por <b>espaco</b> (a margem clara esquerda). O renderizador
    /// sabe disso.
    /// </summary>
    /// <param name="dados">
    /// Digitos e letras maiusculas - o alfabeto da chave de acesso. A chave so
    /// de digitos da 22 simbolos de dados; ver <see cref="Simbolos"/>.
    /// </param>
    public static IReadOnlyList<int> Codificar(string dados)
    {
        IReadOnlyList<int> valores = Simbolos(dados);

        var larguras = new List<int>((valores.Count * 6) + 3) { MargemClaraModulos };

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
    /// Os valores dos simbolos, do Start ao Stop, com o DV.
    ///
    /// <para>As trocas de subconjunto seguem as orientacoes da NT Conjunta
    /// 2025.001 (item 6) para o codigo mais curto - que sao as do anexo
    /// informativo da ISO/IEC 15417, restritas a A e C:</para>
    /// <list type="number">
    /// <item>Start C, porque a chave comeca por seis digitos.</item>
    /// <item>Se os dados comecam por um numero impar de digitos, Code A antes
    /// do ultimo deles.</item>
    /// <item>No A, quatro ou mais digitos seguidos voltam ao C: antes do
    /// primeiro se o grupo e par; logo depois do primeiro, que fica no A, se e
    /// impar.</item>
    /// <item>No C, Code A antes de todo caractere nao numerico.</item>
    /// </list>
    ///
    /// <para>O texto da NT diz que a troca se faz "usando o codigo 100", mas o
    /// exemplo resolvido que vem logo depois usa 101 para ir ao A e 99 para
    /// voltar ao C - que e o que a especificacao do Code 128 define: no
    /// subconjunto C, o 100 leva ao B. Vale o exemplo. Para digito e letra
    /// maiuscula, alias, A e B atribuem os mesmos valores, e o leitor le a
    /// mesma chave de um jeito ou de outro.</para>
    ///
    /// <para>O mesmo exemplo passa ao C para os dois digitos finais de
    /// "5225AB83", o que a regra 3 so manda fazer a partir de quatro. Numa
    /// chave de verdade a diferenca nao aparece: ela termina sempre em 26
    /// digitos, e a regra 3 vale de todo jeito.</para>
    /// </summary>
    public static IReadOnlyList<int> Simbolos(string dados)
    {
        ArgumentNullException.ThrowIfNull(dados);

        if (dados.Length == 0)
        {
            throw new ArgumentException("Code 128 sem dados.", nameof(dados));
        }

        foreach (char c in dados)
        {
            if (!char.IsAsciiDigit(c) && !char.IsAsciiLetterUpper(c))
            {
                throw new ArgumentException(
                    $"a chave de acesso so tem digitos e letras maiusculas; encontrou '{c}'.",
                    nameof(dados));
            }
        }

        var valores = new List<int>(dados.Length + 5) { StartC };

        // Regras 1 e 2: os pares iniciais no C. Se sobra um digito, ele e o
        // "ultimo digito impar", e sai no A.
        int i = 0;
        int inicio = DigitosSeguidos(dados, 0);

        while (i + 1 < inicio)
        {
            valores.Add(Par(dados, i));
            i += 2;
        }

        bool emC = true;

        while (i < dados.Length)
        {
            if (emC)
            {
                // Regra 4. No C so se chega com grupo par de digitos, entao o
                // que vem agora e letra - ou o digito impar da regra 2.
                valores.Add(CodeA);
                emC = false;
            }

            int grupo = DigitosSeguidos(dados, i);

            if (grupo >= 4)
            {
                // Regra 3.
                if (grupo % 2 == 1)
                {
                    valores.Add(ValorNoA(dados[i]));
                    i++;
                    grupo--;
                }

                valores.Add(CodeC);
                emC = true;

                for (int fim = i + grupo; i < fim; i += 2)
                {
                    valores.Add(Par(dados, i));
                }
            }
            else
            {
                valores.Add(ValorNoA(dados[i]));
                i++;
            }
        }

        valores.Add(CalcularChecksum(valores));
        valores.Add(Stop);

        return valores;
    }

    /// <summary>Padrao de larguras do simbolo: barra, espaco, barra... em modulos.</summary>
    public static string Padrao(int valor) => Padroes[valor];

    private static int DigitosSeguidos(string dados, int de)
    {
        int ate = de;
        while (ate < dados.Length && char.IsAsciiDigit(dados[ate]))
        {
            ate++;
        }

        return ate - de;
    }

    /// <summary>Subconjunto C: o par "07" e o simbolo 7.</summary>
    private static int Par(string dados, int i) => ((dados[i] - '0') * 10) + (dados[i + 1] - '0');

    /// <summary>
    /// Subconjunto A: do espaco (32) ao sublinhado (95), o valor e o ASCII
    /// menos 32 - "0" vale 16 e "A" vale 33, como no exemplo da NT.
    /// </summary>
    private static int ValorNoA(char c) => c - ' ';

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

    /// <summary>
    /// Total de modulos de uma cadeia <b>so de digitos</b>, de comprimento par,
    /// margens incluidas: 297 para a chave numerica. Com letra, o total depende
    /// de onde elas caem - some as larguras de <see cref="Codificar"/>.
    /// </summary>
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
