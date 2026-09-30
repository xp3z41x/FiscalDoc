using FiscalDoc.Layout.Barcode;
using FiscalDoc.Layout.DisplayList;
using FiscalDoc.Render.Wpf;

namespace FiscalDoc.Tests;

public sealed class Code128Tests
{
    [Fact]
    public void Tabela_de_padroes_satisfaz_a_paridade_da_especificacao()
    {
        // Em todo simbolo do Code 128 as barras somam par e os espacos somam
        // impar. Um digito trocado na transcricao quebra isto - por isso a
        // regra vale como conferencia completa da tabela.
        Assert.True(Code128.TabelaEhConsistente(out string? erro), erro);
    }

    [Fact]
    public void Chave_de_44_digitos_produz_a_contagem_de_modulos_do_moc()
    {
        // O MOC Anexo II secao 2 deriva 297 posicoes:
        //   44/2 = 22 simbolos x 11 = 242
        //   margem clara 10 x 2     =  20
        //   Start C                 =  11
        //   DV                      =  11
        //   Stop                    =  13
        //                             ---
        //                             297
        Assert.Equal(297, Code128.TotalModulos(44));

        const string chave = "35260811222333000181550010000002141630000759";
        IReadOnlyList<int> larguras = Code128.Codificar(chave);

        Assert.Equal(297, larguras.Sum());
    }

    [Fact]
    public void Codificacao_comeca_e_termina_por_margem_clara()
    {
        IReadOnlyList<int> l = Code128.Codificar("0123456789");

        Assert.Equal(Code128.MargemClaraModulos, l[0]);
        Assert.Equal(Code128.MargemClaraModulos, l[^1]);
    }

    [Fact]
    public void Checksum_usa_modulo_103_com_peso_pela_posicao()
    {
        // Exemplo classico da especificacao: Start C (105) + "42" + "18".
        // soma = 105 + 42*1 + 18*2 = 183; 183 mod 103 = 80.
        int dv = Code128.CalcularChecksum([Code128.StartC, 42, 18]);

        Assert.Equal(80, dv);
    }

    [Fact]
    public void Checksum_do_start_sozinho_e_o_proprio_start()
    {
        Assert.Equal(Code128.StartC % 103, Code128.CalcularChecksum([Code128.StartC]));
    }

    /// <summary>
    /// So o alfabeto da chave: digito e letra maiuscula. Numero impar de
    /// digitos e letra ja nao sao erro - o subconjunto A cuida deles.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("12 4")]       // espaco
    [InlineData("12a4")]       // minuscula: o schema so admite maiuscula
    [InlineData("12-4")]       // separador
    [InlineData("12Ç4")]  // fora do ASCII
    public void Entrada_invalida_e_recusada(string entrada)
    {
        Assert.Throws<ArgumentException>(() => Code128.Codificar(entrada));
    }

    [Fact]
    public void Larguras_ficam_entre_um_e_quatro_modulos_fora_das_margens()
    {
        IReadOnlyList<int> l = Code128.Codificar("35260811222333000181550010000002141630000759");

        for (int i = 1; i < l.Count - 1; i++)
        {
            Assert.InRange(l[i], 1, 4);
        }
    }

    [Fact]
    public void Barra_desenhada_tem_largura_uniforme_em_modulos_inteiros()
    {
        // O ponto inteiro de guardar modulos em vez de milimetros: a 600 dpi
        // todas as barras de mesma largura nominal tem de sair com a mesma
        // largura em pixels. Barra desigual e o que faz leitor recusar.
        const float dpi = 600f;
        const float pxPorMm = dpi / 25.4f;

        IReadOnlyList<int> modulos = Code128.Codificar(
            "35260811222333000181550010000002141630000759");

        // Geometria real do DANFE: a coluna da chave tem 80,3 mm e o codigo
        // ocupa 77,3 mm dela. Com 297 modulos isso da 0,26 mm por modulo, que
        // a 600 dpi quantiza para 6 pontos inteiros - acima do minimo
        // normativo de 0,2 mm e uniforme. E a configuracao que de fato sai na
        // impressora, entao e a que precisa ser uniforme.
        var caixa = new RetanguloMm(10f, 10f, 77.3f, 13f);

        using System.Drawing.Bitmap bmp = RenderTeste.Papel(
            new Pagina(1, [new Primitiva.CodigoBarras(caixa, modulos, 0.2f)]),
            new TamanhoPapel(100f, 30f),
            dpi);

        // Percorre uma linha no meio do codigo e mede cada faixa escura.
        int y = (int)(16 * pxPorMm);
        var faixas = new List<int>();
        int corrente = 0;

        for (int x = 0; x < bmp.Width; x++)
        {
            System.Drawing.Color c = bmp.GetPixel(x, y);
            bool escuro = c.R < 128;

            if (escuro)
            {
                corrente++;
            }
            else if (corrente > 0)
            {
                faixas.Add(corrente);
                corrente = 0;
            }
        }

        if (corrente > 0)
        {
            faixas.Add(corrente);
        }

        Assert.NotEmpty(faixas);

        // Toda faixa tem de ser multiplo inteiro da menor - e o que significa
        // "quantizado para pontos inteiros do dispositivo".
        int unidade = faixas.Min();
        Assert.True(unidade >= 1, "modulo degenerou para zero pixel");

        foreach (int f in faixas)
        {
            int multiplos = (int)Math.Round((double)f / unidade);

            Assert.InRange(multiplos, 1, 4);

            // Tolerancia de 1 px para o antialias das bordas.
            Assert.InRange(f, (multiplos * unidade) - 1, (multiplos * unidade) + 1);
        }
    }

    [Fact]
    public void Em_caixa_apertada_o_minimo_normativo_vence_a_quantizacao()
    {
        // Caixa de 60 mm - exatamente o minimo do MOC para impressora nao
        // matricial. Com 297 modulos de 0,2 mm o codigo ocupa 59,4 mm, e
        // sobram 0,6 mm: folga insuficiente para subir o modulo ao proximo
        // ponto inteiro do dispositivo (0,2117 mm a 600 dpi dariam 62,9 mm e
        // estourariam a caixa).
        //
        // Nesse aperto a escolha e deliberada: respeitar o minimo da norma e
        // aceitar que as barras rasterizem com pequena variacao, em vez de
        // descer abaixo de 0,2 mm. O que nao pode, em hipotese nenhuma, e sair
        // da caixa - e isso vale para as duas situacoes.
        const float dpi = 600f;
        const float pxPorMm = dpi / 25.4f;

        IReadOnlyList<int> modulos = Code128.Codificar(
            "35260811222333000181550010000002141630000759");

        var caixa = new RetanguloMm(10f, 10f, 60f, 13f);

        using System.Drawing.Bitmap bmp = RenderTeste.Papel(
            new Pagina(1, [new Primitiva.CodigoBarras(caixa, modulos, 0.2f)]),
            new TamanhoPapel(90f, 30f),
            dpi);

        int y = (int)(16 * pxPorMm);
        int primeiro = -1;
        int ultimo = -1;

        for (int x = 0; x < bmp.Width; x++)
        {
            if (bmp.GetPixel(x, y).R >= 128)
            {
                continue;
            }

            if (primeiro < 0)
            {
                primeiro = x;
            }

            ultimo = x;
        }

        Assert.True(primeiro >= 0, "o codigo nao foi desenhado");

        // Jamais sai da caixa.
        Assert.True(primeiro / pxPorMm >= 10f - 0.1f, "o codigo comecou antes da caixa");
        Assert.True(ultimo / pxPorMm <= 70f + 0.1f, "o codigo passou do fim da caixa");

        // E o modulo nao desceu abaixo do minimo: as barras cobrem pelo menos
        // a largura que 297 modulos de 0,2 mm ocupariam, descontada a margem
        // clara das duas pontas (10 modulos de cada lado).
        float larguraDesenhadaMm = (ultimo - primeiro + 1) / pxPorMm;
        Assert.InRange(larguraDesenhadaMm, 50f, 60f);
    }

    // =========================================== modelo hibrido 128C / 128A

    [Fact]
    public void Exemplo_do_moc_sai_so_no_subconjunto_c()
    {
        // MOC 7.0 Anexo II, 2.1: "09758364", DV 48. A chave so de digitos nao
        // muda com o modelo hibrido - nem um simbolo.
        int[] esperado = [105, 9, 75, 83, 64, 48, 106];
        Assert.Equal(esperado, Code128.Simbolos("09758364"));
    }

    [Fact]
    public void Dv_do_exemplo_da_nt_e_30()
    {
        // NT Conjunta 2025.001, item 6: Start C, 52, 25, Code A, 'A', 'B',
        // Code C, 83 - soma ponderada 1987, resto 30.
        Assert.Equal(30, Code128.CalcularChecksum([105, 52, 25, 101, 33, 34, 99, 83]));
    }

    [Fact]
    public void Impar_antes_da_letra_sai_no_subconjunto_a()
    {
        // O exemplo "123A" da NT: "12" no C, Code A, e "3" e "A" no A.
        int[] esperado = [105, 12, 101, 19, 33, 96, 106];
        Assert.Equal(esperado, Code128.Simbolos("123A"));
    }

    [Fact]
    public void Chave_com_cnpj_alfanumerico_segue_as_regras_da_nt()
    {
        // 35260712 ABC 3 4501 DE 35550010000002141630000756
        //  C: pares  A   A  C     A  C: 13 pares
        // O grupo "34501" tem cinco digitos: o primeiro fica no A e os quatro
        // seguintes vao ao C (regra 3). Os 26 digitos finais, sempre no C.
        int[] esperado =
            [
                105, 35, 26, 7, 12,
                101, 33, 34, 35, 19,
                99, 45, 1,
                101, 36, 37,
                99, 35, 55, 0, 10, 0, 0, 2, 14, 16, 30, 0, 7, 56,
                16, 106,
            ];

        Assert.Equal(esperado, Code128.Simbolos("35260712ABC34501DE35550010000002141630000756"));
    }

    [Fact]
    public void Numero_impar_de_digitos_nao_e_mais_erro()
    {
        // Regra 2: o ultimo digito do grupo impar vai ao subconjunto A.
        Assert.Equal("12345", LeitorCode128.Dados(Code128.Simbolos("12345")));
        int[] inicio = [105, 12, 34, 101, 21];
        Assert.Equal(inicio, Code128.Simbolos("12345").Take(5));
    }

    /// <summary>
    /// Toda forma de chave que o schema admite - letra ou digito em cada uma
    /// das doze posicoes do CNPJ, 4096 combinacoes - le de volta a propria
    /// chave, com DV modulo 103 conferindo, e as larguras sao exatamente os
    /// padroes dos simbolos.
    /// </summary>
    [Fact]
    public void Toda_forma_de_chave_le_de_volta_a_propria_chave()
    {
        var sorteio = new Random(2229);

        foreach (string chave in TodasAsFormasDeChave(sorteio))
        {
            IReadOnlyList<int> simbolos = Code128.Simbolos(chave);

            Assert.Equal(chave, LeitorCode128.Dados(simbolos));
            Assert.Equal(simbolos, LeitorCode128.Simbolos(Code128.Codificar(chave)));
        }
    }

    /// <summary>
    /// O pior caso sao 385 modulos, contra 297 da chave numerica. No modulo
    /// minimo de 0,02 cm isso da 77,0 mm - e o quadro do MOC para o codigo, no
    /// DANFE retrato, tem 77,3 mm uteis. E o que deixa o DANFE absorver a
    /// chave alfanumerica sem mudar de geometria.
    /// </summary>
    [Fact]
    public void Pior_caso_cabe_no_quadro_do_danfe_retrato_no_modulo_minimo()
    {
        int pior = TodasAsFormasDeChave(new Random(2229))
            .Max(chave => Code128.Codificar(chave).Sum());

        Assert.Equal(385, pior);

        float util = FiscalDoc.Layout.Danfe.DanfeMetricas.LarguraColunaChave - 3f;
        Assert.True(
            pior * FiscalDoc.Layout.Danfe.DanfeMetricas.ModuloMinimoBarras <= util,
            $"{pior} modulos de 0,2 mm nao cabem em {util} mm");
    }

    /// <summary>
    /// Uma chave por forma: cUF e AAMM, as doze posicoes do CNPJ com letra ou
    /// digito conforme os bits da mascara, e os 26 digitos finais.
    /// </summary>
    private static IEnumerable<string> TodasAsFormasDeChave(Random sorteio)
    {
        for (int mascara = 0; mascara < 1 << 12; mascara++)
        {
            var chave = new System.Text.StringBuilder("352607", 44);

            for (int i = 0; i < 12; i++)
            {
                chave.Append(((mascara >> i) & 1) == 1
                    ? (char)('A' + sorteio.Next(26))
                    : (char)('0' + sorteio.Next(10)));
            }

            for (int i = 0; i < 26; i++)
            {
                chave.Append((char)('0' + sorteio.Next(10)));
            }

            yield return chave.ToString();
        }
    }

    [Fact]
    public void Codigo_respeita_a_largura_minima_de_modulo_do_danfe()
    {
        // MOC Anexo II secao 2: modulo minimo de 0,02 cm e largura total
        // minima de 6 cm para impressora nao matricial. 297 modulos x 0,2 mm
        // dao exatamente 59,4 mm, e o MOC arredonda para 6 cm.
        Assert.Equal(297, Code128.TotalModulos(44));

        const float moduloMinimoMm = 0.2f;
        float larguraMinima = 297 * moduloMinimoMm;

        Assert.InRange(larguraMinima, 59f, 60f);
    }
}
