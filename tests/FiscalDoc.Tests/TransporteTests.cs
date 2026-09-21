using FiscalDoc.Core.Model;
using FiscalDoc.Core.Model.Cte;
using FiscalDoc.Core.Model.Evento;
using FiscalDoc.Core.Model.Mdfe;
using FiscalDoc.Core.Model.Nfe;
using FiscalDoc.Core.Parsing;
using FiscalDoc.Layout.Dacte;
using FiscalDoc.Layout.Damdfe;
using FiscalDoc.Layout.DisplayList;
using FiscalDoc.Layout.Evento;
using FiscalDoc.Render.Wpf;

namespace FiscalDoc.Tests;

/// <summary>
/// CT-e, MDF-e e eventos.
///
/// As amostras sao sinteticas, geradas por
/// tools/gerar-amostras-transporte.py a partir da estrutura dos schemas
/// oficiais - nao foi possivel obter exemplares publicos reais. Sao
/// estruturalmente fieis, mas nao substituem conferencia contra documentos de
/// verdade; quando houver, vale repassar.
/// </summary>
public sealed class TransporteTests
{
    private static T Ler<T>(string arquivo) where T : DocumentoFiscal
    {
        ResultadoLeitura r = LeitorDocumento.Ler(Amostras.Sintetica(arquivo));
        var ok = Assert.IsType<ResultadoLeitura.Ok>(r);
        return Assert.IsType<T>(ok.Documento);
    }

    // ================================================================ CT-e

    [Theory]
    [InlineData("cte-400-rodoviario.xml", "4.00")]
    [InlineData("cte-300-rodoviario.xml", "3.00")]
    public void Cte_das_duas_versoes_e_lido_pelo_mesmo_caminho(string arquivo, string versao)
    {
        // O plano decidiu suportar 3.00 e 4.00: a guarda fiscal e de 5 anos e
        // a 4.00 so entrou em producao em 06/2023.
        CteDocumento cte = Ler<CteDocumento>(arquivo);

        Assert.Equal(versao, cte.VersaoLeiaute);
        Assert.Equal(FamiliaDocumento.Cte, cte.Familia);
        Assert.NotNull(cte.Chave);
        Assert.Equal("57", cte.Chave!.Modelo);
        Assert.True(cte.Chave.DigitoVerificadorConfere);
    }

    [Fact]
    public void Cte_carrega_participantes_valores_e_documentos()
    {
        CteDocumento cte = Ler<CteDocumento>("cte-400-rodoviario.xml");

        // Valor produzido pela anonimizacao das amostras
        // (tools/anonimizar.py): as amostras vao para o repositorio
        // publico e nao podem carregar identificacao real.
        Assert.Equal("INDUSTRIA E COMERCIO ZENITE SA", cte.Emitente.RazaoSocial);
        Assert.NotNull(cte.Remetente);
        Assert.NotNull(cte.Destinatario);

        Assert.Equal(2202.10m, cte.ValorTotalPrestacao);
        Assert.Equal(4, cte.Componentes.Count);
        Assert.Equal("FRETE PESO", cte.Componentes[0].Nome);

        Assert.Equal(6, cte.DocumentosOriginarios.Count);
        Assert.All(cte.DocumentosOriginarios, d => Assert.Equal("NF-e", d.Tipo));

        Assert.Equal(ModalCte.Rodoviario, cte.Modal);
        Assert.Equal("12345678", cte.Rntrc);
        Assert.Equal(2, cte.Quantidades.Count);
    }

    [Fact]
    public void Cte_identifica_o_tomador_pelo_grupo_toma3()
    {
        CteDocumento cte = Ler<CteDocumento>("cte-400-rodoviario.xml");

        Assert.Equal(3, cte.CodigoTomador);
        Assert.Equal("Destinatário", cte.NomeTomador);
    }

    [Fact]
    public void Cte_achata_o_grupo_de_escolha_do_icms()
    {
        CteDocumento cte = Ler<CteDocumento>("cte-400-rodoviario.xml");

        Assert.Equal("00", cte.Icms.Cst);
        Assert.Equal(2202.10m, cte.Icms.BaseCalculo);
        Assert.Equal(12.00m, cte.Icms.Aliquota);
        Assert.Equal(264.25m, cte.Icms.Valor);
    }

    [Fact]
    public void Cte_em_contingencia_carrega_motivo_e_data()
    {
        CteDocumento cte = Ler<CteDocumento>("cte-400-contingencia.xml");

        Assert.Equal(TipoEmissao.ContingenciaFsDa, cte.TipoEmissao);
        Assert.NotNull(cte.DataHoraContingencia);
        Assert.NotNull(cte.JustificativaContingencia);
    }

    [Fact]
    public void Cte_em_homologacao_exige_sem_valor_fiscal()
    {
        CteDocumento cte = Ler<CteDocumento>("cte-400-homologacao.xml");

        Assert.Equal(Ambiente.Homologacao, cte.Ambiente);
        Assert.True(cte.ExigeSemValorFiscal);
    }

    [Fact]
    public void Cte_os_modelo_67_e_recusado_dizendo_qual_modelo_e()
    {
        // Modelo 67 esta fora do escopo, mas a recusa precisa nomea-lo.
        string caminho = Path.Combine(Path.GetTempPath(), $"cteos-{Guid.NewGuid()}.xml");
        File.WriteAllText(caminho,
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>"
            + "<cteOSProc versao=\"4.00\" xmlns=\"http://www.portalfiscal.inf.br/cte\">"
            + "<CTeOS><infCte versao=\"4.00\" Id=\"CTe41260911222333000181670010000917231741139202\">"
            + "<ide><mod>67</mod></ide></infCte></CTeOS></cteOSProc>");

        try
        {
            ResultadoLeitura r = LeitorDocumento.Ler(caminho);
            var fora = Assert.IsType<ResultadoLeitura.ModeloForaDoEscopo>(r);
            Assert.Equal("67", fora.Modelo);
            Assert.Contains("CT-e OS", r.MensagemUsuario, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(caminho);
        }
    }

    // =============================================================== MDF-e

    [Fact]
    public void Mdfe_carrega_veiculo_condutores_e_documentos()
    {
        MdfeDocumento m = Ler<MdfeDocumento>("mdfe-300-rodoviario.xml");

        Assert.Equal(FamiliaDocumento.Mdfe, m.Familia);
        Assert.Equal("58", m.Chave!.Modelo);
        Assert.True(m.Chave.DigitoVerificadorConfere);

        Assert.Equal(ModalMdfe.Rodoviario, m.Modal);
        Assert.Equal("12345678", m.Rntrc);

        Assert.NotNull(m.VeiculoTracao);
        Assert.Equal("ABC1D23", m.VeiculoTracao!.Placa);
        Assert.Equal(2, m.VeiculoTracao.Condutores.Count);
        Assert.Single(m.Reboques);

        Assert.Equal(9, m.Documentos.Count);
        Assert.Equal(2, m.Lacres.Count);
    }

    [Fact]
    public void Mdfe_agrupa_documentos_por_municipio_de_descarga()
    {
        // E como o DAMDFE os imprime: o motorista precisa ver o que entrega onde.
        MdfeDocumento m = Ler<MdfeDocumento>("mdfe-300-rodoviario.xml");

        var municipios = m.Documentos
            .Select(d => d.Municipio)
            .Distinct()
            .ToList();

        Assert.Equal(3, municipios.Count);
        Assert.Contains("SAO PAULO", municipios);
        Assert.Contains("CAMPINAS", municipios);
        Assert.Contains("RIBEIRAO PRETO", municipios);
    }

    [Fact]
    public void Mdfe_em_contingencia_exige_o_dizer_do_moc()
    {
        MdfeDocumento m = Ler<MdfeDocumento>("mdfe-300-contingencia.xml");

        // MOC MDF-e 2.5: "EMISSÃO EM CONTINGÊNCIA" em destaque, no lugar
        // reservado ao protocolo.
        Assert.True(m.EmContingencia);
        Assert.NotNull(m.JustificativaContingencia);
    }

    // ============================================================= eventos

    [Fact]
    public void Carta_de_correcao_carrega_texto_e_condicoes_de_uso()
    {
        EventoDocumento ev = Ler<EventoDocumento>("evento-nfe-cce.xml");

        Assert.Equal(FamiliaDocumento.EventoNfe, ev.Familia);
        Assert.Equal(TiposEvento.CartaCorrecao, ev.CodigoEvento);
        Assert.NotNull(ev.ChaveReferenciada);
        Assert.Equal("55", ev.ChaveReferenciada!.Modelo);

        Assert.NotNull(ev.TextoCorrecao);

        // O xCondUso e obrigacao do ARQUIVO, nao de um layout: o MOC nao define
        // representacao grafica de evento nenhum. O que o app garante e
        // imprimi-lo na integra, como veio - inclusive na variante sem acentos.
        Assert.NotNull(ev.CondicoesUso);
        Assert.Contains("Convenio S/N", ev.CondicoesUso!, StringComparison.Ordinal);
        Assert.Contains("III", ev.CondicoesUso!, StringComparison.Ordinal);

        Assert.True(ev.Registrado);
        Assert.Equal("135", ev.Retorno!.CodigoStatus);
    }

    [Fact]
    public void Cancelamento_carrega_justificativa_e_protocolo_referenciado()
    {
        EventoDocumento ev = Ler<EventoDocumento>("evento-nfe-cancelamento.xml");

        Assert.Equal("110111", ev.CodigoEvento);
        Assert.NotNull(ev.Justificativa);
        // Valor produzido pela anonimizacao das amostras
        // (tools/anonimizar.py): as amostras vao para o repositorio
        // publico e nao podem carregar identificacao real.
        Assert.Equal("999999999999990", ev.ProtocoloReferenciado);
        Assert.Null(ev.TextoCorrecao);
    }

    [Fact]
    public void Evento_de_cte_e_reconhecido_pela_familia_certa()
    {
        EventoDocumento ev = Ler<EventoDocumento>("evento-cte-entrega.xml");

        Assert.Equal(FamiliaDocumento.EventoCte, ev.Familia);
        Assert.Equal(FamiliaDocumento.Cte, ev.FamiliaOrigem);
        Assert.Equal("110160", ev.CodigoEvento);
        Assert.Equal("57", ev.ChaveReferenciada!.Modelo);
    }

    [Fact]
    public void Campos_desconhecidos_do_evento_viram_pares_rotulo_valor()
    {
        // Um tipo de evento novo, criado por Nota Tecnica, precisa aparecer
        // legivel no papel sem alteracao de codigo.
        EventoDocumento ev = Ler<EventoDocumento>("evento-cte-entrega.xml");

        Assert.NotEmpty(ev.Detalhes);

        var rotulos = ev.Detalhes.Select(d => d.Rotulo).ToList();
        Assert.Contains("Data/hora da entrega", rotulos);
        Assert.Contains("Nome", rotulos);
        Assert.Contains("Latitude", rotulos);

        // O que ja tem lugar proprio no desenho nao se repete aqui.
        Assert.DoesNotContain("descEvento", rotulos);
    }

    // ============================================================== layout

    [Fact]
    public void Os_tres_layouts_produzem_paginas_dentro_do_papel()
    {
        var medidor = new MedidorTextoWpf();

        (string Arquivo, Func<DocumentoFiscal, ConjuntoPaginas> Montar)[] casos =
        [
            ("cte-400-muitos-documentos.xml", d => DacteLayout.Construir((CteDocumento)d, medidor)),
            ("mdfe-300-muitos-documentos.xml", d => DamdfeLayout.Construir((MdfeDocumento)d, medidor)),
            ("evento-nfe-cce.xml", d => EventoLayout.Construir((EventoDocumento)d, medidor)),
        ];

        foreach ((string arquivo, var montar) in casos)
        {
            var ok = Assert.IsType<ResultadoLeitura.Ok>(
                LeitorDocumento.Ler(Amostras.Sintetica(arquivo)));

            ConjuntoPaginas c = montar(ok.Documento);

            Assert.True(c.Total >= 1, $"{arquivo}: nenhuma pagina");
            Assert.All(c.Paginas, p => Assert.NotEmpty(p.Primitivas));

            // A extensao usada tem de caber no papel declarado - e o que a
            // escala de impressao compara contra a area imprimivel.
            Assert.True(c.ExtensaoUsada.Direita <= c.Papel.LarguraMm + 0.01f,
                $"{arquivo}: conteudo mais largo que o papel");
            Assert.True(c.ExtensaoUsada.Base <= c.Papel.AlturaMm + 0.01f,
                $"{arquivo}: conteudo mais alto que o papel");
        }
    }

    [Fact]
    public void Tabela_longa_pagina_e_repete_o_cabecalho_das_colunas()
    {
        var medidor = new MedidorTextoWpf();

        MdfeDocumento m = Ler<MdfeDocumento>("mdfe-300-muitos-documentos.xml");
        ConjuntoPaginas c = DamdfeLayout.Construir(m, medidor);

        // 90 documentos nao cabem numa folha.
        Assert.True(c.Total > 1, "esperava paginacao com 90 documentos");

        // Toda pagina traz o cabecalho repetido - o MOC do MDF-e e o do CT-e
        // exigem isso nas folhas adicionais.
        foreach (Pagina p in c.Paginas)
        {
            bool temChave = p.Primitivas.Any(
                x => x is Primitiva.Texto t
                     && t.Conteudo.Contains("CHAVE DE ACESSO", StringComparison.Ordinal));

            Assert.True(temChave, $"pagina {p.Numero} sem cabecalho repetido");
        }
    }

    [Fact]
    public void Folha_n_de_m_aparece_em_toda_pagina()
    {
        var medidor = new MedidorTextoWpf();

        CteDocumento cte = Ler<CteDocumento>("cte-400-muitos-documentos.xml");
        ConjuntoPaginas c = DacteLayout.Construir(cte, medidor);

        for (int i = 0; i < c.Total; i++)
        {
            string esperado = $"FOLHA {i + 1:00}/{c.Total:00}";

            bool achou = c.Paginas[i].Primitivas.Any(
                x => x is Primitiva.Texto t && t.Conteudo == esperado);

            Assert.True(achou, $"nao achei \"{esperado}\" na pagina {i + 1}");
        }
    }
}
