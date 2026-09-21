namespace FiscalDoc.Layout.Danfe;

/// <summary>
/// Medidas do DANFE A4 retrato, transcritas do MOC 7.0 Anexo II, secao 3.8.1.
///
/// <para><b>Uma unica nota sobre como a tabela foi aplicada.</b> A tabela do
/// MOC da altura, largura e canto superior esquerdo de cada campo, mas os
/// valores publicados <b>nao fecham</b> ao milimetro: a caixa de identificacao
/// e assinatura do canhoto termina em 16,45 cm e a caixa do numero comeca em
/// 16,35 cm (1 mm de sobreposicao); a terceira linha do destinatario termina em
/// 11,13 cm e o titulo de FATURA comeca em 11,09 cm; a fatura termina em
/// 12,51 cm e o titulo de CALCULO DO IMPOSTO comeca em 12,36 cm.</para>
///
/// <para>Diante disso, foram tomadas como normativas as <b>alturas e
/// larguras</b>, e as posicoes verticais sao <b>empilhadas</b> a partir do
/// topo. O desvio em relacao aos valores publicados fica abaixo de 1,5 mm -
/// menor que as contradicoes da propria tabela - e em troca nenhum quadro
/// se sobrepoe ou deixa vao.</para>
///
/// <para>O quadro de produtos e o unico elastico: recebe o que sobra. Isso
/// tambem e o que o MOC manda, em 3.3.3 (suprimir o ISSQN devolve a altura
/// para os produtos) e em 3.10.3 (reduzir os produtos para absorver margem
/// maior de impressora).</para>
///
/// Nenhuma coordenada do DANFE aparece fora deste arquivo.
/// </summary>
public static class DanfeMetricas
{
    // ---- Folha -------------------------------------------------------------

    /// <summary>Borda esquerda do conteudo: 0,25 cm.</summary>
    public const float Esquerda = 2.5f;

    /// <summary>Borda direita do conteudo: 0,25 + 20,57 = 20,82 cm.</summary>
    public const float Direita = 208.2f;

    /// <summary>Largura util: 20,57 cm.</summary>
    public const float Largura = Direita - Esquerda;

    /// <summary>Borda superior do conteudo: 0,42 cm.</summary>
    public const float Topo = 4.2f;

    /// <summary>Borda inferior do conteudo: 29,40 cm.</summary>
    public const float Base = 294.0f;

    // ---- Alturas padrao ----------------------------------------------------

    /// <summary>Altura de uma linha de campos: 0,85 cm.</summary>
    public const float AlturaLinha = 8.5f;

    /// <summary>Altura da faixa de titulo de bloco: 0,42 cm.</summary>
    public const float AlturaTitulo = 4.2f;

    // ---- Canhoto -----------------------------------------------------------

    /// <summary>Altura total do canhoto: duas linhas de 0,85 cm.</summary>
    public const float AlturaCanhoto = 2 * AlturaLinha;

    /// <summary>Vao entre o canhoto e o corpo, onde fica a linha de destaque.</summary>
    public const float VaoCanhoto = 4.2f;

    /// <summary>Largura da caixa de numero/serie no canhoto: 4,50 cm.</summary>
    public const float LarguraNumeroCanhoto = 45.0f;

    /// <summary>Largura de "DATA DE RECEBIMENTO": 4,10 cm.</summary>
    public const float LarguraDataRecebimento = 41.0f;

    // ---- Cabecalho (variante laser, MOC 3.8.1) -----------------------------

    /// <summary>Quadro de identificacao do emitente: 10,00 cm de largura.</summary>
    public const float LarguraEmitente = 100.0f;

    /// <summary>Quadro da palavra DANFE: 2,54 cm de largura.</summary>
    public const float LarguraQuadroDanfe = 25.4f;

    /// <summary>Altura do bloco emitente / DANFE: 3,92 cm.</summary>
    public const float AlturaEmitente = 39.2f;

    /// <summary>Altura do quadro do codigo de barras: 1,48 cm.</summary>
    public const float AlturaQuadroBarras = 14.8f;

    /// <summary>
    /// Altura util da barra propriamente dita: 1,00 cm no MOC, contra um
    /// minimo normativo de 0,80 cm (Anexo II secao 2).
    /// </summary>
    public const float AlturaBarras = 10.0f;

    /// <summary>Largura minima do codigo de barras em impressora nao matricial: 6 cm.</summary>
    public const float LarguraMinimaBarras = 60.0f;

    /// <summary>Largura minima do modulo: 0,02 cm (Anexo II secao 2).</summary>
    public const float ModuloMinimoBarras = 0.2f;

    /// <summary>
    /// Terceira coluna do cabecalho (barras, chave, consulta, protocolo).
    /// 20,82 - (0,25 + 10,00 + 2,54) = 8,03 cm.
    /// </summary>
    public const float LarguraColunaChave =
        Direita - (Esquerda + LarguraEmitente + LarguraQuadroDanfe);

    /// <summary>Faixa da mensagem de consulta de autenticidade / 2o codigo de barras.</summary>
    public const float AlturaConsulta = 15.9f;

    // ---- Destinatario ------------------------------------------------------

    public const float LarguraDestRazaoSocial = 123.2f;
    public const float LarguraDestCnpj = 53.3f;
    public const float LarguraDestEndereco = 101.6f;
    public const float LarguraDestBairro = 48.3f;
    public const float LarguraDestCep = 26.7f;
    public const float LarguraDestMunicipio = 71.1f;
    public const float LarguraDestFone = 40.6f;
    public const float LarguraDestUf = 11.4f;
    public const float LarguraDestData = 29.2f;

    // ---- Calculo do imposto ------------------------------------------------

    /// <summary>Primeira linha: cinco campos de 4,06 cm (o ultimo fecha a borda).</summary>
    public const float LarguraImpostoLinha1 = 40.6f;

    /// <summary>Segunda linha: seis campos de 3,30 cm (o ultimo fecha a borda).</summary>
    public const float LarguraImpostoLinha2 = 33.0f;

    // ---- IBS / CBS (Reforma Tributária) ------------------------------------

    /// <summary>
    /// Largura dos campos do bloco de IBS/CBS: cinco de 4,06 cm, o mesmo
    /// gabarito da primeira linha de CALCULO DO IMPOSTO. Reaproveitar a
    /// medida mantem as molduras alinhadas verticalmente entre os dois blocos.
    /// </summary>
    public const float LarguraIbsCbs = 40.6f;

    /// <summary>
    /// Custo do bloco: faixa de titulo mais uma linha de campos. Sai do quadro
    /// de produtos, que e o bloco elastico - com ele a primeira pagina passa de
    /// 25 para 21 itens, e o excedente pagina normalmente.
    /// </summary>
    public const float AlturaBlocoIbsCbs = AlturaTitulo + AlturaLinha;

    // ---- Transportador -----------------------------------------------------

    public const float LarguraTranspRazaoSocial = 90.2f;
    public const float LarguraTranspFretePorConta = 27.9f;
    public const float LarguraTranspAntt = 17.8f;
    public const float LarguraTranspPlaca = 22.9f;
    public const float LarguraTranspUf = 7.6f;
    public const float LarguraTranspCnpj = 39.4f;

    public const float LarguraTranspEndereco = 90.2f;
    public const float LarguraTranspMunicipio = 68.6f;
    public const float LarguraTranspIe = 39.4f;

    public const float LarguraVolQuantidade = 29.2f;
    public const float LarguraVolEspecie = 30.5f;
    public const float LarguraVolMarca = 30.5f;
    public const float LarguraVolNumeracao = 48.3f;
    public const float LarguraVolPesoBruto = 34.3f;
    public const float LarguraVolPesoLiquido = 33.0f;

    // ---- ISSQN -------------------------------------------------------------

    public const float LarguraIssqn = 50.8f;

    // ---- Dados adicionais --------------------------------------------------

    /// <summary>Altura do bloco de dados adicionais: 3,07 cm.</summary>
    public const float AlturaDadosAdicionais = 30.7f;

    /// <summary>Informacoes complementares: 12,95 cm.</summary>
    public const float LarguraInfoComplementares = 129.5f;

    // ---- Quadro de produtos ------------------------------------------------

    /// <summary>
    /// Altura minima aceitavel para o quadro de produtos. Abaixo disso o
    /// cabecalho das colunas nao cabe junto com uma linha de item, e vale mais
    /// paginar do que espremer.
    /// </summary>
    public const float AlturaMinimaProdutos = 20.0f;

    /// <summary>Altura do cabecalho das colunas de produtos.</summary>
    public const float AlturaCabecalhoProdutos = 5.4f;

    /// <summary>
    /// Larguras das colunas de produtos, na ordem do MOC. As colunas que o
    /// 3.1.7 proibe suprimir estao todas aqui; as opcionais escolhidas sao as
    /// que aparecem em praticamente todo DANFE do mercado.
    /// </summary>
    public static readonly (string Titulo, float Largura, bool Numerica)[] ColunasProduto =
    [
        ("CÓDIGO", 16.0f, false),
        ("DESCRIÇÃO DO PRODUTO / SERVIÇO", 55.0f, false),
        ("NCM/SH", 12.0f, false),
        ("CST", 7.0f, false),
        ("CFOP", 8.0f, false),
        ("UNID", 8.0f, false),
        ("QUANT", 14.0f, true),
        ("VALOR UNIT", 16.0f, true),
        ("VALOR TOTAL", 16.0f, true),
        ("BC ICMS", 14.0f, true),
        ("VLR ICMS", 13.0f, true),
        ("VLR IPI", 11.0f, true),
        ("ALIQ ICMS", 8.0f, true),
        ("ALIQ IPI", 7.7f, true),
    ];

    /// <summary>
    /// Soma das larguras das colunas. Conferida por teste contra
    /// <see cref="Largura"/> - se alguem mexer numa coluna sem acertar outra,
    /// o teste quebra antes de o desenho sair torto.
    /// </summary>
    public static float SomaColunasProduto
    {
        get
        {
            float total = 0f;
            foreach ((_, float largura, _) in ColunasProduto)
            {
                total += largura;
            }

            return total;
        }
    }
}
