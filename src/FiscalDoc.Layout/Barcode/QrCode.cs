using System.Text;

namespace FiscalDoc.Layout.Barcode;

/// <summary>
/// Matriz de modulos de um simbolo QR, sem margem clara.
///
/// Como no <see cref="Code128C"/>, o que sai daqui e <b>modulo</b>, e nao
/// milimetro: quem decide quantos pontos do dispositivo vale um modulo e o
/// renderizador, que conhece o DPI real. Um QR rasterizado em bitmap e depois
/// reescalado sai com modulos de larguras desiguais, e leitor de celular
/// recusa exatamente isso.
/// </summary>
public sealed class MatrizQr
{
    private readonly bool[] _escuro;

    internal MatrizQr(int versao, int tamanho, bool[] escuro)
    {
        Versao = versao;
        Tamanho = tamanho;
        _escuro = escuro;
    }

    /// <summary>Versao do simbolo, de 1 a 40. Define o lado: 17 + 4 x versao.</summary>
    public int Versao { get; }

    /// <summary>Lado da matriz em modulos.</summary>
    public int Tamanho { get; }

    /// <summary>Modulo escuro (true) ou claro, origem no canto superior esquerdo.</summary>
    public bool Escuro(int x, int y) => _escuro[(y * Tamanho) + x];

    /// <summary>
    /// Margem clara (<i>quiet zone</i>) de cada lado, em modulos.
    ///
    /// <para>Duas exigencias se somam aqui. A ISO/IEC 18004 manda 4 modulos.
    /// O Manual de Especificacoes Tecnicas do DANFE NFC-e (v6.0, item 3.2)
    /// manda 3 mm numa imagem de 25 mm e, "para dimensoes superiores a 25mm,
    /// considerar a margem segura de 10% da dimensao total".</para>
    ///
    /// <para>10% da dimensao total de cada lado quer dizer
    /// <c>q / (n + 2q) &gt;= 0,10</c>, ou seja <c>q &gt;= n/8</c>. O valor
    /// devolvido e o menor inteiro que satisfaz as duas regras ao mesmo tempo,
    /// e por isso a margem sai certa em qualquer tamanho impresso - nao ha um
    /// numero de milimetros escondido no layout.</para>
    /// </summary>
    public int MargemModulos => Math.Max(4, (Tamanho + 7) / 8);
}

/// <summary>
/// Codificador de QR Code (ISO/IEC 18004), modo binario, nivel de correcao M.
///
/// <para><b>Por que so o nivel M.</b> O Manual de Especificacoes Tecnicas do
/// DANFE NFC-e (v6.0, item 5.2.2) e categorico: "Para o QR Code do DANFE
/// NFC-e sera utilizado Nivel M". Implementar os quatro niveis exigiria
/// transcrever mais 120 numeros de tabela que nunca seriam exercitados - e
/// cada numero transcrito e um erro possivel.</para>
///
/// <para><b>Por que modo binario em UTF-8.</b> Mesmo manual, item 5.2.3:
/// "Para o QR Code da NFC-e sera utilizada a opcao 2 - UTF-8". A URL do
/// campo <c>qrCode</c> e ASCII na pratica, mas a regra e a regra.</para>
///
/// <para><b>Por que a mao, e nao por biblioteca.</b> Pelo mesmo motivo do
/// Code 128: biblioteca devolve bitmap, e bitmap reescalado para 600 dpi sai
/// com modulos desiguais. Ver <see cref="Code128C"/>.</para>
///
/// <para>As duas tabelas transcritas se autovalidam contra a formula de
/// contagem de modulos da norma - ver <see cref="TabelasSaoConsistentes"/>.</para>
/// </summary>
public static class QrCode
{
    /// <summary>Indicador de modo binario (byte), 4 bits.</summary>
    private const int ModoBinario = 0b0100;

    /// <summary>
    /// Bits de nivel de correcao no campo de formato. L=1, M=0, Q=3, H=2 -
    /// repare que a ordem <b>nao</b> e a ordem de robustez.
    /// </summary>
    private const int BitsNivelM = 0;

    /// <summary>Polinomio primitivo de GF(2^8) usado pelo QR: x^8+x^4+x^3+x^2+1.</summary>
    private const int PolinomioGf = 0x11D;

    /// <summary>
    /// Codewords de correcao por bloco, nivel M, versoes 1 a 40
    /// (ISO/IEC 18004, tabelas 13 a 22). O indice 0 nao existe.
    /// </summary>
    private static readonly int[] CorrecaoPorBloco =
    [
        -1,
        10, 16, 26, 18, 24, 16, 18, 22, 22, 26,
        30, 22, 22, 24, 24, 28, 28, 26, 26, 26,
        26, 28, 28, 28, 28, 28, 28, 28, 28, 28,
        28, 28, 28, 28, 28, 28, 28, 28, 28, 28,
    ];

    /// <summary>Quantidade de blocos de correcao, nivel M, versoes 1 a 40.</summary>
    private static readonly int[] BlocosPorVersao =
    [
        -1,
        1, 1, 1, 2, 2, 4, 4, 4, 5, 5,
        5, 8, 9, 9, 10, 10, 11, 13, 14, 16,
        17, 17, 18, 20, 21, 23, 25, 26, 28, 29,
        31, 33, 35, 37, 38, 40, 43, 45, 47, 49,
    ];

    /// <summary>Codifica o texto no menor simbolo que o comporte.</summary>
    /// <exception cref="ArgumentException">Nao cabe nem na versao 40.</exception>
    public static MatrizQr Codificar(string texto)
    {
        ArgumentNullException.ThrowIfNull(texto);

        byte[] dados = Encoding.UTF8.GetBytes(texto);
        int versao = EscolherVersao(dados.Length);

        if (versao < 0)
        {
            throw new ArgumentException(
                $"texto de {dados.Length} bytes nao cabe num QR Code nivel M "
                + $"(maximo {CapacidadeDadosBytes(40)} bytes, na versao 40).",
                nameof(texto));
        }

        byte[] codewords = MontarCodewords(dados, versao);
        byte[] comCorrecao = IntercalarComCorrecao(codewords, versao);

        return Desenhar(versao, comCorrecao);
    }

    /// <summary>Menor versao cujo espaco de dados comporta tantos bytes, ou -1.</summary>
    public static int EscolherVersao(int quantidadeBytes)
    {
        for (int v = 1; v <= 40; v++)
        {
            if (quantidadeBytes <= CapacidadeDadosBytes(v))
            {
                return v;
            }
        }

        return -1;
    }

    /// <summary>
    /// Bytes de carga util numa versao, no nivel M, ja descontando o indicador
    /// de modo (4 bits) e o contador de caracteres (8 ou 16 bits).
    /// </summary>
    public static int CapacidadeDadosBytes(int versao)
    {
        int bitsCabecalho = 4 + BitsDoContador(versao);
        return CodewordsDeDados(versao) - ((bitsCabecalho + 7) / 8);
    }

    /// <summary>
    /// Total de codewords (dados mais correcao) de uma versao, derivado da
    /// contagem de modulos da norma - e nao transcrito de tabela.
    ///
    /// <para>Sao os modulos totais menos os de funcao: tres localizadores com
    /// separador, os dois padroes de sincronismo, os alinhamentos (ja
    /// descontada a sobreposicao com o sincronismo) e, da versao 7 em diante,
    /// os dois blocos de informacao de versao.</para>
    /// </summary>
    public static int CodewordsTotais(int versao)
    {
        int bits = (((16 * versao) + 128) * versao) + 64;

        if (versao >= 2)
        {
            int alinhamentos = (versao / 7) + 2;
            bits -= (((25 * alinhamentos) - 10) * alinhamentos) - 55;

            if (versao >= 7)
            {
                bits -= 36;
            }
        }

        return bits / 8;
    }

    /// <summary>Codewords de dados de uma versao, no nivel M.</summary>
    public static int CodewordsDeDados(int versao) =>
        CodewordsTotais(versao) - (CorrecaoPorBloco[versao] * BlocosPorVersao[versao]);

    /// <summary>Lado da matriz, em modulos.</summary>
    public static int TamanhoDaVersao(int versao) => (4 * versao) + 17;

    /// <summary>
    /// Autoteste das tabelas transcritas, no mesmo espirito do
    /// <see cref="Code128C.TabelaEhConsistente"/>: as duas tabelas so fazem
    /// sentido se, em toda versao, os blocos couberem exatamente no total de
    /// codewords que a formula da norma preve, com pelo menos um byte de dados
    /// por bloco, com blocos curtos e longos diferindo em no maximo um byte, e
    /// com a capacidade crescendo de uma versao para a seguinte. Um numero
    /// trocado na transcricao quebra alguma dessas condicoes.
    /// </summary>
    public static bool TabelasSaoConsistentes(out string? erro)
    {
        if (CorrecaoPorBloco.Length != 41 || BlocosPorVersao.Length != 41)
        {
            erro = "as tabelas deveriam ter 41 entradas: o indice 0 inexistente e as versoes 1 a 40";
            return false;
        }

        int capacidadeAnterior = 0;

        for (int v = 1; v <= 40; v++)
        {
            int total = CodewordsTotais(v);
            int blocos = BlocosPorVersao[v];
            int correcao = CorrecaoPorBloco[v];
            int dados = total - (blocos * correcao);

            if (blocos < 1 || correcao < 1)
            {
                erro = $"versao {v}: blocos={blocos}, correcao={correcao}";
                return false;
            }

            if (dados < blocos)
            {
                erro = $"versao {v}: {dados} codewords de dados para {blocos} blocos";
                return false;
            }

            int curtos = dados / blocos;
            int longos = dados % blocos == 0 ? curtos : curtos + 1;

            if (longos - curtos > 1)
            {
                erro = $"versao {v}: blocos de {curtos} e {longos} bytes";
                return false;
            }

            int capacidade = CapacidadeDadosBytes(v);

            if (capacidade <= capacidadeAnterior)
            {
                erro = $"versao {v}: capacidade {capacidade} nao cresce sobre {capacidadeAnterior}";
                return false;
            }

            capacidadeAnterior = capacidade;
        }

        erro = null;
        return true;
    }

    /// <summary>
    /// Contador de caracteres do modo binario: 8 bits ate a versao 9, 16 bits
    /// da 10 em diante (ISO/IEC 18004, tabela 3).
    /// </summary>
    private static int BitsDoContador(int versao) => versao <= 9 ? 8 : 16;

    // =====================================================================
    // Fluxo de bits
    // =====================================================================

    private static byte[] MontarCodewords(byte[] dados, int versao)
    {
        int capacidadeBits = CodewordsDeDados(versao) * 8;
        var bits = new ListaDeBits(capacidadeBits);

        bits.Acrescentar(ModoBinario, 4);
        bits.Acrescentar(dados.Length, BitsDoContador(versao));

        foreach (byte b in dados)
        {
            bits.Acrescentar(b, 8);
        }

        // Terminador: ate quatro zeros, o que couber.
        bits.Acrescentar(0, Math.Min(4, capacidadeBits - bits.Quantidade));

        // Completa o byte corrente.
        bits.Acrescentar(0, (8 - (bits.Quantidade % 8)) % 8);

        // Enchimento alternado 11101100 / 00010001, da norma.
        for (int enchimento = 0xEC; bits.Quantidade < capacidadeBits; enchimento ^= 0xEC ^ 0x11)
        {
            bits.Acrescentar(enchimento, 8);
        }

        return bits.ParaBytes();
    }

    /// <summary>
    /// Divide os dados em blocos, calcula a correcao de cada um e intercala
    /// byte a byte, que e a ordem em que a norma manda gravar.
    ///
    /// A intercalacao existe para que uma mancha no papel destrua um byte de
    /// cada bloco em vez de um bloco inteiro - a correcao de erro trabalha por
    /// bloco, entao o dano espalhado e recuperavel e o dano concentrado nao.
    /// </summary>
    private static byte[] IntercalarComCorrecao(byte[] dados, int versao)
    {
        int blocos = BlocosPorVersao[versao];
        int correcao = CorrecaoPorBloco[versao];
        int totalCodewords = CodewordsTotais(versao);
        int bytesCurtos = (totalCodewords / blocos) - correcao;
        int blocosCurtos = blocos - (totalCodewords % blocos);

        byte[] divisor = GerarDivisor(correcao);

        var blocosDados = new byte[blocos][];
        var blocosCorrecao = new byte[blocos][];

        int posicao = 0;

        for (int i = 0; i < blocos; i++)
        {
            int tamanho = bytesCurtos + (i < blocosCurtos ? 0 : 1);

            var bloco = new byte[tamanho];
            Array.Copy(dados, posicao, bloco, 0, tamanho);
            posicao += tamanho;

            blocosDados[i] = bloco;
            blocosCorrecao[i] = RestoReedSolomon(bloco, divisor);
        }

        var saida = new byte[totalCodewords];
        int escrita = 0;

        // Dados, coluna a coluna. O bloco curto nao tem a ultima coluna.
        for (int coluna = 0; coluna <= bytesCurtos; coluna++)
        {
            for (int i = 0; i < blocos; i++)
            {
                if (coluna < blocosDados[i].Length)
                {
                    saida[escrita++] = blocosDados[i][coluna];
                }
            }
        }

        // Correcao: aqui todos os blocos tem o mesmo tamanho.
        for (int coluna = 0; coluna < correcao; coluna++)
        {
            for (int i = 0; i < blocos; i++)
            {
                saida[escrita++] = blocosCorrecao[i][coluna];
            }
        }

        return saida;
    }

    // =====================================================================
    // Reed-Solomon sobre GF(2^8)
    // =====================================================================

    /// <summary>Produto em GF(2^8) pelo polinomio primitivo do QR.</summary>
    public static byte MultiplicarGf(byte a, byte b)
    {
        int z = 0;

        for (int i = 7; i >= 0; i--)
        {
            z = (z << 1) ^ ((z >>> 7) * PolinomioGf);
            z ^= ((b >>> i) & 1) * a;
        }

        return (byte)z;
    }

    /// <summary>
    /// Polinomio gerador de grau <paramref name="grau"/>, que e o produto de
    /// (x - a^0)(x - a^1) ... (x - a^(grau-1)) com a = 2. Devolvido sem o
    /// termo lider, que vale sempre 1.
    /// </summary>
    public static byte[] GerarDivisor(int grau)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(grau, 1);

        var resultado = new byte[grau];
        resultado[grau - 1] = 1;

        byte raiz = 1;

        for (int i = 0; i < grau; i++)
        {
            for (int j = 0; j < grau; j++)
            {
                resultado[j] = MultiplicarGf(resultado[j], raiz);

                if (j + 1 < grau)
                {
                    resultado[j] ^= resultado[j + 1];
                }
            }

            raiz = MultiplicarGf(raiz, 0x02);
        }

        return resultado;
    }

    /// <summary>Resto da divisao dos dados pelo polinomio gerador.</summary>
    public static byte[] RestoReedSolomon(IReadOnlyList<byte> dados, IReadOnlyList<byte> divisor)
    {
        ArgumentNullException.ThrowIfNull(dados);
        ArgumentNullException.ThrowIfNull(divisor);

        var resto = new byte[divisor.Count];

        foreach (byte b in dados)
        {
            byte fator = (byte)(b ^ resto[0]);

            Array.Copy(resto, 1, resto, 0, resto.Length - 1);
            resto[^1] = 0;

            for (int j = 0; j < resto.Length; j++)
            {
                resto[j] ^= MultiplicarGf(divisor[j], fator);
            }
        }

        return resto;
    }

    // =====================================================================
    // Montagem da matriz
    // =====================================================================

    private static MatrizQr Desenhar(int versao, byte[] codewords)
    {
        var tela = new Tela(versao);

        tela.DesenharFuncoes();
        tela.GravarCodewords(codewords);

        // Oito mascaras, uma escolhida por penalidade. Todas produzem um
        // simbolo valido; a penalidade so evita manchas que atrapalham o
        // leitor (ISO/IEC 18004, 7.8.3).
        int melhorMascara = 0;
        int melhorPenalidade = int.MaxValue;

        for (int mascara = 0; mascara < 8; mascara++)
        {
            tela.AplicarMascara(mascara);
            tela.GravarFormato(mascara);

            int penalidade = tela.Penalidade();

            if (penalidade < melhorPenalidade)
            {
                melhorPenalidade = penalidade;
                melhorMascara = mascara;
            }

            // A mascara e XOR: aplicar de novo desfaz.
            tela.AplicarMascara(mascara);
        }

        tela.AplicarMascara(melhorMascara);
        tela.GravarFormato(melhorMascara);

        return tela.Materializar();
    }

    private static bool Bit(int valor, int posicao) => ((valor >>> posicao) & 1) != 0;

    /// <summary>Acumulador de bits que vira o fluxo de codewords.</summary>
    private sealed class ListaDeBits
    {
        private readonly List<byte> _bytes;
        private int _parcial;
        private int _usados;

        internal ListaDeBits(int capacidadeBits)
        {
            _bytes = new List<byte>((capacidadeBits / 8) + 1);
        }

        internal int Quantidade => (_bytes.Count * 8) + _usados;

        internal void Acrescentar(int valor, int bits)
        {
            for (int i = bits - 1; i >= 0; i--)
            {
                _parcial = (_parcial << 1) | ((valor >>> i) & 1);
                _usados++;

                if (_usados == 8)
                {
                    _bytes.Add((byte)_parcial);
                    _parcial = 0;
                    _usados = 0;
                }
            }
        }

        internal byte[] ParaBytes() => [.. _bytes];
    }

    /// <summary>
    /// A matriz em construcao. Separa modulo de funcao (localizador,
    /// sincronismo, alinhamento, formato, versao) de modulo de dados: so o de
    /// dados recebe mascara, e so ele aceita codeword.
    /// </summary>
    private sealed class Tela
    {
        private readonly bool[] _escuro;
        private readonly bool[] _funcao;
        private readonly int _versao;
        private readonly int _n;

        internal Tela(int versao)
        {
            _versao = versao;
            _n = TamanhoDaVersao(versao);
            _escuro = new bool[_n * _n];
            _funcao = new bool[_n * _n];
        }

        internal MatrizQr Materializar() => new(_versao, _n, _escuro);

        internal void DesenharFuncoes()
        {
            // Sincronismo: linha e coluna 6, alternando a partir de escuro.
            for (int i = 0; i < _n; i++)
            {
                PorFuncao(6, i, i % 2 == 0);
                PorFuncao(i, 6, i % 2 == 0);
            }

            // Localizadores nos tres cantos, com separador.
            Localizador(3, 3);
            Localizador(_n - 4, 3);
            Localizador(3, _n - 4);

            int[] posicoes = PosicoesDeAlinhamento();

            for (int i = 0; i < posicoes.Length; i++)
            {
                for (int j = 0; j < posicoes.Length; j++)
                {
                    // Os tres cantos ja tem localizador.
                    bool cantoComLocalizador =
                        (i == 0 && j == 0)
                        || (i == 0 && j == posicoes.Length - 1)
                        || (i == posicoes.Length - 1 && j == 0);

                    if (!cantoComLocalizador)
                    {
                        Alinhamento(posicoes[i], posicoes[j]);
                    }
                }
            }

            // Reserva a area do formato; o conteudo e gravado de novo depois,
            // junto com a mascara escolhida.
            GravarFormato(0);
            GravarVersao();
        }

        /// <summary>
        /// Grava as duas copias da informacao de formato: nivel de correcao e
        /// mascara, protegidos por um BCH(15,5) e embaralhados por 0x5412.
        /// </summary>
        internal void GravarFormato(int mascara)
        {
            int dados = (BitsNivelM << 3) | mascara;
            int resto = dados;

            for (int i = 0; i < 10; i++)
            {
                resto = (resto << 1) ^ ((resto >>> 9) * 0x537);
            }

            int bits = ((dados << 10) | resto) ^ 0x5412;

            // Primeira copia, em volta do localizador superior esquerdo.
            for (int i = 0; i <= 5; i++)
            {
                PorFuncao(8, i, Bit(bits, i));
            }

            PorFuncao(8, 7, Bit(bits, 6));
            PorFuncao(8, 8, Bit(bits, 7));
            PorFuncao(7, 8, Bit(bits, 8));

            for (int i = 9; i < 15; i++)
            {
                PorFuncao(14 - i, 8, Bit(bits, i));
            }

            // Segunda copia, partida entre os outros dois localizadores.
            for (int i = 0; i < 8; i++)
            {
                PorFuncao(_n - 1 - i, 8, Bit(bits, i));
            }

            for (int i = 8; i < 15; i++)
            {
                PorFuncao(8, _n - 15 + i, Bit(bits, i));
            }

            // Modulo sempre escuro, logo acima da copia inferior.
            PorFuncao(8, _n - 8, true);
        }

        /// <summary>
        /// Percorre a matriz em colunas de duas, alternando o sentido, e grava
        /// os bits nos modulos que nao sao de funcao. A coluna 6 e pulada
        /// porque e o sincronismo vertical.
        /// </summary>
        internal void GravarCodewords(byte[] codewords)
        {
            int i = 0;
            int totalBits = codewords.Length * 8;

            for (int direita = _n - 1; direita >= 1; direita -= 2)
            {
                if (direita == 6)
                {
                    direita = 5;
                }

                for (int vertical = 0; vertical < _n; vertical++)
                {
                    for (int j = 0; j < 2; j++)
                    {
                        int x = direita - j;
                        bool subindo = ((direita + 1) & 2) == 0;
                        int y = subindo ? _n - 1 - vertical : vertical;

                        if (!EhFuncao(x, y) && i < totalBits)
                        {
                            Por(x, y, Bit(codewords[i >>> 3], 7 - (i & 7)));
                            i++;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Aplica - ou desfaz, por ser XOR - uma das oito mascaras da norma
        /// sobre os modulos de dados.
        /// </summary>
        internal void AplicarMascara(int mascara)
        {
            for (int y = 0; y < _n; y++)
            {
                for (int x = 0; x < _n; x++)
                {
                    if (EhFuncao(x, y))
                    {
                        continue;
                    }

                    bool inverter = mascara switch
                    {
                        0 => (x + y) % 2 == 0,
                        1 => y % 2 == 0,
                        2 => x % 3 == 0,
                        3 => (x + y) % 3 == 0,
                        4 => ((x / 3) + (y / 2)) % 2 == 0,
                        5 => ((x * y) % 2) + ((x * y) % 3) == 0,
                        6 => ((((x * y) % 2) + ((x * y) % 3)) % 2) == 0,
                        7 => ((((x + y) % 2) + ((x * y) % 3)) % 2) == 0,
                        _ => false,
                    };

                    if (inverter)
                    {
                        Por(x, y, !Em(x, y));
                    }
                }
            }
        }

        /// <summary>
        /// Penalidade das quatro regras de 7.8.3.1. Serve unicamente para
        /// escolher entre as oito mascaras - qualquer uma delas produz um
        /// simbolo valido, e nenhuma decisao de leitura depende deste numero.
        /// </summary>
        internal int Penalidade()
        {
            int total = 0;

            // Regra 1: corridas de 5 ou mais modulos iguais, em linha e em
            // coluna. 3 pontos pela corrida de 5, mais 1 por modulo extra.
            for (int i = 0; i < _n; i++)
            {
                total += PenalidadeDeCorrida(i, horizontal: true);
                total += PenalidadeDeCorrida(i, horizontal: false);
            }

            // Regra 2: blocos 2x2 da mesma cor, 3 pontos cada.
            for (int y = 0; y < _n - 1; y++)
            {
                for (int x = 0; x < _n - 1; x++)
                {
                    bool c = Em(x, y);

                    if (c == Em(x + 1, y) && c == Em(x, y + 1) && c == Em(x + 1, y + 1))
                    {
                        total += 3;
                    }
                }
            }

            // Regra 3: sequencia 1:1:3:1:1 com quatro modulos claros de um dos
            // lados - o que imita um localizador. 40 pontos cada.
            for (int i = 0; i < _n; i++)
            {
                total += 40 * PadroesDeLocalizadorFalso(i, horizontal: true);
                total += 40 * PadroesDeLocalizadorFalso(i, horizontal: false);
            }

            // Regra 4: desvio da proporcao de 50% de modulos escuros, 10
            // pontos por faixa de 5 pontos percentuais.
            int escuros = 0;

            foreach (bool m in _escuro)
            {
                if (m)
                {
                    escuros++;
                }
            }

            int celulas = _n * _n;
            int desvio = Math.Abs((escuros * 20) - (celulas * 10)) / celulas;

            return total + (desvio * 10);
        }

        /// <summary>
        /// Informacao de versao, so da versao 7 em diante: 6 bits de versao e
        /// 12 de um BCH(18,6), em dois blocos 3x6 junto aos localizadores.
        /// </summary>
        private void GravarVersao()
        {
            if (_versao < 7)
            {
                return;
            }

            int resto = _versao;

            for (int i = 0; i < 12; i++)
            {
                resto = (resto << 1) ^ ((resto >>> 11) * 0x1F25);
            }

            int bits = (_versao << 12) | resto;

            for (int i = 0; i < 18; i++)
            {
                bool valor = Bit(bits, i);
                int a = _n - 11 + (i % 3);
                int b = i / 3;

                PorFuncao(a, b, valor);
                PorFuncao(b, a, valor);
            }
        }

        private bool Em(int x, int y) => _escuro[(y * _n) + x];

        private void Por(int x, int y, bool valor) => _escuro[(y * _n) + x] = valor;

        private void PorFuncao(int x, int y, bool valor)
        {
            Por(x, y, valor);
            _funcao[(y * _n) + x] = true;
        }

        private bool EhFuncao(int x, int y) => _funcao[(y * _n) + x];

        private void Localizador(int cx, int cy)
        {
            for (int dy = -4; dy <= 4; dy++)
            {
                for (int dx = -4; dx <= 4; dx++)
                {
                    int x = cx + dx;
                    int y = cy + dy;

                    if (x < 0 || x >= _n || y < 0 || y >= _n)
                    {
                        continue;
                    }

                    // Norma do maximo: os aneis a distancia 2 e 4 sao claros.
                    int distancia = Math.Max(Math.Abs(dx), Math.Abs(dy));
                    PorFuncao(x, y, distancia != 2 && distancia != 4);
                }
            }
        }

        private void Alinhamento(int cx, int cy)
        {
            for (int dy = -2; dy <= 2; dy++)
            {
                for (int dx = -2; dx <= 2; dx++)
                {
                    int distancia = Math.Max(Math.Abs(dx), Math.Abs(dy));
                    PorFuncao(cx + dx, cy + dy, distancia != 1);
                }
            }
        }

        /// <summary>
        /// Centros dos padroes de alinhamento (ISO/IEC 18004, tabela E.1). O
        /// primeiro e sempre 6, o ultimo sempre n-7, e os do meio ficam
        /// igualmente espacados - com a excecao conhecida da versao 32.
        /// </summary>
        private int[] PosicoesDeAlinhamento()
        {
            if (_versao == 1)
            {
                return [];
            }

            int quantidade = (_versao / 7) + 2;

            int passo = _versao == 32
                ? 26
                : ((_versao * 4) + (quantidade * 2) + 1) / ((quantidade * 2) - 2) * 2;

            var posicoes = new int[quantidade];
            posicoes[0] = 6;

            for (int i = quantidade - 1, p = _n - 7; i >= 1; i--, p -= passo)
            {
                posicoes[i] = p;
            }

            return posicoes;
        }

        private int PenalidadeDeCorrida(int indice, bool horizontal)
        {
            int total = 0;
            int corrida = 1;

            for (int i = 1; i < _n; i++)
            {
                bool atual = horizontal ? Em(i, indice) : Em(indice, i);
                bool anterior = horizontal ? Em(i - 1, indice) : Em(indice, i - 1);

                if (atual == anterior)
                {
                    corrida++;
                    continue;
                }

                total += PontosDaCorrida(corrida);
                corrida = 1;
            }

            return total + PontosDaCorrida(corrida);
        }

        private static int PontosDaCorrida(int corrida) =>
            corrida < 5 ? 0 : 3 + (corrida - 5);

        /// <summary>
        /// Conta as ocorrencias do nucleo 1:1:3:1:1 (escuro, claro, tres
        /// escuros, claro, escuro) precedido ou seguido de quatro modulos
        /// claros, na linha ou coluna dada.
        ///
        /// <para>A borda do simbolo conta como area clara: a edicao de 2015 da
        /// ISO/IEC 18004 trata o lado de fora como claro, e por isso um nucleo
        /// encostado na borda pontua mesmo sem quatro modulos claros dentro do
        /// simbolo. E o mesmo criterio das implementacoes de referencia.</para>
        ///
        /// <para>Depois de pontuar, a busca recomeca <i>depois</i> do nucleo;
        /// quando nao pontua, recomeca no quarto modulo, que e a primeira
        /// posicao onde outro nucleo poderia comecar sem repetir este.</para>
        /// </summary>
        private int PadroesDeLocalizadorFalso(int indice, bool horizontal)
        {
            int achados = 0;
            int i = 0;

            while (i + 7 <= _n)
            {
                if (!NucleoEm(i, indice, horizontal))
                {
                    i++;
                    continue;
                }

                if (TudoClaro(i - 4, i, indice, horizontal)
                    || TudoClaro(i + 7, i + 11, indice, horizontal))
                {
                    achados++;
                    i += 7;
                }
                else
                {
                    i += 4;
                }
            }

            return achados;
        }

        /// <summary>O nucleo 1011101 comeca nesta posicao?</summary>
        private bool NucleoEm(int inicio, int indice, bool horizontal)
        {
            const int Nucleo = 0b1011101;

            for (int k = 0; k < 7; k++)
            {
                bool esperado = ((Nucleo >>> (6 - k)) & 1) != 0;
                bool real = horizontal ? Em(inicio + k, indice) : Em(indice, inicio + k);

                if (real != esperado)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Todos os modulos do intervalo sao claros? O que cai fora do simbolo
        /// e considerado claro, e o intervalo vazio tambem conta como claro.
        /// </summary>
        private bool TudoClaro(int de, int ate, int indice, bool horizontal)
        {
            for (int k = Math.Max(de, 0); k < Math.Min(ate, _n); k++)
            {
                if (horizontal ? Em(k, indice) : Em(indice, k))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
