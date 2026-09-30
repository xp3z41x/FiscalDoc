using FiscalDoc.App;
using FiscalDoc.Core.Model.Cte;
using FiscalDoc.Core.Model.Evento;
using FiscalDoc.Core.Model.Mdfe;
using FiscalDoc.Core.Model.Nfe;
using FiscalDoc.Core.Parsing;
using FiscalDoc.Core.Values;
using FiscalDoc.Layout.Barcode;
using FiscalDoc.Layout.Dacte;
using FiscalDoc.Layout.Damdfe;
using FiscalDoc.Layout.Danfe;
using FiscalDoc.Layout.DisplayList;
using FiscalDoc.Layout.Evento;
using FiscalDoc.Layout.Nfce;
using FiscalDoc.Render.Wpf;

namespace FiscalDoc.Tests;

/// <summary>
/// CNPJ alfanumerico (IN RFB 2.229/2024), emitido desde julho de 2026, e o seu
/// reflexo na chave de acesso de NF-e, NFC-e, CT-e e MDF-e.
///
/// <para>Fontes: NT Conjunta 2025.001 v1.00 (ENCAT/RFB) - composicao da chave,
/// DV por ASCII - 48, codigo de barras hibrido 128C/128A; NT NF-e 2026.004
/// v1.01, NT CT-e 2025.001 v1.14b (item 17) e NT MDF-e 2025.001 v1.03 (item
/// 4) - as mesmas expressoes no schema de cada projeto; RFB, Perguntas e
/// Respostas do CNPJ alfanumerico - o exemplo 12.ABC.345/01DE-35 (pergunta
/// 14), a mesma mascara (21) e todas as 26 letras (45).</para>
///
/// <para>Ate esta mudanca, a chave com letra era recusada na leitura - so os
/// digitos eram contados -, e o documento saia sem chave e sem codigo de
/// barras; e, se a leitura passasse, o Code 128C lancaria excecao na primeira
/// letra, e a janela mostraria a mensagem de falha de layout no lugar do
/// documento.</para>
/// </summary>
public sealed class CnpjAlfanumericoTests
{
    /// <summary>
    /// cUF 35, AAMM 2607, o CNPJ do exemplo da Receita (12.ABC.345/01DE-35),
    /// modelo 55, serie 1, numero 214, tpEmis 1, cNF 63000075 - e o DV 6,
    /// calculado por uma transcricao independente do algoritmo do Anexo II da
    /// NT Conjunta 2025.001 (VB.NET).
    /// </summary>
    private const string Chave = "35260712ABC34501DE35550010000002141630000756";

    private static readonly MedidorTextoWpf Medidor = new();

    // ================================================================ chave

    [Fact]
    public void Chave_com_letras_no_cnpj_e_reconhecida()
    {
        ChaveAcesso? c = ChaveAcesso.DeAtributoId("NFe" + Chave);

        Assert.NotNull(c);
        Assert.Equal(Chave, c!.Caracteres);
        Assert.Equal("35", c.CodigoUf);
        Assert.Equal("2607", c.AnoMes);
        Assert.Equal("12ABC34501DE35", c.CnpjEmitente);
        Assert.Equal("55", c.Modelo);
        Assert.Equal("001", c.Serie);
        Assert.Equal("000000214", c.Numero);
        Assert.Equal("1", c.TipoEmissao);
        Assert.Equal("63000075", c.CodigoNumerico);
        Assert.Equal("6", c.DigitoVerificador);
    }

    [Theory]
    [InlineData("NFe")]
    [InlineData("CTe")]
    [InlineData("MDFe")]
    [InlineData("")]
    public void Prefixo_de_qualquer_modelo_sai_e_as_letras_do_cnpj_ficam(string prefixo)
    {
        Assert.Equal(Chave, ChaveAcesso.DeAtributoId(prefixo + Chave)?.Caracteres);
    }

    [Fact]
    public void Chave_formatada_le_de_volta()
    {
        // O DACTE e o DAMDFE passam chave de lista por DeAtributoId e imprimem
        // a Formatada; separador e o que o prefixo toleram.
        ChaveAcesso c = ChaveAcesso.DeAtributoId(Chave)!;

        Assert.Equal("3526 0712 ABC3 4501 DE35 5500 1000 0002 1416 3000 0756", c.Formatada);
        Assert.Equal(c, ChaveAcesso.DeAtributoId(c.Formatada));
    }

    /// <summary>
    /// A expressao do schema e <c>[0-9]{6}[A-Z0-9]{12}[0-9]{26}</c>: letra so
    /// nas doze posicoes da raiz e da ordem do CNPJ, e so maiuscula. O DV do
    /// CNPJ, dentro da chave, continua numerico.
    /// </summary>
    [Theory]
    [InlineData(5)]   // AAMM
    [InlineData(18)]  // DV do CNPJ
    [InlineData(20)]  // modelo
    [InlineData(43)]  // DV da chave
    public void Letra_fora_das_posicoes_do_cnpj_nao_e_chave(int posicao)
    {
        string errada = Chave[..posicao] + "A" + Chave[(posicao + 1)..];

        Assert.Null(ChaveAcesso.DeAtributoId("NFe" + errada));
    }

    [Fact]
    public void Letra_minuscula_nao_e_chave()
    {
        Assert.Null(ChaveAcesso.DeAtributoId("NFe" + Chave.ToLowerInvariant()));
    }

    [Fact]
    public void Dv_da_chave_usa_o_ascii_menos_48_de_cada_caractere()
    {
        // As letras valem 17 ("A") a 42 ("Z"). Com int.Parse, ou pulando as
        // letras, o DV sairia outro.
        Assert.Equal(6, ChaveAcesso.CalcularDv(Chave[..43]));
        Assert.True(ChaveAcesso.DeAtributoId(Chave)!.DigitoVerificadorConfere);

        // Uma letra trocada por outra muda o DV - a letra entra na conta.
        string outra = Chave[..8] + "B" + Chave[9..43];
        Assert.NotEqual(6, ChaveAcesso.CalcularDv(outra));
    }

    [Fact]
    public void Emitente_da_chave_alfanumerica_e_o_cnpj_inteiro()
    {
        // Com letra nao ha CPF possivel: o desempate por DV nem se aplica.
        Assert.Equal("12ABC34501DE35", ChaveAcesso.DeAtributoId(Chave)!.DocumentoEmitente);

        // Nem quando o campo comeca por 000, que e onde o CPF se esconde.
        string corpo = "352607" + "000ABC3450HY" + "88" + "55001000000214163000075";
        ChaveAcesso zeros = ChaveAcesso.DeAtributoId(corpo + ChaveAcesso.CalcularDv(corpo))!;

        Assert.Equal("000ABC3450HY88", zeros.DocumentoEmitente);
    }

    // ============================================================= formatos

    /// <summary>
    /// A mascara e a de sempre (RFB, pergunta 21). Os valores sao os exemplos
    /// da propria Receita cujo DV confere: 12.ABC.345/01DE-35 (pergunta 14) e
    /// AA.345.678/000A-29 e 12.345.678/000A-08 (pergunta 23). O primeiro
    /// exemplo da pergunta 23, AA.345.678/0003-29, fica de fora: o DV dele e
    /// 86, e nao 29.
    /// </summary>
    [Theory]
    [InlineData("12ABC34501DE35", "12.ABC.345/01DE-35")]
    [InlineData("AA345678000A29", "AA.345.678/000A-29")]
    [InlineData("12345678000A08", "12.345.678/000A-08")]
    [InlineData("12.ABC.345/01DE-35", "12.ABC.345/01DE-35")]
    [InlineData("11222333000181", "11.222.333/0001-81")]
    public void Cnpj_alfanumerico_sai_com_a_mascara_de_sempre(string doXml, string impresso)
    {
        Assert.Equal(impresso, Formatos.Cnpj(doXml));
        Assert.Equal(impresso, Formatos.CnpjOuCpf(doXml));
    }

    /// <summary>
    /// Tres letras deixam onze digitos. Contar so os digitos imprimia
    /// 12ABC345000188 como o CPF 123.450.001-88, que nao esta no arquivo -
    /// o que o MOC 3.1 proibe.
    /// </summary>
    [Fact]
    public void Cnpj_alfanumerico_com_onze_digitos_nao_vira_cpf()
    {
        Assert.Equal("12.ABC.345/0001-88", Formatos.CnpjOuCpf("12ABC345000188"));
    }

    /// <summary>
    /// O que nao tem a forma do schema sai como veio: nem mascara, nem
    /// maiuscula inventada, nem letra jogada fora.
    /// </summary>
    [Theory]
    [InlineData("12abc34501de35")]   // minuscula
    [InlineData("12ABC34501DEAB")]   // DV com letra
    [InlineData("1234567890A")]      // CPF com letra
    public void Fora_da_forma_do_schema_sai_como_veio(string doXml)
    {
        Assert.Equal(doXml, Formatos.CnpjOuCpf(doXml));
        Assert.Equal(doXml, Formatos.Cnpj(doXml));
        Assert.Equal(doXml, Formatos.Cpf(doXml));
    }

    [Fact]
    public void Cpf_continua_sendo_cpf()
    {
        Assert.Equal("123.456.789-09", Formatos.CnpjOuCpf("12345678909"));
        Assert.Equal("123.456.789-09", Formatos.Cpf("123.456.789-09"));
    }

    // ========================================================== documentos

    [Fact]
    public void Danfe_retrato_traz_chave_cnpjs_e_codigo_que_le_a_chave()
    {
        NfeDocumento nfe = Ler<NfeDocumento>("cnpj-alfanumerico.xml");
        Assert.False(nfe.Paisagem);

        Pagina p = DanfeRetrato.Construir(nfe, Medidor).Paginas[0];
        List<string> textos = Textos(p);

        Assert.Contains(nfe.Chave!.Formatada, textos);
        Assert.Contains("12.ABC.345/01DE-35", textos);   // emitente
        Assert.Contains("13.ABC.345/01DE-06", textos);   // destinatario
        Assert.Equal(nfe.Chave.Caracteres, LerCodigo(p));
    }

    [Fact]
    public void Danfe_paisagem_traz_chave_e_codigo_que_le_a_chave()
    {
        NfeDocumento nfe = Ler<NfeDocumento>(
            "cnpj-alfanumerico.xml", x => x.Replace("<tpImp>1</tpImp>", "<tpImp>2</tpImp>", StringComparison.Ordinal));
        Assert.True(nfe.Paisagem);

        Pagina p = DanfePaisagem.Construir(nfe, Medidor).Paginas[0];

        Assert.Contains(nfe.Chave!.Formatada, Textos(p));
        Assert.Equal(nfe.Chave.Caracteres, LerCodigo(p));
    }

    [Fact]
    public void Nfce_traz_a_chave_com_letras_e_o_qr_code_do_arquivo()
    {
        NfeDocumento nfce = Ler<NfeDocumento>("nfce-cnpj-alfanumerico.xml");
        List<string> textos = DanfeNfce.Construir(nfce, Medidor).Paginas.SelectMany(Textos).ToList();

        Assert.Contains(nfce.Chave!.Formatada, textos);
        Assert.Contains(textos, t => t.Contains("12.ABC.345/01DE-35", StringComparison.Ordinal));

        // O QR Code e o do arquivo, e a chave dentro dele tambem tem letras.
        Assert.Contains(nfce.Chave.Caracteres, nfce.Suplementares!.QrCode!, StringComparison.Ordinal);
    }

    [Fact]
    public void Dacte_traz_a_chave_e_os_documentos_com_emitente_alfanumerico()
    {
        CteDocumento cte = Ler<CteDocumento>("cte-400-cnpj-alfanumerico.xml");
        Pagina p = DacteLayout.Construir(cte, Medidor).Paginas[0];
        List<string> textos = Textos(p);

        Assert.Contains(cte.Chave!.Formatada, textos);
        Assert.Equal(cte.Chave.Caracteres, LerCodigo(p));

        // CNPJ/CPF EMITENTE e SERIE/No de cada NF-e saem da chave dela, que
        // tambem tem letras (MOC CT-e 4.00 Anexo II, 3).
        DocumentoReferenciado nota = cte.DocumentosOriginarios[0];
        Assert.Equal("13ABC34501DE06", nota.DocumentoEmitente);
        Assert.Contains("13.ABC.345/01DE-06", textos);
        Assert.Contains("1 / 000.294.050", textos);
    }

    [Fact]
    public void Damdfe_traz_a_chave_e_a_lista_de_documentos_com_letras()
    {
        MdfeDocumento mdfe = Ler<MdfeDocumento>("mdfe-300-cnpj-alfanumerico.xml");
        Pagina p = DamdfeLayout.Construir(mdfe, Medidor).Paginas[0];
        List<string> textos = Textos(p);

        Assert.Contains(mdfe.Chave!.Formatada, textos);
        Assert.Equal(mdfe.Chave.Caracteres, LerCodigo(p));
        Assert.Contains("12.ABC.345/01DE-35", textos);

        foreach (DocumentoDescarga d in mdfe.Documentos)
        {
            Assert.Contains(ChaveAcesso.DeAtributoId(d.Chave)!.Formatada, textos);
        }
    }

    [Fact]
    public void Evento_traz_a_chave_referenciada_e_o_autor_alfanumerico()
    {
        EventoDocumento ev = Ler<EventoDocumento>("evento-nfe-cnpj-alfanumerico.xml");
        List<string> textos = EventoLayout.Construir(ev, Medidor).Paginas.SelectMany(Textos).ToList();

        Assert.NotNull(ev.ChaveReferenciada);
        Assert.Contains(ev.ChaveReferenciada!.Formatada, textos);
        Assert.Contains("12.ABC.345/01DE-35", textos);
    }

    /// <summary>
    /// Em todo documento auxiliar, de toda amostra com CNPJ alfanumerico - e
    /// nas variantes que mudam a geometria do cabecalho -, o modulo do codigo
    /// fica no minimo normativo de 0,02 cm ou acima (MOC 7.0 Anexo II, 2; NT
    /// Conjunta 2025.001, item 6).
    ///
    /// <para>No DACTE e no DAMDFE o codigo com letra e mais comprido e a
    /// coluna da chave e que cresce: antes ela era dimensionada para os 297
    /// modulos da chave numerica, e no retrato sem QR Code o modulo caia a
    /// 0,19 mm; com QR Code, a 0,15.</para>
    /// </summary>
    [Fact]
    public void Codigo_com_letras_nunca_fica_abaixo_do_modulo_minimo()
    {
        static string SemQr(string x) =>
            System.Text.RegularExpressions.Regex.Replace(x, "<infCTeSupl>.*?</infCTeSupl>", string.Empty);

        static string Paisagem(string x) =>
            x.Replace("<tpImp>1</tpImp>", "<tpImp>2</tpImp>", StringComparison.Ordinal);

        var paginas = new List<(string Nome, Pagina Pagina)>
        {
            ("DANFE retrato", DanfeRetrato.Construir(Ler<NfeDocumento>("cnpj-alfanumerico.xml"), Medidor).Paginas[0]),
            ("DANFE paisagem", DanfePaisagem.Construir(Ler<NfeDocumento>("cnpj-alfanumerico.xml", Paisagem), Medidor).Paginas[0]),
            ("DACTE retrato com QR", DacteLayout.Construir(Ler<CteDocumento>("cte-400-cnpj-alfanumerico.xml"), Medidor).Paginas[0]),
            ("DACTE retrato sem QR", DacteLayout.Construir(Ler<CteDocumento>("cte-400-cnpj-alfanumerico.xml", SemQr), Medidor).Paginas[0]),
            ("DACTE paisagem", DacteLayout.Construir(Ler<CteDocumento>("cte-400-cnpj-alfanumerico.xml", Paisagem), Medidor).Paginas[0]),
            ("DAMDFE", DamdfeLayout.Construir(Ler<MdfeDocumento>("mdfe-300-cnpj-alfanumerico.xml"), Medidor).Paginas[0]),
        };

        foreach ((string nome, Pagina pagina) in paginas)
        {
            Primitiva.CodigoBarras b = Assert.Single(pagina.Primitivas.OfType<Primitiva.CodigoBarras>());
            float modulo = b.Caixa.Largura / b.Modulos.Sum();

            Assert.True(modulo >= b.LarguraModuloMmMinima, $"{nome}: modulo de {modulo:0.000} mm");
            Assert.True(b.Modulos.Sum() > Code128.TotalModulos(44), $"{nome}: a chave nao tem letra");
        }
    }

    /// <summary>
    /// Com a chave so de digitos, o cabecalho do DACTE e do DAMDFE nao se
    /// move: a coluna da chave so cresce quando o codigo pede. Os numeros sao
    /// os que a versao anterior a esta mudanca desenhava para as mesmas
    /// amostras - e as PNGs de conferencia de toda amostra numerica sairam
    /// identicas, byte a byte.
    ///
    /// <para>O DAMDFE entra com e sem QR Code: sao as duas divisoes de coluna
    /// do cabecalho, e um MDF-e anterior ao infMDFeSupl chega sem ele.</para>
    /// </summary>
    [Theory]
    [InlineData("cte-400-subcontratacao.xml", false, 104f, 62f)]      // retrato, com QR Code
    [InlineData("cte-400-rodoviario.xml", false, 163.9f, 89.1f)]      // paisagem, com QR Code
    [InlineData("mdfe-300-rodoviario.xml", false, 103.8f, 61.2f)]     // DAMDFE, com QR Code
    [InlineData("mdfe-300-rodoviario.xml", true, 130.26f, 72.24f)]    // DAMDFE, sem QR Code
    public void Chave_numerica_nao_muda_a_geometria_do_cabecalho(string arquivo, bool semQr, float x, float largura)
    {
        var ok = Assert.IsType<ResultadoLeitura.Ok>(LeitorDocumento.Ler(Amostras.Sintetica(arquivo)));

        ConjuntoPaginas c = ok.Documento switch
        {
            CteDocumento cte => DacteLayout.Construir(cte, Medidor),
            MdfeDocumento mdfe => DamdfeLayout.Construir(semQr ? mdfe with { QrCode = null } : mdfe, Medidor),
            _ => throw new InvalidOperationException(arquivo),
        };

        Primitiva.CodigoBarras b = Assert.Single(c.Paginas[0].Primitivas.OfType<Primitiva.CodigoBarras>());

        Assert.Equal(Code128.TotalModulos(44), b.Modulos.Sum());
        Assert.InRange(b.Caixa.X, x - 0.001f, x + 0.001f);
        Assert.InRange(b.Caixa.Largura, largura - 0.001f, largura + 0.001f);
    }

    [Fact]
    public void O_codigo_desenhado_le_de_volta_a_chave_a_600_dpi()
    {
        // O teste que importa para quem vai ler o papel: o DANFE retrato, onde
        // a folga e menor, rasterizado como a impressora faria, e o codigo lido
        // da imagem pixel a pixel.
        NfeDocumento nfe = Ler<NfeDocumento>("cnpj-alfanumerico.xml");
        Primitiva.CodigoBarras b = DanfeRetrato.Construir(nfe, Medidor).Paginas[0]
            .Primitivas.OfType<Primitiva.CodigoBarras>().Single();

        const float dpi = 600f;
        var caixa = new RetanguloMm(5f, 5f, b.Caixa.Largura, b.Caixa.Altura);

        using System.Drawing.Bitmap bmp = RenderTeste.Papel(
            new Pagina(1, [b with { Caixa = caixa }]),
            new TamanhoPapel(caixa.Largura + 10f, caixa.Altura + 10f),
            dpi);

        int y = (int)((caixa.Y + (caixa.Altura / 2f)) * dpi / 25.4f);

        Assert.Equal(nfe.Chave!.Caracteres, LeitorCode128.Dados(LeitorCode128.SimbolosDaImagem(bmp, y)));
    }

    /// <summary>
    /// Pelo caminho do proprio aplicativo: abrir o arquivo da o documento, e
    /// nao mensagem de falha.
    /// </summary>
    [Theory]
    [InlineData("cnpj-alfanumerico.xml")]
    [InlineData("nfce-cnpj-alfanumerico.xml")]
    [InlineData("cte-400-cnpj-alfanumerico.xml")]
    [InlineData("mdfe-300-cnpj-alfanumerico.xml")]
    [InlineData("evento-nfe-cnpj-alfanumerico.xml")]
    public void O_aplicativo_abre_todo_documento_com_cnpj_alfanumerico(string arquivo)
    {
        AberturaDocumento.Resultado r = new AberturaDocumento().Abrir(Amostras.Sintetica(arquivo));

        Assert.Null(r.Mensagem);
        Assert.NotNull(r.Paginas);
        Assert.NotEmpty(r.Paginas!.Paginas);
    }

    // ============================================================= auxiliares

    private static T Ler<T>(string arquivo, Func<string, string>? alterar = null)
    {
        string caminho = Amostras.Sintetica(arquivo);

        if (alterar is null)
        {
            var ok = Assert.IsType<ResultadoLeitura.Ok>(LeitorDocumento.Ler(caminho));
            return Assert.IsType<T>(ok.Documento);
        }

        string temporario = Path.Combine(Path.GetTempPath(), $"alfa-{Guid.NewGuid()}.xml");
        File.WriteAllText(temporario, alterar(File.ReadAllText(caminho)));

        try
        {
            var ok = Assert.IsType<ResultadoLeitura.Ok>(LeitorDocumento.Ler(temporario));
            return Assert.IsType<T>(ok.Documento);
        }
        finally
        {
            File.Delete(temporario);
        }
    }

    private static List<string> Textos(Pagina p) =>
        p.Primitivas.OfType<Primitiva.Texto>().Select(t => t.Conteudo).ToList();

    /// <summary>O codigo de barras da pagina, lido de volta das larguras.</summary>
    private static string LerCodigo(Pagina p)
    {
        Primitiva.CodigoBarras b = Assert.Single(p.Primitivas.OfType<Primitiva.CodigoBarras>());
        return LeitorCode128.Dados(LeitorCode128.Simbolos(b.Modulos));
    }
}
