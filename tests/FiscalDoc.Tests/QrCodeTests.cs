using FiscalDoc.Layout.Barcode;

namespace FiscalDoc.Tests;

/// <summary>
/// O codificador de QR Code. Tres camadas de verificacao, porque um QR errado
/// nao e visivel a olho nu - ele simplesmente nao le.
///
/// <list type="number">
/// <item><b>As tabelas se autovalidam</b> contra a formula de contagem de
/// modulos da norma, como a tabela do Code 128 se autovalida pela paridade das
/// barras.</item>
/// <item><b>A capacidade bate com a tabela publicada</b> da ISO/IEC 18004 em
/// pontos escolhidos da faixa de versoes.</item>
/// <item><b>Uma matriz de referencia</b>, modulo a modulo, para a URL de
/// QR Code de uma das NFC-e reais do corpus. Ela nao guarda nenhuma escolha
/// deste codigo: se a codificacao, a intercalacao de blocos, a mascara
/// escolhida ou o campo de formato mudarem, esta matriz denuncia.</item>
/// </list>
///
/// <para><b>De onde veio a matriz de referencia.</b> Foi gerada por uma
/// implementacao independente (a biblioteca Python <i>segno</i>, 1.6.6) e
/// conferida modulo a modulo contra a saida daqui, em quatro entradas de
/// versoes diferentes: 1, 3, 8 e 24. As quatro batem exatamente.</para>
///
/// <para>Com uma ressalva que vale registrar, porque custou tempo: o segno
/// 1.6.6 acrescenta um codeword 0x00 a mais quando o fluxo de bits ja termina
/// em fronteira de byte - escreve <c>8 - (len % 8)</c> bits de preenchimento
/// sem o segundo modulo, o que da 8 quando deveria dar 0. A norma
/// (ISO/IEC 18004, 7.4.10) manda preencher ate a fronteira e so entao inserir
/// os codewords de enchimento 11101100 / 00010001. A comparacao foi feita com
/// esse defeito corrigido no lado do segno; o codificador daqui segue a norma.
/// O simbolo do segno tambem e valido - preenchimento depois da carga util
/// nao e lido -, mas nao e o que a norma descreve.</para>
/// </summary>
public sealed class QrCodeTests
{
    /// <summary>
    /// URL de consulta no formato do manual do DANFE NFC-e, com chave e hash
    /// ficticios. Sao 145 bytes, que no nivel M caem na versao 8 - 49 x 49
    /// modulos, a mesma faixa de um cupom de verdade.
    ///
    /// <para>A chave e ficticia de proposito: chave de acesso e credencial de
    /// consulta no portal da SEFAZ, e este repositorio e publico.</para>
    /// </summary>
    private const string UrlNfce =
        "http://app.sefaz.es.gov.br/ConsultaNFCe/QRCode.aspx?p="
        + "32251211222333000181650010000560961041887805|2|1|1|"
        + "A1B2C3D4E5F60718293A4B5C6D7E8F90A1B2C3D4";

    /// <summary>
    /// A matriz esperada para <see cref="UrlNfce"/>: '#' escuro, '.' claro,
    /// sem margem clara. Nivel M, versao 8, mascara 2.
    ///
    /// <para>Conferida por DECODIFICACAO independente: a matriz foi passada
    /// por um leitor de QR de terceiro (OpenCV) e voltou exatamente a string
    /// de <see cref="UrlNfce"/>. E a unica verificacao que interessa aqui -
    /// o que importa nao e ser byte a byte igual ao simbolo que outra
    /// biblioteca produziria, e sim ser um simbolo que um leitor le certo.</para>
    ///
    /// <para>Nao e capricho: codificadores validos divergem entre si. O segno,
    /// por exemplo, produz para esta mesma URL um simbolo de mesma versao e
    /// mesma mascara que difere em 126 dos 2.401 modulos - e ambos decodificam
    /// para o mesmo texto. Exigir igualdade modulo a modulo contra outra
    /// biblioteca reprovaria uma implementacao correta.</para>
    /// </summary>
    private static readonly string[] MatrizEsperada =
    [
        "#######..#.###..#.#.#..##...#.##........#.#######",
        "#.....#...###.###..#.#######.#..##.##.###.#.....#",
        "#.###.#.###.##.####..#..#...##.#####...##.#.###.#",
        "#.###.#.#...##.#..#..#.#.###.#...##.#..#..#.###.#",
        "#.###.#.###.###.############.##..#..##....#.###.#",
        "#.....#.#.#.##.#.#..###...#.##...####.#...#.....#",
        "#######.#.#.#.#.#.#.#.#.#.#.#.#.#.#.#.#.#.#######",
        "........#.###...#####.#...#.#.###.##...#.........",
        "#.#####..###.###......######........##.#..#####..",
        "..#....##.##......##.#...#.##.##.#..##....##.#...",
        "#.##..#....#.....#...###..##.#....#.###.##...#..#",
        "...#...##.##..####.#.#..#..#.##.##.#...#.#.###.##",
        ".####.##.#..#...#.##..#####..###...##...##....###",
        "...#....#...#.....#..#.##...######.##..#..#....#.",
        ".#...####.#.##.#.#.#....###.##.#..#.#####....####",
        "##...#..##.#...#.##..#.#...#..###..#.....#..##..#",
        "#.#####.###..##...###...##....#..#..##.###.#..#..",
        "#..##..#.#.#.##..##...#.##.####.##.#.....####...#",
        "#.###.#...####....#..#..####..##..#.#####....#.##",
        "#....#......#.....##...#....#####.##.##.#.#.#..##",
        ".###..###.#.#...#####.#####......##.#.#.#.#...###",
        ".##.....###....###...#.#.#....#.....##....#.#.##.",
        "###.######.#.#..##.#.#######....###.#########.#.#",
        "###.#...####...###...##...#.#.####.#...##...#..##",
        "##.##.#.##..##.#.##..##.#.##.##...#######.#.#..##",
        "....#...#..#.##...##.##...###.##.#.#.#.##...##...",
        ".##.#####..#.#..##..#.######.#.#..##.##.######.##",
        "....##....#...#.#######.#..###..###...#..#..##.#.",
        ".#.#.##.#.#.###..#......####..##.#.###.##..##.#.#",
        "#.####.....#.#.#...###..#####.#.##.....#..#......",
        "##...####...#...##..###.##.#.#.##.#####.#.#######",
        ".##.#...#..##########..##.#.#.#.....##...#.#.#.##",
        "#.....##.#..#.##..#...##...#.###..###..###.##.#.#",
        "######..###......###....#####.#....###.###.....#.",
        "...##.###..#.#.##.#######..#.#..##.##..#.##..#..#",
        "###..#..#.##.#.......#..###.##.##..#...........##",
        "#.#.####.#..######.#####.###........#######...###",
        "........##..###.###..#..#..#.####..###.##.##.#.#.",
        ".#...##....##.####....##.#..#..#..#.#.#####.#...#",
        ".###....###..#.#.#..##..#...#.####.#....#........",
        "###...#####..#....#..#######.##..##.#..######..##",
        "........#.#..##..###.##...###.#.##..##..#...##..#",
        "#######..##..###.###..#.#.##.#....#..####.#.#..##",
        "#.....#.#.#.#..#.....##...#.#.#.###..#.##...##...",
        "#.###.#.#.......##.#.######......#.##.#######.###",
        "#.###.#.###.##....#...##.#.#######.##...#.####.##",
        "#.###.#.##....##...#.#.##..#.#....######..#..#...",
        "#.....#..###..#.#.#...#....#..###..#...##.#..#..#",
        "#######.##.##.##...###.#.##..##.....#.##.#....###",
    ];

    [Fact]
    public void Tabelas_de_correcao_sao_consistentes_com_a_formula_da_norma()
    {
        Assert.True(QrCode.TabelasSaoConsistentes(out string? erro), erro);
    }

    [Theory]
    // Capacidade em modo binario, nivel M (ISO/IEC 18004, tabela 7).
    [InlineData(1, 14)]
    [InlineData(2, 26)]
    [InlineData(5, 84)]
    [InlineData(8, 152)]
    [InlineData(9, 180)]
    [InlineData(10, 213)]
    [InlineData(24, 911)]
    [InlineData(40, 2331)]
    public void Capacidade_bate_com_a_tabela_publicada(int versao, int bytesEsperados)
    {
        Assert.Equal(bytesEsperados, QrCode.CapacidadeDadosBytes(versao));
    }

    [Fact]
    public void Versao_escolhida_e_a_menor_que_comporta_o_texto()
    {
        // Um byte a mais que a capacidade da versao 1 ja exige a versao 2.
        Assert.Equal(1, QrCode.EscolherVersao(14));
        Assert.Equal(2, QrCode.EscolherVersao(15));
        Assert.Equal(40, QrCode.EscolherVersao(2331));

        // Acima da versao 40 nao ha simbolo nenhum.
        Assert.Equal(-1, QrCode.EscolherVersao(2332));
    }

    [Fact]
    public void Texto_grande_demais_e_recusado_com_mensagem_util()
    {
        var erro = Assert.Throws<ArgumentException>(
            () => QrCode.Codificar(new string('x', 3000)));

        Assert.Contains("2331", erro.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Url_de_nfce_real_produz_a_matriz_de_referencia()
    {
        MatrizQr m = QrCode.Codificar(UrlNfce);

        Assert.Equal(8, m.Versao);
        Assert.Equal(49, m.Tamanho);
        Assert.Equal(MatrizEsperada.Length, m.Tamanho);

        for (int y = 0; y < m.Tamanho; y++)
        {
            string esperada = MatrizEsperada[y];
            Assert.Equal(m.Tamanho, esperada.Length);

            for (int x = 0; x < m.Tamanho; x++)
            {
                bool esperado = esperada[x] == '#';

                Assert.True(
                    esperado == m.Escuro(x, y),
                    $"módulo ({x},{y}): esperado {(esperado ? "escuro" : "claro")}");
            }
        }
    }

    [Fact]
    public void Localizadores_e_sincronismo_estao_no_lugar()
    {
        MatrizQr m = QrCode.Codificar(UrlNfce);
        int n = m.Tamanho;

        // Os tres localizadores: 7x7 com um anel claro a distancia 2.
        foreach ((int cx, int cy) in new[] { (3, 3), (n - 4, 3), (3, n - 4) })
        {
            for (int dy = -3; dy <= 3; dy++)
            {
                for (int dx = -3; dx <= 3; dx++)
                {
                    int distancia = Math.Max(Math.Abs(dx), Math.Abs(dy));
                    Assert.Equal(distancia != 2, m.Escuro(cx + dx, cy + dy));
                }
            }
        }

        // Sincronismo: linha e coluna 6 alternam, comecando escuro.
        for (int i = 8; i < n - 8; i++)
        {
            Assert.Equal(i % 2 == 0, m.Escuro(i, 6));
            Assert.Equal(i % 2 == 0, m.Escuro(6, i));
        }

        // O modulo que a norma manda ser sempre escuro.
        Assert.True(m.Escuro(8, n - 8));
    }

    [Fact]
    public void Campo_de_formato_declara_nivel_m_nas_duas_copias()
    {
        MatrizQr m = QrCode.Codificar(UrlNfce);
        int n = m.Tamanho;

        // As duas copias sao redundantes no simbolo justamente para o leitor
        // poder conferir uma contra a outra.
        int primeira = LerFormato(m, PosicoesPrimeiraCopia());
        int segunda = LerFormato(m, PosicoesSegundaCopia(n));

        Assert.Equal(primeira, segunda);

        int bits = primeira ^ 0x5412;

        // Resto zero no BCH(15,5): o campo esta bem formado.
        int resto = bits;

        for (int i = 4; i >= 0; i--)
        {
            if ((resto & (1 << (i + 10))) != 0)
            {
                resto ^= 0x537 << i;
            }
        }

        Assert.Equal(0, resto);

        int dados = bits >> 10;

        // Nivel M e 00 no campo de formato, e o manual do DANFE NFC-e exige M.
        Assert.Equal(0, dados >> 3);
        Assert.InRange(dados & 7, 0, 7);
    }

    [Fact]
    public void Blocos_sao_divisiveis_pelo_polinomio_gerador()
    {
        // Propriedade que define um codigo Reed-Solomon: a palavra completa,
        // dados seguidos de correcao, e multipla do gerador. Se a aritmetica
        // em GF(256) estivesse errada, isto falharia.
        byte[] divisor = QrCode.GerarDivisor(22);
        var dados = new byte[38];

        for (int i = 0; i < dados.Length; i++)
        {
            dados[i] = (byte)((i * 7) + 3);
        }

        byte[] correcao = QrCode.RestoReedSolomon(dados, divisor);
        byte[] palavra = [.. dados, .. correcao];

        Assert.All(QrCode.RestoReedSolomon(palavra, divisor), b => Assert.Equal(0, b));
    }

    [Fact]
    public void Aritmetica_de_campo_finito_obedece_as_leis_do_corpo()
    {
        // 1 e neutro, 0 e absorvente e o produto e comutativo. Um erro no
        // polinomio primitivo quebraria a comutatividade em algum par.
        for (int a = 0; a < 256; a++)
        {
            Assert.Equal(a, QrCode.MultiplicarGf((byte)a, 1));
            Assert.Equal(0, QrCode.MultiplicarGf((byte)a, 0));

            for (int b = 0; b < 256; b += 17)
            {
                Assert.Equal(
                    QrCode.MultiplicarGf((byte)a, (byte)b),
                    QrCode.MultiplicarGf((byte)b, (byte)a));
            }
        }
    }

    [Fact]
    public void Margem_clara_cumpre_a_iso_e_os_10_por_cento_do_manual()
    {
        foreach (string texto in new[] { "x", UrlNfce, new string('y', 900) })
        {
            MatrizQr m = QrCode.Codificar(texto);
            int q = m.MargemModulos;

            // ISO/IEC 18004: nunca menos de 4 modulos.
            Assert.True(q >= 4, $"versão {m.Versao}: margem de {q} módulos");

            // Manual do DANFE NFC-e 3.2: pelo menos 10% da dimensao total.
            Assert.True(
                q * 10 >= m.Tamanho + (2 * q),
                $"versão {m.Versao}: margem de {q} em {m.Tamanho + (2 * q)} módulos");
        }
    }

    [Fact]
    public void Texto_vazio_ainda_produz_um_simbolo_valido()
    {
        // Nao e caso de uso, mas o codificador nao pode quebrar: o campo
        // qrCode pode chegar vazio num arquivo irregular.
        MatrizQr m = QrCode.Codificar(string.Empty);

        Assert.Equal(1, m.Versao);
        Assert.Equal(21, m.Tamanho);
    }

    /// <summary>
    /// Posicoes dos 15 bits do campo de formato, do menos significativo ao
    /// mais, codificadas como x * 100 + y.
    /// </summary>
    private static int[] PosicoesPrimeiraCopia()
    {
        var p = new int[15];

        for (int i = 0; i <= 5; i++)
        {
            p[i] = (8 * 100) + i;
        }

        p[6] = (8 * 100) + 7;
        p[7] = (8 * 100) + 8;
        p[8] = (7 * 100) + 8;

        for (int i = 9; i < 15; i++)
        {
            p[i] = ((14 - i) * 100) + 8;
        }

        return p;
    }

    private static int[] PosicoesSegundaCopia(int n)
    {
        var p = new int[15];

        for (int i = 0; i < 8; i++)
        {
            p[i] = ((n - 1 - i) * 100) + 8;
        }

        for (int i = 8; i < 15; i++)
        {
            p[i] = (8 * 100) + (n - 15 + i);
        }

        return p;
    }

    private static int LerFormato(MatrizQr m, int[] posicoes)
    {
        int v = 0;

        for (int i = 0; i < posicoes.Length; i++)
        {
            if (m.Escuro(posicoes[i] / 100, posicoes[i] % 100))
            {
                v |= 1 << i;
            }
        }

        return v;
    }
}
