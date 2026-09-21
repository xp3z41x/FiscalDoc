using FiscalDoc.Core.Model.Nfe;
using FiscalDoc.Core.Parsing;

namespace FiscalDoc.Tests;

/// <summary>
/// Leitura das 10 NF-e reais, modelo 55. Estes testes sao o criterio de
/// aceitacao da Fase 1 do plano.
///
/// As NFC-e do corpus - modelo 65, mesmo parser, outro documento auxiliar -
/// tem suite propria em <see cref="LeituraNfceTests"/>.
/// </summary>
public sealed class LeituraNfeTests
{
    public static TheoryData<string> TodasAsReais()
    {
        var dados = new TheoryData<string>();
        foreach (FileInfo f in Amostras.ReaisNfeSemGuarda())
        {
            dados.Add(f.Name);
        }

        // Sem corpus real o provedor ainda precisa devolver UMA linha: teoria
        // com zero linhas e FALHA no xUnit, e o que se quer aqui e "ignorado".
        // O corpo do teste chama ExigirCorpusReal e a linha vira ignorada.
        if (dados.Count == 0)
        {
            dados.Add(string.Empty);
        }

        return dados;
    }

    private static NfeDocumento LerReal(string nome)
    {
        Amostras.ExigirCorpusReal();

        ResultadoLeitura r = LeitorDocumento.Ler(
            Path.Combine(Amostras.Raiz.FullName, "Exemplos XML", nome));

        var ok = Assert.IsType<ResultadoLeitura.Ok>(r);
        return Assert.IsType<NfeDocumento>(ok.Documento);
    }

    [FatoComCorpusReal]
    public void Corpus_real_tem_dez_amostras()
    {
        Assert.Equal(10, Amostras.ReaisNfe().Length);
    }

    [TeoriaComCorpusReal]
    [MemberData(nameof(TodasAsReais))]
    public void Toda_amostra_real_e_lida_como_nfe(string nome)
    {
        NfeDocumento nfe = LerReal(nome);

        Assert.Equal("4.00", nfe.VersaoLeiaute);
        Assert.Equal("55", nfe.Ide.Modelo);
        Assert.NotEmpty(nfe.Itens);
    }

    [TeoriaComCorpusReal]
    [MemberData(nameof(TodasAsReais))]
    public void Toda_amostra_real_tem_chave_de_44_digitos_com_dv_valido(string nome)
    {
        NfeDocumento nfe = LerReal(nome);

        Assert.NotNull(nfe.Chave);
        Assert.Equal(44, nfe.Chave!.Digitos.Length);
        Assert.All(nfe.Chave.Digitos, c => Assert.InRange(c, '0', '9'));

        // O DV das chaves reais tem de fechar: se o modulo 11 estiver errado,
        // esta errado no nosso codigo, nao no arquivo da SEFAZ.
        Assert.True(
            nfe.Chave.DigitoVerificadorConfere,
            $"DV nao confere para a chave {nfe.Chave.Digitos}");

        // O modelo embutido na chave tem de bater com o campo ide/mod.
        Assert.Equal(nfe.Ide.Modelo, nfe.Chave.Modelo);
    }

    [TeoriaComCorpusReal]
    [MemberData(nameof(TodasAsReais))]
    public void Toda_amostra_real_tem_emitente_destinatario_e_totais(string nome)
    {
        NfeDocumento nfe = LerReal(nome);

        Assert.False(string.IsNullOrWhiteSpace(nfe.Emitente.RazaoSocial));
        Assert.NotNull(nfe.Emitente.Documento);
        Assert.NotNull(nfe.Emitente.Endereco.Municipio);

        Assert.False(string.IsNullOrWhiteSpace(nfe.Destinatario.RazaoSocial));
        Assert.NotNull(nfe.Destinatario.Documento);

        Assert.NotNull(nfe.Totais.ValorTotalNota);
        Assert.True(nfe.Totais.ValorTotalNota > 0);
    }

    [TeoriaComCorpusReal]
    [MemberData(nameof(TodasAsReais))]
    public void Todo_item_tem_as_colunas_que_o_moc_proibe_suprimir(string nome)
    {
        NfeDocumento nfe = LerReal(nome);

        // MOC Anexo II 3.1.7: Codigo, Descricao, NCM, CST, CFOP, Unidade,
        // Quantidade, Valor Unitario e Valor Total nunca podem ser suprimidos.
        foreach (ItemNfe item in nfe.Itens)
        {
            Assert.NotNull(item.Codigo);
            Assert.NotNull(item.Descricao);
            Assert.NotNull(item.Ncm);
            Assert.NotNull(item.Cfop);
            Assert.NotNull(item.Unidade);
            Assert.NotNull(item.Quantidade);
            Assert.NotNull(item.ValorUnitario);
            Assert.NotNull(item.ValorTotal);

            // A coluna CST do DANFE e origem + CST (ou CSOSN).
            Assert.NotNull(item.Icms.CstOuCsosnFormatado);
        }
    }

    [TeoriaComCorpusReal]
    [MemberData(nameof(TodasAsReais))]
    public void Itens_sao_numerados_de_um_ate_n_sem_buraco(string nome)
    {
        NfeDocumento nfe = LerReal(nome);

        // A paginacao depende de nItem; um buraco aqui vira folha errada.
        int esperado = 1;
        foreach (ItemNfe item in nfe.Itens)
        {
            Assert.Equal(esperado++, item.Numero);
        }
    }

    [TeoriaComCorpusReal]
    [MemberData(nameof(TodasAsReais))]
    public void Toda_amostra_real_esta_autorizada_em_producao(string nome)
    {
        NfeDocumento nfe = LerReal(nome);

        Assert.Equal(Ambiente.Producao, nfe.Ide.Ambiente);
        Assert.Equal(TipoEmissao.Normal, nfe.Ide.TipoEmissao);
        Assert.False(nfe.SemProtocolo);
        Assert.NotNull(nfe.Protocolo!.Numero);
        Assert.Equal("100", nfe.Protocolo.CodigoStatus);
    }

    [FatoComCorpusReal]
    public void Orientacao_de_impressao_segue_o_tpImp_do_arquivo()
    {
        var porOrientacao = Amostras.ReaisNfe()
            .Select(f => LerReal(f.Name))
            .GroupBy(n => n.Ide.TipoImpressao)
            .ToDictionary(g => g.Key, g => g.Count());

        // O corpus cobre as duas orientacoes do escopo - e por isso que a
        // Fase 4 consegue validar paisagem sem amostra sintetica.
        Assert.Equal(8, porOrientacao[TipoImpressao.Retrato]);
        Assert.Equal(2, porOrientacao[TipoImpressao.Paisagem]);

        NfeDocumento paisagem = LerReal(
            Path.GetFileName(Amostras.PaisagemComMaisItens()));
        Assert.True(paisagem.Paisagem);
    }

    [FatoComCorpusReal]
    public void Amostra_de_99_itens_e_a_de_maior_paginacao()
    {
        NfeDocumento nfe = LerReal(Path.GetFileName(Amostras.Real("ZOUIL")));

        Assert.Equal(99, nfe.Itens.Count);
        Assert.Equal(99, nfe.Itens[^1].Numero);
    }

    [FatoComCorpusReal]
    public void Amostra_com_infAdProd_carrega_a_informacao_de_todos_os_itens()
    {
        NfeDocumento nfe = LerReal(Path.GetFileName(Amostras.Real("DALMASIO")));

        // 74 itens, todos com infAdProd: e o pior caso de paginacao do corpus,
        // porque cada item ocupa mais de uma linha (MOC 3.1.7).
        Assert.Equal(74, nfe.Itens.Count);
        Assert.All(nfe.Itens, i => Assert.True(i.TemInformacaoAdicional));
    }

    [FatoComCorpusReal]
    public void Descricao_mais_longa_do_corpus_e_preservada_inteira()
    {
        NfeDocumento nfe = LerReal(Path.GetFileName(Amostras.Real("DALMASIO")));

        int maior = nfe.Itens.Max(i => i.Descricao!.Length);

        // 118 caracteres: forca quebra de linha na coluna Descricao.
        Assert.Equal(118, maior);
    }

    [FatoComCorpusReal]
    public void Grupos_de_icms_diferentes_sao_todos_achatados_para_as_mesmas_colunas()
    {
        // O corpus tem ICMS00, ICMS20, ICMS40, ICMSSN101 e ICMSSN102. O DANFE
        // imprime as mesmas colunas para todos.
        var vistos = new HashSet<string>(StringComparer.Ordinal);

        foreach (FileInfo f in Amostras.ReaisNfe())
        {
            foreach (ItemNfe item in LerReal(f.Name).Itens)
            {
                if (item.Icms.Cst is not null)
                {
                    vistos.Add("CST" + item.Icms.Cst);
                }

                if (item.Icms.Csosn is not null)
                {
                    vistos.Add("CSOSN" + item.Icms.Csosn);
                }
            }
        }

        // Pelo menos um CST e um CSOSN aparecem, provando que as duas formas
        // do grupo de escolha foram lidas.
        Assert.Contains(vistos, v => v.StartsWith("CST", StringComparison.Ordinal));
        Assert.Contains(vistos, v => v.StartsWith("CSOSN", StringComparison.Ordinal));
    }

    [FatoComCorpusReal]
    public void Reforma_tributaria_ibs_cbs_e_lida_quando_presente()
    {
        // 9 das 10 amostras de 2026 ja trazem IBSCBS. O parser le; onde isso
        // aparece no papel e decisao do layout, nao do parser.
        int comIbsCbs = Amostras.ReaisNfe()
            .Select(f => LerReal(f.Name))
            .Count(n => n.IbsCbs is not null);

        Assert.Equal(9, comIbsCbs);
    }
}
