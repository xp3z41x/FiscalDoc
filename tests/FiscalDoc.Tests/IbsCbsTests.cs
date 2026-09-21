using FiscalDoc.Core.Model.Nfe;
using FiscalDoc.Core.Parsing;
using FiscalDoc.Layout.Danfe;
using FiscalDoc.Layout.DisplayList;
using FiscalDoc.Render.Wpf;

namespace FiscalDoc.Tests;

/// <summary>
/// IBS e CBS da Reforma Tributária no DANFE.
///
/// O MOC 7.00 Anexo II é de 2020 e não tem quadro para estes valores — o
/// layout foi publicado cinco anos antes de os grupos existirem no XML. O
/// bloco é acréscimo, e estes testes fixam o comportamento: aparece quando o
/// documento traz o grupo, some quando não traz, e nunca imprime valor que
/// não esteja no arquivo.
/// </summary>
public sealed class IbsCbsTests
{
    private static NfeDocumento LerReal(string parcial) =>
        LerCaminho(Amostras.Real(parcial));

    private static NfeDocumento LerCaminho(string caminho)
    {
        var ok = Assert.IsType<ResultadoLeitura.Ok>(LeitorDocumento.Ler(caminho));
        return Assert.IsType<NfeDocumento>(ok.Documento);
    }

    private static List<string> TextosDaPagina(Pagina p)
    {
        var t = new List<string>();
        Coletar(p.Primitivas, t);
        return t;

        static void Coletar(IReadOnlyList<Primitiva> ps, List<string> saida)
        {
            foreach (Primitiva x in ps)
            {
                switch (x)
                {
                    case Primitiva.Texto txt:
                        saida.Add(txt.Conteudo);
                        break;
                    case Primitiva.Rotacionado r:
                        Coletar(r.Filhos, saida);
                        break;
                }
            }
        }
    }

    [FatoComCorpusReal]
    public void Nove_das_dez_amostras_trazem_o_grupo_ibs_cbs()
    {
        // O corpus é de 2026: a reforma já aparece nos arquivos reais.
        int com = Amostras.ReaisNfe()
            .Select(f => LeitorDocumento.Ler(f.FullName))
            .OfType<ResultadoLeitura.Ok>()
            .Select(o => (NfeDocumento)o.Documento)
            .Count(n => n.IbsCbs is not null);

        Assert.Equal(9, com);
    }

    [FatoComCorpusReal]
    public void Totais_batem_com_as_aliquotas_de_transicao()
    {
        // 2026 é o ano de teste da reforma: IBS estadual 0,1%, IBS municipal
        // 0,0% e CBS 0,9%. Se o parser estivesse lendo o campo errado, a
        // proporção não fecharia.
        NfeDocumento nfe = LerReal("RENASCENCA");

        Assert.NotNull(nfe.IbsCbs);
        TotaisIbsCbs t = nfe.IbsCbs!;

        Assert.NotNull(t.BaseCalculo);
        decimal bc = t.BaseCalculo!.Value;

        Assert.Equal(Math.Round(bc * 0.001m, 2), t.ValorIbsUf!.Value, 1);
        Assert.Equal(0m, t.ValorIbsMun);
        Assert.Equal(t.ValorIbsUf, t.ValorIbs);
        Assert.Equal(Math.Round(bc * 0.009m, 2), t.ValorCbs!.Value, 1);
    }

    [FatoComCorpusReal]
    public void Item_carrega_aliquotas_e_valores_proprios()
    {
        NfeDocumento nfe = LerReal("RENASCENCA");

        foreach (ItemNfe item in nfe.Itens)
        {
            Assert.NotNull(item.IbsCbs);
            IbsCbsItem i = item.IbsCbs!;

            Assert.NotNull(i.Cst);
            Assert.NotNull(i.ClassificacaoTributaria);
            Assert.NotNull(i.BaseCalculo);

            // As três alíquotas do ano de transição.
            Assert.Equal(0.1000m, i.AliquotaIbsUf);
            Assert.Equal(0.0000m, i.AliquotaIbsMun);
            Assert.Equal(0.9000m, i.AliquotaCbs);

            // vIBS do item é a soma das duas parcelas.
            Assert.Equal((i.ValorIbsUf ?? 0m) + (i.ValorIbsMun ?? 0m), i.ValorIbs);
        }
    }

    [FatoComCorpusReal]
    public void Bloco_aparece_no_danfe_quando_o_documento_traz_o_grupo()
    {
        var medidor = new MedidorTextoWpf();

        NfeDocumento nfe = LerReal("RENASCENCA");
        ConjuntoPaginas c = DanfeRetrato.Construir(nfe, medidor);

        List<string> textos = TextosDaPagina(c.Paginas[0]);

        Assert.Contains("TRIBUTOS DA REFORMA TRIBUTÁRIA (LC 214/2025)", textos);
        Assert.Contains("BASE DE CÁLCULO IBS/CBS", textos);
        Assert.Contains("IBS ESTADUAL", textos);
        Assert.Contains("IBS MUNICIPAL", textos);

        // Redação da NT 2026.003, Divisão III-A - a única que um fisco
        // brasileiro publicou para imprimir estes tributos num documento
        // auxiliar de NF-e. O "(+)" carrega a semântica de tributo por fora.
        Assert.Contains("(+) IBS R$", textos);
        Assert.Contains("(+) CBS R$", textos);

        // E os valores impressos são os do arquivo, não recalculados.
        Assert.Contains(Core.Values.Formatos.Moeda(nfe.IbsCbs!.ValorCbs), textos);
        Assert.Contains(Core.Values.Formatos.Moeda(nfe.IbsCbs.ValorIbs), textos);
    }

    [FatoComCorpusReal]
    public void Bloco_some_por_inteiro_quando_nao_ha_grupo()
    {
        var medidor = new MedidorTextoWpf();

        // A única amostra do corpus sem IBSCBS. Uma nota anterior à reforma
        // não pode pagar por um quadro que não teria.
        NfeDocumento semIbs = Amostras.ReaisNfe()
            .Select(f => LeitorDocumento.Ler(f.FullName))
            .OfType<ResultadoLeitura.Ok>()
            .Select(o => (NfeDocumento)o.Documento)
            .First(n => n.IbsCbs is null);

        ConjuntoPaginas c = DanfeRetrato.Construir(semIbs, medidor);
        List<string> textos = TextosDaPagina(c.Paginas[0]);

        Assert.DoesNotContain("TRIBUTOS DA REFORMA TRIBUTÁRIA (LC 214/2025)", textos);
        Assert.DoesNotContain("(+) CBS R$", textos);
    }

    [FatoComCorpusReal]
    public void Bloco_sai_do_quadro_de_produtos_e_nao_estoura_a_folha()
    {
        var medidor = new MedidorTextoWpf();

        // O bloco custa 12,7 mm, tirados do quadro de produtos, que é o
        // elástico. O conteúdo tem de continuar dentro do papel.
        foreach (FileInfo f in Amostras.ReaisNfe())
        {
            var ok = Assert.IsType<ResultadoLeitura.Ok>(LeitorDocumento.Ler(f.FullName));
            var nfe = (NfeDocumento)ok.Documento;

            ConjuntoPaginas c = nfe.Paisagem
                ? DanfePaisagem.Construir(nfe, medidor)
                : DanfeRetrato.Construir(nfe, medidor);

            Assert.True(
                c.ExtensaoUsada.Base <= c.Papel.AlturaMm + 0.01f,
                $"{f.Name}: conteúdo mais alto que o papel");

            Assert.All(c.Paginas, p => Assert.NotEmpty(p.Primitivas));
        }
    }

    [FatoComCorpusReal]
    public void Paisagem_tambem_imprime_o_bloco()
    {
        var medidor = new MedidorTextoWpf();

        NfeDocumento nfe = LerCaminho(Amostras.PaisagemComMaisItens());
        Assert.True(nfe.Paisagem);
        Assert.NotNull(nfe.IbsCbs);

        ConjuntoPaginas c = DanfePaisagem.Construir(nfe, medidor);
        List<string> textos = TextosDaPagina(c.Paginas[0]);

        // Em paisagem o título vira tarja vertical e é abreviado, mas os
        // rótulos dos campos são os mesmos do retrato.
        Assert.Contains("REFORMA", textos);
        Assert.Contains("(+) IBS R$", textos);
        Assert.Contains("(+) CBS R$", textos);
    }

    [FatoComCorpusReal]
    public void Nada_e_calculado_pelo_aplicativo()
    {
        // MOC 3.1: "não poderão ser impressas informações que não constem do
        // arquivo da NF-e". Uma soma IBS+CBS seria conveniente e não existe no
        // XML - por isso não é impressa. Este teste existe para que ninguém a
        // acrescente sem perceber a regra.
        var medidor = new MedidorTextoWpf();

        NfeDocumento nfe = LerReal("RENASCENCA");
        ConjuntoPaginas c = DanfeRetrato.Construir(nfe, medidor);
        List<string> textos = TextosDaPagina(c.Paginas[0]);

        decimal soma = (nfe.IbsCbs!.ValorIbs ?? 0m) + (nfe.IbsCbs.ValorCbs ?? 0m);

        Assert.DoesNotContain(Core.Values.Formatos.Moeda(soma), textos);
        Assert.DoesNotContain("TOTAL IBS + CBS", textos);
    }

    // ================================================ campos condicionais

    private static NfeDocumento LerSintetica(string arquivo)
    {
        var ok = Assert.IsType<ResultadoLeitura.Ok>(
            LeitorDocumento.Ler(Amostras.Sintetica(arquivo)));
        return Assert.IsType<NfeDocumento>(ok.Documento);
    }

    [Fact]
    public void Imposto_seletivo_e_lido_de_fora_do_grupo_ibscbs()
    {
        // ISTot é irmão de ICMSTot dentro de <total>, e não filho de
        // IBSCBSTot. Ler no lugar errado devolveria nulo em silêncio.
        NfeDocumento nfe = LerSintetica("reforma-com-is-e-total.xml");

        Assert.NotNull(nfe.IbsCbs);
        Assert.Equal(235.14m, nfe.IbsCbs!.ValorIs);
        Assert.True(nfe.IbsCbs.TemImpostoSeletivo);
    }

    [FatoComCorpusReal]
    public void Imposto_seletivo_so_aparece_quando_existe()
    {
        var medidor = new MedidorTextoWpf();

        List<string> com = TextosDaPagina(
            DanfeRetrato.Construir(LerSintetica("reforma-com-is-e-total.xml"), medidor).Paginas[0]);
        Assert.Contains("(+) IS R$", com);

        // Nenhuma nota real do corpus tem IS: o campo não pode aparecer.
        List<string> sem = TextosDaPagina(
            DanfeRetrato.Construir(LerReal("RENASCENCA"), medidor).Paginas[0]);
        Assert.DoesNotContain("(+) IS R$", sem);
    }

    [Fact]
    public void Total_com_tributos_so_aparece_quando_diverge_do_valor_da_nota()
    {
        var medidor = new MedidorTextoWpf();

        // IBS, CBS e IS são cobrados "por fora", então vNFTot passa a divergir
        // de vNF quando forem efetivamente somados.
        NfeDocumento diverge = LerSintetica("reforma-com-is-e-total.xml");
        Assert.Equal(4334.43m, diverge.IbsCbs!.ValorTotalNotaComTributos);
        Assert.True(diverge.IbsCbs.TotalDivergeDoValorDaNota(diverge.Totais.ValorTotalNota));

        List<string> textos = TextosDaPagina(
            DanfeRetrato.Construir(diverge, medidor).Paginas[0]);
        Assert.Contains("TOTAL COM IBS/CBS/IS", textos);
        Assert.Contains(Core.Values.Formatos.Moeda(4334.43m), textos);
    }

    [FatoComCorpusReal]
    public void Total_igual_ao_valor_da_nota_nao_vira_campo_repetido()
    {
        var medidor = new MedidorTextoWpf();

        // Em 2026 o art. 348 da LC 214/2025 dispensa o recolhimento, e todo
        // documento real vem com vNFTot igual a vNF. Repetir o mesmo número
        // em dois campos do DANFE seria ruído.
        NfeDocumento nfe = LerReal("ZOUIL");

        Assert.NotNull(nfe.IbsCbs!.ValorTotalNotaComTributos);
        Assert.Equal(nfe.Totais.ValorTotalNota, nfe.IbsCbs.ValorTotalNotaComTributos);
        Assert.False(nfe.IbsCbs.TotalDivergeDoValorDaNota(nfe.Totais.ValorTotalNota));

        List<string> textos = TextosDaPagina(
            DanfeRetrato.Construir(nfe, medidor).Paginas[0]);
        Assert.DoesNotContain("TOTAL COM IBS/CBS/IS", textos);
    }

    [Fact]
    public void Bloco_com_todos_os_campos_ainda_cabe_na_folha()
    {
        var medidor = new MedidorTextoWpf();

        // Sete campos na linha, o pior caso: base, IBS UF, IBS mun, IBS, CBS,
        // IS e total com tributos.
        NfeDocumento nfe = LerSintetica("reforma-com-is-e-total.xml");
        ConjuntoPaginas c = DanfeRetrato.Construir(nfe, medidor);

        Assert.True(c.ExtensaoUsada.Direita <= c.Papel.LarguraMm + 0.01f);
        Assert.True(c.ExtensaoUsada.Base <= c.Papel.AlturaMm + 0.01f);
        Assert.All(c.Paginas, p => Assert.NotEmpty(p.Primitivas));
    }
}
