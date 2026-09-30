using FiscalDoc.Core.Model.Cte;
using FiscalDoc.Core.Parsing;
using FiscalDoc.Core.Values;
using FiscalDoc.Layout.Composition;
using FiscalDoc.Layout.Dacte;
using FiscalDoc.Layout.DisplayList;
using FiscalDoc.Render.Wpf;

namespace FiscalDoc.Tests;

/// <summary>
/// O que o DACTE imprime, campo a campo, contra o manual (MOC CT-e 4.00,
/// Anexo II). Nasceu de uma reclamacao concreta: o financeiro de um usuario
/// nao achava no DACTE a nota fiscal do frete - o quadro "Documentos
/// Originarios" trazia so a chave de 44 digitos. A auditoria que se seguiu
/// achou o resto: QR Code, tomador, ICMS por substituicao, documentos
/// anteriores, CT-e complementado e substituido, os quadros de cada modal.
///
/// <para>As amostras sao sinteticas (tools/gerar-amostras-transporte.py),
/// uma por assunto.</para>
/// </summary>
public sealed class DacteTests
{
    private static CteDocumento Ler(string arquivo)
    {
        var ok = Assert.IsType<ResultadoLeitura.Ok>(LeitorDocumento.Ler(Amostras.Sintetica(arquivo)));
        return Assert.IsType<CteDocumento>(ok.Documento);
    }

    /// <summary>Le uma amostra modificada, gravada num arquivo temporario.</summary>
    private static CteDocumento LerVariacao(string arquivo, Func<string, string> alterar)
    {
        string xml = alterar(File.ReadAllText(Amostras.Sintetica(arquivo)));
        string caminho = Path.Combine(Path.GetTempPath(), $"dacte-{Guid.NewGuid()}.xml");
        File.WriteAllText(caminho, xml);

        try
        {
            var ok = Assert.IsType<ResultadoLeitura.Ok>(LeitorDocumento.Ler(caminho));
            return Assert.IsType<CteDocumento>(ok.Documento);
        }
        finally
        {
            File.Delete(caminho);
        }
    }

    private static IEnumerable<string> Textos(Pagina p) =>
        p.Primitivas.OfType<Primitiva.Texto>().Select(t => t.Conteudo);

    // ================================================= documentos originarios

    [Fact]
    public void Nfe_originaria_traz_emitente_serie_e_numero_tirados_da_chave()
    {
        // A reclamacao: o numero da nota nao aparecia. Ele esta na chave -
        // e e de la que as colunas do manual o tiram.
        CteDocumento cte = Ler("cte-400-rodoviario.xml");
        DocumentoReferenciado d = cte.DocumentosOriginarios[0];

        Assert.Equal("NF-e", d.Tipo);
        Assert.NotNull(d.Chave);
        Assert.Equal(d.Chave!.CnpjEmitente, d.DocumentoEmitente);
        Assert.Equal("001", d.Serie);
        Assert.Equal("000294000", d.Numero);
    }

    [Fact]
    public void Dacte_imprime_serie_e_numero_da_nota_ao_lado_da_chave()
    {
        CteDocumento cte = Ler("cte-400-rodoviario.xml");
        ConjuntoPaginas c = DacteLayout.Construir(cte, new MedidorTextoWpf());

        var textos = c.Paginas.SelectMany(Textos).ToList();
        DocumentoReferenciado d = cte.DocumentosOriginarios[0];

        Assert.Contains("SÉRIE/Nº DOCUMENTO", textos);
        Assert.Contains("CNPJ/CPF EMITENTE", textos);
        Assert.Contains("1 / 000.294.000", textos);
        Assert.Contains(Formatos.Cnpj(d.DocumentoEmitente), textos);
        Assert.Contains(d.Chave!.Formatada, textos);
    }

    [Fact]
    public void Nota_em_papel_leva_o_cnpj_do_remetente()
    {
        // Manual, secao 3: "CNPJ/CPF Emitente: CNPJ/CPF, em rem".
        CteDocumento cte = Ler("cte-400-subcontratacao.xml");

        Assert.Collection(
            cte.DocumentosOriginarios,
            d =>
            {
                Assert.Equal("NF produtor", d.Tipo);
                Assert.Equal(cte.Remetente!.Documento, d.DocumentoEmitente);
                Assert.Equal("812", d.Numero);
                Assert.Null(d.Chave);
            },
            d =>
            {
                Assert.Equal("NF", d.Tipo);
                Assert.Equal("2", d.Serie);
                Assert.Equal("4471", d.Numero);
            });
    }

    [Fact]
    public void Outros_documentos_usam_a_descricao_do_arquivo()
    {
        CteDocumento cte = Ler("cte-400-aereo.xml");

        Assert.Equal(new[] { "ROMANEIO DE CARGA", "Declaração" }, cte.DocumentosOriginarios.Select(d => d.Tipo));
        Assert.Equal("R-2201", cte.DocumentosOriginarios[0].Numero);
    }

    [Fact]
    public void Dc_e_e_decomposta_pela_chave_como_a_nfe()
    {
        const string chave = "41260912222333000144990010002940001100000009";

        CteDocumento cte = LerVariacao("cte-400-homologacao.xml", xml =>
        {
            int ini = xml.IndexOf("<infDoc>", StringComparison.Ordinal);
            int fim = xml.IndexOf("</infDoc>", StringComparison.Ordinal) + "</infDoc>".Length;
            return xml[..ini] + $"<infDoc><infDCe><chave>{chave}</chave></infDCe></infDoc>" + xml[fim..];
        });

        DocumentoReferenciado d = Assert.Single(cte.DocumentosOriginarios);
        Assert.Equal("DC-e", d.Tipo);
        Assert.Equal("12222333000144", d.DocumentoEmitente);
        Assert.Equal("000294000", d.Numero);
    }

    [Fact]
    public void Chave_emitida_por_cpf_devolve_o_cpf()
    {
        // Pessoa fisica: o campo de 14 posicoes traz o CPF com zeros a esquerda.
        string corpo = "412609" + "000" + "11144477735" + "55" + "001" + "000000123" + "1" + "12345678";
        ChaveAcesso chave = ChaveAcesso.DeAtributoId(corpo + ChaveAcesso.CalcularDv(corpo))!;

        Assert.Equal("11144477735", chave.DocumentoEmitente);
    }

    [Fact]
    public void Chave_ambigua_fica_com_o_cnpj()
    {
        // 00.000.000/0001-91 e CNPJ valido e seus 11 ultimos digitos tambem
        // formam um CPF valido. Pessoa juridica e o emitente comum: vale CNPJ.
        string corpo = "412609" + "00000000000191" + "55" + "001" + "000000123" + "1" + "12345678";
        ChaveAcesso chave = ChaveAcesso.DeAtributoId(corpo + ChaveAcesso.CalcularDv(corpo))!;

        Assert.Equal("00000000000191", chave.DocumentoEmitente);
    }

    // ============================================================== imposto

    [Fact]
    public void Icms60_preenche_base_aliquota_e_valor_retidos()
    {
        // O ICMS60 da nome proprio a cada campo (vBCSTRet, pICMSSTRet,
        // vICMSSTRet). Antes, as tres caixas saiam em branco.
        CteDocumento cte = Ler("cte-400-subcontratacao.xml");

        Assert.Equal("60", cte.Icms.Cst);
        Assert.Equal(2202.10m, cte.Icms.BaseCalculo);
        Assert.Equal(12.00m, cte.Icms.Aliquota);
        Assert.Equal(264.25m, cte.Icms.Valor);
        Assert.Equal("60 - ICMS cobrado por substituição tributária", cte.Icms.Descricao);
    }

    [Fact]
    public void Ibs_e_cbs_sao_lidos_como_os_totais_da_nfe()
    {
        CteDocumento cte = Ler("cte-400-subcontratacao.xml");

        Assert.NotNull(cte.IbsCbs);
        Assert.Equal(2.20m, cte.IbsCbs!.ValorIbs);
        Assert.Equal(19.82m, cte.IbsCbs.ValorCbs);
        Assert.Equal(2224.12m, cte.IbsCbs.ValorTotalNotaComTributos);
        Assert.True(cte.IbsCbs.TotalDivergeDoValorDaNota(cte.ValorTotalPrestacao));

        // Documento sem o grupo nao ganha quadro vazio.
        Assert.Null(Ler("cte-400-rodoviario.xml").IbsCbs);
    }

    // ======================================================= participantes

    [Fact]
    public void Tomador_do_toma3_e_o_participante_referenciado()
    {
        CteDocumento cte = Ler("cte-400-rodoviario.xml");

        Assert.Equal(3, cte.CodigoTomador);
        Assert.Same(cte.Destinatario, cte.TomadorEfetivo);
    }

    [Fact]
    public void Tomador_do_toma4_traz_os_proprios_dados()
    {
        CteDocumento cte = Ler("cte-400-subcontratacao.xml");

        Assert.Equal(4, cte.CodigoTomador);
        Assert.Same(cte.Tomador, cte.TomadorEfetivo);
        Assert.NotEqual(cte.Emitente.Documento, cte.TomadorEfetivo!.Documento);
    }

    [Fact]
    public void Quadro_do_tomador_sai_mesmo_quando_ele_e_um_participante()
    {
        ConjuntoPaginas c = DacteLayout.Construir(Ler("cte-400-rodoviario.xml"), new MedidorTextoWpf());

        Assert.Contains("TOMADOR DO SERVIÇO (DESTINATÁRIO)", c.Paginas.SelectMany(Textos));
    }

    [Fact]
    public void Suframa_e_pais_dos_participantes_sao_lidos()
    {
        CteDocumento cte = Ler("cte-400-subcontratacao.xml");

        Assert.Equal("210987654", cte.Destinatario!.InscricaoSuframa);
        Assert.Null(cte.Remetente!.InscricaoSuframa);
        Assert.Equal("BRASIL", cte.Remetente.Endereco.Pais);
    }

    // ======================================= documentos e CT-e referenciados

    [Fact]
    public void Documentos_anteriores_vem_agrupados_por_emissor()
    {
        CteDocumento cte = Ler("cte-400-subcontratacao.xml");

        EmissorDocumentoAnterior emissor = Assert.Single(cte.DocumentosAnteriores);
        Assert.Equal("SC", emissor.Uf);

        Assert.Collection(
            emissor.Documentos,
            d =>
            {
                Assert.Equal("Conhecimento avulso", d.Tipo);
                Assert.Equal("15520", d.Numero);
                Assert.Equal(new DateOnly(2026, 9, 12), d.DataEmissao);
            },
            d =>
            {
                Assert.Equal("CT-e", d.Tipo);
                Assert.Equal("000040221", d.Numero);
                Assert.Equal(emissor.Documento, d.DocumentoEmitente);
            });
    }

    [Fact]
    public void Complemento_cita_os_ctes_complementados_e_nao_tem_carga()
    {
        CteDocumento cte = Ler("cte-400-complemento.xml");

        Assert.Equal(TipoCte.Complemento, cte.Tipo);
        Assert.Equal(new[] { "000091723", "000091724" }, cte.Referencias.Complementados.Select(d => d.Numero));
        Assert.Null(cte.ProdutoPredominante);
        Assert.Empty(cte.DocumentosOriginarios);

        var textos = DacteLayout.Construir(cte, new MedidorTextoWpf()).Paginas.SelectMany(Textos).ToList();
        Assert.Contains("CT-E COMPLEMENTADO", textos);
        Assert.DoesNotContain("INFORMAÇÕES DA CARGA", textos);
    }

    [Fact]
    public void Substituto_cita_o_cte_substituido_e_a_alteracao_de_tomador()
    {
        CteDocumento cte = Ler("cte-400-substituto.xml");

        Assert.NotNull(cte.Referencias.Substituido);
        Assert.Equal("000091723", cte.Referencias.Substituido!.Numero);
        Assert.True(cte.Referencias.AlteraTomador);
    }

    [Fact]
    public void Servico_vinculado_cita_o_cte_multimodal()
    {
        CteDocumento cte = Ler("cte-400-aquaviario.xml");

        Assert.Equal(TipoServicoCte.ServicoVinculadoMultimodal, cte.Servico);
        Assert.Equal("000070001", Assert.Single(cte.Referencias.Multimodais).Numero);
    }

    // =============================================== complemento e cobranca

    [Fact]
    public void Grupo_compl_traz_caracteristicas_fluxo_entrega_e_campos_livres()
    {
        ComplementoCte c = Ler("cte-400-subcontratacao.xml").Complemento;

        Assert.Equal("PALETIZADA", c.CaracteristicaTransporte);
        Assert.Equal("ENTREGA AGENDADA", c.CaracteristicaServico);

        Assert.Equal(new[] { "JVE", "REG" }, c.Fluxo!.Passagens);
        Assert.Equal("R12", c.Fluxo.Rota);

        Assert.Equal("Até 19/09/2026", c.Entrega!.DescricaoData);
        Assert.Equal("Das 08:00 às 12:00", c.Entrega.DescricaoHora);

        Assert.Equal(new[] { "PEDIDO: PC-4471", "COLETA: JANELA 07H-09H" }, c.CamposContribuinte.Select(x => x.Linha));
        Assert.Equal("ICMS: ICMS RETIDO POR SUBSTITUICAO TRIBUTARIA", Assert.Single(c.CamposFisco).Linha);
    }

    [Fact]
    public void Cobranca_traz_fatura_e_duplicatas()
    {
        CobrancaCte cobranca = Ler("cte-400-subcontratacao.xml").Cobranca!;

        Assert.Equal("91724", cobranca.Fatura!.Numero);
        Assert.Equal(2202.10m, cobranca.Fatura.ValorLiquido);
        Assert.Equal(2, cobranca.Duplicatas.Count);
        Assert.Equal(new DateOnly(2026, 10, 14), cobranca.Duplicatas[0].Vencimento);
    }

    // ================================================================ modais

    [Theory]
    [InlineData("cte-400-subcontratacao.xml", typeof(DetalheRodoviario))]
    [InlineData("cte-400-aereo.xml", typeof(DetalheAereo))]
    [InlineData("cte-400-aquaviario.xml", typeof(DetalheAquaviario))]
    [InlineData("cte-400-ferroviario.xml", typeof(DetalheFerroviario))]
    [InlineData("cte-400-dutoviario.xml", typeof(DetalheDutoviario))]
    [InlineData("cte-400-multimodal.xml", typeof(DetalheMultimodal))]
    public void Cada_modal_ganha_o_seu_quadro(string arquivo, Type esperado)
    {
        CteDocumento cte = Ler(arquivo);

        Assert.IsType(esperado, cte.DetalheModal);

        string titulo = esperado == typeof(DetalheMultimodal)
            ? "INFORMAÇÕES ESPECÍFICAS DO TRANSPORTE MULTIMODAL DE CARGAS"
            : $"INFORMAÇÕES ESPECÍFICAS DO MODAL {RotulosCte.Modal(cte.Modal).ToUpperInvariant()}";

        ConjuntoPaginas c = DacteLayout.Construir(cte, new MedidorTextoWpf());
        Assert.Contains(titulo, c.Paginas.SelectMany(Textos));
    }

    [Fact]
    public void Rodoviario_traz_as_ordens_de_coleta()
    {
        var rodo = Assert.IsType<DetalheRodoviario>(Ler("cte-400-subcontratacao.xml").DetalheModal);

        OrdemColeta o = Assert.Single(rodo.OrdensColeta);
        Assert.Equal("55821", o.Numero);
        Assert.Equal(new DateOnly(2026, 9, 12), o.DataEmissao);
    }

    [Fact]
    public void Aereo_traz_tarifa_manuseio_perigosos_e_retirada()
    {
        CteDocumento cte = Ler("cte-400-aereo.xml");
        var aereo = Assert.IsType<DetalheAereo>(cte.DetalheModal);

        Assert.Equal("12345678901", aereo.NumeroOperacional);
        Assert.Equal("G - Tarifa geral", aereo.ClasseTarifa);
        Assert.Equal(2, aereo.InformacoesManuseio.Count);
        Assert.StartsWith("02 - ", aereo.InformacoesManuseio[0], StringComparison.Ordinal);

        ArtigoPerigoso p = Assert.Single(aereo.ArtigosPerigosos);
        Assert.Equal("3480", p.NumeroOnu);
        Assert.Equal("KG", p.Unidade);

        Assert.True(cte.RecebedorRetira);
        Assert.NotNull(cte.DetalhesRetirada);
    }

    [Fact]
    public void Aquaviario_traz_balsas_e_conteineres()
    {
        var aquav = Assert.IsType<DetalheAquaviario>(Ler("cte-400-aquaviario.xml").DetalheModal);

        Assert.Equal(new[] { "BALSA 07", "BALSA 11" }, aquav.Balsas);
        Assert.Equal("Norte", aquav.Direcao);
        Assert.Equal("1 - Cabotagem", aquav.TipoNavegacao);

        Conteiner c = Assert.Single(aquav.Conteineres);
        Assert.Equal(2, c.Lacres.Count);
        Assert.Equal("NF-e", Assert.Single(c.Documentos).Tipo);
    }

    [Fact]
    public void Ferroviario_traz_trafego_ferrovias_e_veiculos_novos()
    {
        CteDocumento cte = Ler("cte-400-ferroviario.xml");
        var ferrov = Assert.IsType<DetalheFerroviario>(cte.DetalheModal);

        Assert.Equal("1 - Mútuo", ferrov.TipoTrafego);
        Assert.Single(ferrov.Ferrovias);
        Assert.Equal(2, cte.VeiculosNovos.Count);
        Assert.Equal("BRANCO", cte.VeiculosNovos[0].Cor);
    }

    [Fact]
    public void Dutoviario_guarda_as_seis_casas_da_tarifa()
    {
        CteDocumento cte = Ler("cte-400-dutoviario.xml");
        var duto = Assert.IsType<DetalheDutoviario>(cte.DetalheModal);

        Assert.Equal(0.012345m, duto.ValorTarifa);
        Assert.Equal("1 - Gasoduto", duto.Classificacao);

        // Com duas casas a tarifa sairia "0,01" - um numero que nao esta no
        // arquivo.
        var textos = DacteLayout.Construir(cte, new MedidorTextoWpf()).Paginas.SelectMany(Textos);
        Assert.Contains("0,012345", textos);

        Assert.True(cte.Globalizado);
        Assert.NotNull(cte.InformacoesGlobalizado);
    }

    [Fact]
    public void Multimodal_traz_certificado_e_seguro()
    {
        var mm = Assert.IsType<DetalheMultimodal>(Ler("cte-400-multimodal.xml").DetalheModal);

        Assert.Equal("COTM000123", mm.Cotm);
        Assert.Equal("1 - Negociável", mm.Negociavel);
        Assert.Equal("APL55120", mm.Apolice);
    }

    // =============================================================== QR Code

    [Fact]
    public void Qr_code_aparece_em_toda_folha()
    {
        // Manual 2.19.1: "campo 3: informara o QR Code". O cabecalho se
        // repete, e o QR vai junto.
        ConjuntoPaginas c = DacteLayout.Construir(Ler("cte-400-muitos-documentos.xml"), new MedidorTextoWpf());

        Assert.True(c.Total > 1);
        Assert.All(c.Paginas, p => Assert.Single(p.Primitivas.OfType<Primitiva.CodigoQr>()));
    }

    [Fact]
    public void Qr_code_tem_ao_menos_os_25_mm_do_manual()
    {
        ConjuntoPaginas c = DacteLayout.Construir(Ler("cte-400-rodoviario.xml"), new MedidorTextoWpf());
        Primitiva.CodigoQr qr = c.Paginas[0].Primitivas.OfType<Primitiva.CodigoQr>().Single();

        Assert.True(qr.Caixa.Largura >= 25f, $"QR com {qr.Caixa.Largura} mm");
        Assert.Equal(qr.Caixa.Largura, qr.Caixa.Altura);
    }

    [Fact]
    public void Qr_code_e_achado_tambem_dentro_de_infCte()
    {
        // O schema o poe ao lado de infCte; um arquivo que o grave dentro
        // ainda traz o QR Code.
        CteDocumento cte = LerVariacao("cte-400-rodoviario.xml", xml =>
        {
            int ini = xml.IndexOf("<infCTeSupl>", StringComparison.Ordinal);
            int fim = xml.IndexOf("</infCTeSupl>", StringComparison.Ordinal) + "</infCTeSupl>".Length;
            string supl = xml[ini..fim];
            return xml.Remove(ini, fim - ini).Replace("</infCte>", supl + "</infCte>", StringComparison.Ordinal);
        });

        Assert.StartsWith("https://", cte.QrCode, StringComparison.Ordinal);
    }

    [Fact]
    public void Qr_code_maior_que_o_simbolo_perde_o_simbolo_e_nao_o_documento()
    {
        CteDocumento cte = LerVariacao("cte-400-rodoviario.xml", xml =>
        {
            int ini = xml.IndexOf("<qrCodCTe>", StringComparison.Ordinal) + "<qrCodCTe>".Length;
            int fim = xml.IndexOf("</qrCodCTe>", StringComparison.Ordinal);
            return xml[..ini] + new string('A', 3000) + xml[fim..];
        });

        ConjuntoPaginas c = DacteLayout.Construir(cte, new MedidorTextoWpf());

        Assert.True(c.Total >= 1);
        Assert.Empty(c.Paginas.SelectMany(p => p.Primitivas.OfType<Primitiva.CodigoQr>()));
    }

    // ============================================================ diagramacao

    [Theory]
    [InlineData("cte-400-rodoviario.xml")]
    [InlineData("cte-400-subcontratacao.xml")]
    public void Descricao_do_cabecalho_cabe_na_propria_caixa(string arquivo)
    {
        // No retrato com QR Code a coluna de identificacao estreita, e a
        // descricao pede tres linhas. Com a caixa fixa de 6 mm, a terceira
        // passava por baixo do numero do CT-e.
        var medidor = new MedidorTextoWpf();
        ConjuntoPaginas c = DacteLayout.Construir(Ler(arquivo), medidor);

        Primitiva.Texto descricao = c.Paginas[0].Primitivas.OfType<Primitiva.Texto>()
            .Single(t => t.Conteudo.StartsWith("Documento Auxiliar", StringComparison.Ordinal));
        Primitiva.Texto numero = c.Paginas[0].Primitivas.OfType<Primitiva.Texto>()
            .Single(t => t.Conteudo.StartsWith("N. ", StringComparison.Ordinal));

        int linhas = medidor.Quebrar(descricao.Conteudo, descricao.Estilo, descricao.Caixa.Largura).Count;
        float necessaria = linhas * medidor.AlturaLinhaMm(descricao.Estilo);

        Assert.True(necessaria <= descricao.Caixa.Altura + 0.01f,
            $"{linhas} linhas pedem {necessaria:0.00} mm numa caixa de {descricao.Caixa.Altura:0.00} mm");
        Assert.True(descricao.Caixa.Base <= numero.Caixa.Y + 0.01f);
    }

    [Theory]
    [InlineData("cte-400-subcontratacao.xml")]
    [InlineData("cte-400-complemento.xml")]
    [InlineData("cte-400-substituto.xml")]
    [InlineData("cte-400-aereo.xml")]
    [InlineData("cte-400-aquaviario.xml")]
    [InlineData("cte-400-ferroviario.xml")]
    [InlineData("cte-400-dutoviario.xml")]
    [InlineData("cte-400-multimodal.xml")]
    public void Toda_amostra_cabe_no_papel_e_numera_as_folhas(string arquivo)
    {
        ConjuntoPaginas c = DacteLayout.Construir(Ler(arquivo), new MedidorTextoWpf());

        Assert.True(c.ExtensaoUsada.Direita <= c.Papel.LarguraMm + 0.01f);
        Assert.True(c.ExtensaoUsada.Base <= c.Papel.AlturaMm + 0.01f);

        for (int i = 0; i < c.Total; i++)
        {
            Assert.Contains($"FOLHA {i + 1:00}/{c.Total:00}", Textos(c.Paginas[i]));
        }
    }

    // ========================================== paginacao do montador de blocos

    private static ConjuntoPaginas Montar(IReadOnlyList<BlocoDoc> blocos)
    {
        var m = new MontadorDocumento(TamanhoPapel.A4, 5f, new EstilosDanfe(), new MedidorTextoWpf());
        return m.Montar(blocos, (_, _, _) => m.Topo);
    }

    private static List<BlocoDoc> Enchimento(int linhas)
    {
        var b = new List<BlocoDoc>();
        for (int i = 0; i < linhas; i++)
        {
            b.Add(new BlocoDoc.Linha([new CampoDoc("CAMPO", $"linha {i}")]));
        }

        return b;
    }

    [Fact]
    public void Titulo_de_secao_nao_fica_sozinho_no_pe_da_pagina()
    {
        // 35 linhas de 8 mm deixam 7 mm no pe da A4: cabe o titulo, nao cabe
        // o titulo mais o cabecalho e a primeira linha da tabela.
        List<BlocoDoc> blocos = Enchimento(35);
        blocos.Add(new BlocoDoc.Secao("QUADRO DE TESTE"));
        blocos.Add(new BlocoDoc.Tabela([new ColunaDoc("COLUNA DE TESTE", 1f)], [["valor"]]));

        ConjuntoPaginas c = Montar(blocos);

        Pagina comTitulo = Assert.Single(c.Paginas, p => Textos(p).Contains("QUADRO DE TESTE"));
        Assert.Contains("COLUNA DE TESTE", Textos(comTitulo));
    }

    [Fact]
    public void Tabela_dividida_reabre_com_o_titulo_do_quadro()
    {
        var linhas = Enumerable.Range(0, 120).Select(i => new[] { $"item {i}" }).ToList();

        ConjuntoPaginas c = Montar(
        [
            new BlocoDoc.Secao("QUADRO LONGO"),
            new BlocoDoc.Tabela([new ColunaDoc("COLUNA", 1f)], linhas, TituloNaContinuacao: "QUADRO LONGO"),
        ]);

        Assert.True(c.Total > 1);
        Assert.All(c.Paginas, p => Assert.Contains("QUADRO LONGO", Textos(p)));
    }
}
