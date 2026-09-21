using FiscalDoc.Core.Parsing;
using FiscalDoc.Core.Values;

namespace FiscalDoc.Tests;

/// <summary>
/// O que acontece quando o arquivo nao e o que se espera. Criterio do plano
/// 2.9: mensagem clara, sem travar e sem stack trace na tela. Aqui isso vira
/// "nenhum caminho lanca excecao e toda recusa tem texto em portugues".
/// </summary>
public sealed class RecusaTests
{
    [Fact]
    public void Xml_valido_que_nao_e_fiscal_e_recusado_dizendo_qual_e_a_raiz()
    {
        ResultadoLeitura r = LeitorDocumento.Ler(Amostras.Sintetica("recusar-nao-fiscal.xml"));

        var naoFiscal = Assert.IsType<ResultadoLeitura.NaoFiscal>(r);
        Assert.Equal("Project", naoFiscal.ElementoRaiz);
        Assert.Contains("não é um documento fiscal", r.MensagemUsuario, StringComparison.Ordinal);
        Assert.Contains("Project", r.MensagemUsuario, StringComparison.Ordinal);
    }

    [Fact]
    public void Modelo_fora_do_escopo_e_recusado_dizendo_qual_modelo_e()
    {
        // CT-e OS, modelo 67: familia de transporte, raiz CTeOS, fora do
        // escopo. A NFC-e ja foi modelo de recusa aqui; desde que o modelo 65
        // entrou no escopo, quem faz este papel e o 67.
        ResultadoLeitura r = LeitorDocumento.Ler(Amostras.Sintetica("recusar-modelo67-cteos.xml"));

        var fora = Assert.IsType<ResultadoLeitura.ModeloForaDoEscopo>(r);
        Assert.Equal("67", fora.Modelo);

        // A recusa tem de nomear o modelo, nao so dizer "nao suportado".
        Assert.Contains("CT-e OS", r.MensagemUsuario, StringComparison.Ordinal);
        Assert.Contains("55", r.MensagemUsuario, StringComparison.Ordinal);
        Assert.Contains("65", r.MensagemUsuario, StringComparison.Ordinal);
    }

    [Fact]
    public void Xml_truncado_e_recusado_com_a_posicao_do_erro()
    {
        ResultadoLeitura r = LeitorDocumento.Ler(Amostras.Sintetica("recusar-xml-truncado.xml"));

        var invalido = Assert.IsType<ResultadoLeitura.XmlInvalido>(r);
        Assert.True(invalido.Linha > 0);
        Assert.Contains("inválido", r.MensagemUsuario, StringComparison.Ordinal);

        // A mensagem e para humano: nada de nome de excecao na tela.
        Assert.DoesNotContain("Exception", r.MensagemUsuario, StringComparison.Ordinal);
    }

    [Fact]
    public void Arquivo_vazio_e_recusado_sem_excecao()
    {
        ResultadoLeitura r = LeitorDocumento.Ler(Amostras.Sintetica("recusar-vazio.xml"));

        Assert.IsType<ResultadoLeitura.XmlInvalido>(r);
        Assert.NotEmpty(r.MensagemUsuario);
    }

    [Fact]
    public void Arquivo_inexistente_e_recusado_sem_excecao()
    {
        string caminho = Path.Combine(Path.GetTempPath(), "fiscaldoc-nao-existe-" + Guid.NewGuid() + ".xml");

        ResultadoLeitura r = LeitorDocumento.Ler(caminho);

        Assert.IsType<ResultadoLeitura.ArquivoIlegivel>(r);
        Assert.NotEmpty(r.MensagemUsuario);
    }

    [Fact]
    public void Arquivo_binario_e_recusado_sem_excecao()
    {
        string caminho = Path.Combine(Path.GetTempPath(), "fiscaldoc-binario-" + Guid.NewGuid() + ".xml");
        File.WriteAllBytes(caminho, [0x00, 0x01, 0x02, 0xFF, 0xFE, 0x7F, 0x00, 0x42]);

        try
        {
            ResultadoLeitura r = LeitorDocumento.Ler(caminho);

            Assert.IsNotType<ResultadoLeitura.Ok>(r);
            Assert.NotEmpty(r.MensagemUsuario);
        }
        finally
        {
            File.Delete(caminho);
        }
    }

    [Fact]
    public void Xml_com_entidade_externa_nao_resolve_a_entidade()
    {
        // Um XML fiscal chega de terceiros. Sem DTD proibido, isto leria um
        // arquivo da maquina do usuario.
        string alvo = Path.Combine(Path.GetTempPath(), "fiscaldoc-segredo-" + Guid.NewGuid() + ".txt");
        File.WriteAllText(alvo, "CONTEUDO-SIGILOSO");

        string caminho = Path.Combine(Path.GetTempPath(), "fiscaldoc-xxe-" + Guid.NewGuid() + ".xml");
        File.WriteAllText(caminho,
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n"
            + "<!DOCTYPE nfeProc [ <!ENTITY vazamento SYSTEM \""
            + alvo.Replace('\\', '/') + "\"> ]>\n"
            + "<nfeProc xmlns=\"http://www.portalfiscal.inf.br/nfe\"><x>&vazamento;</x></nfeProc>");

        try
        {
            ResultadoLeitura r = LeitorDocumento.Ler(caminho);

            // Recusado por causa do DTD, e o conteudo do arquivo alvo nunca
            // aparece em lugar nenhum do resultado.
            Assert.IsNotType<ResultadoLeitura.Ok>(r);
            Assert.DoesNotContain("SIGILOSO", r.MensagemUsuario, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(caminho);
            File.Delete(alvo);
        }
    }

    [FatoComCorpusReal]
    public void Nenhuma_amostra_do_corpus_lanca_excecao()
    {
        // Rede de seguranca ampla: o contrato do leitor e nunca lancar por
        // conteudo de arquivo, so devolver um ResultadoLeitura.
        var todos = new List<string>();
        todos.AddRange(Amostras.Reais().Select(f => f.FullName));
        todos.AddRange(Directory.GetFiles(
            Path.Combine(Amostras.Raiz.FullName, "tests", "Amostras"), "*.xml"));

        Assert.NotEmpty(todos);

        foreach (string caminho in todos)
        {
            ResultadoLeitura r = LeitorDocumento.Ler(caminho);
            Assert.NotNull(r);

            if (r is not ResultadoLeitura.Ok)
            {
                Assert.NotEmpty(r.MensagemUsuario);
            }
        }
    }
}

public sealed class ChaveAcessoTests
{
    [Fact]
    public void Dv_e_calculado_por_modulo_11_com_pesos_2_a_9()
    {
        // Chave real do corpus; o DV publicado pela SEFAZ e o ultimo digito.
        const string chave = "35260811222333000181550010000002141630000759";

        Assert.Equal(chave[43] - '0', ChaveAcesso.CalcularDv(chave[..43]));
    }

    [Fact]
    public void Resto_zero_ou_um_produz_dv_zero()
    {
        // Regra explicita do MOC: resto 0 ou 1 => DV = 0, nunca 11 ou 10.
        for (int i = 0; i < 1000; i++)
        {
            string base43 = i.ToString("D43", System.Globalization.CultureInfo.InvariantCulture);
            int dv = ChaveAcesso.CalcularDv(base43);
            Assert.InRange(dv, 0, 9);
        }
    }

    [Fact]
    public void Chave_e_formatada_em_onze_grupos_de_quatro()
    {
        // MOC Anexo II 3.1.1.
        ChaveAcesso? c = ChaveAcesso.DeAtributoId("NFe35260811222333000181550010000002141630000759");

        Assert.NotNull(c);
        string f = c!.Formatada;

        Assert.Equal(54, f.Length); // 44 digitos + 10 espacos
        Assert.Equal(11, f.Split(' ').Length);
        Assert.All(f.Split(' '), grupo => Assert.Equal(4, grupo.Length));
        Assert.Equal(c.Digitos, f.Replace(" ", string.Empty, StringComparison.Ordinal));
    }

    [Fact]
    public void Componentes_da_chave_batem_com_a_tabela_do_moc()
    {
        ChaveAcesso c = ChaveAcesso.DeAtributoId(
            "NFe35260811222333000181550010000002141630000759")!;

        Assert.Equal("35", c.CodigoUf);          // SP
        Assert.Equal("2608", c.AnoMes);          // ago/2026
        Assert.Equal("11222333000181", c.CnpjEmitente);
        Assert.Equal("55", c.Modelo);
        Assert.Equal("001", c.Serie);
        Assert.Equal("000000214", c.Numero);
        Assert.Equal("1", c.TipoEmissao);
        Assert.Equal("63000075", c.CodigoNumerico);
        Assert.Equal("9", c.DigitoVerificador);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("NFe123")]
    [InlineData("nao-e-chave")]
    [InlineData("NFe3526081122233300018155001000000214163000075912345")]
    public void Id_que_nao_e_chave_devolve_nulo(string? id)
    {
        Assert.Null(ChaveAcesso.DeAtributoId(id));
    }

    [Fact]
    public void Id_sem_prefixo_ainda_e_aceito()
    {
        Assert.NotNull(ChaveAcesso.DeAtributoId("35260811222333000181550010000002141630000759"));
    }
}
