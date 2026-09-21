using System.Globalization;
using FiscalDoc.Core.Model.Nfe;
using FiscalDoc.Core.Parsing;
using FiscalDoc.Core.Values;

namespace FiscalDoc.Tests;

/// <summary>
/// Os valores impressos, presos por literal.
///
/// Todo o resto da suite compara a saida com <c>Formatos.Moeda(x)</c> - a
/// propria funcao sob teste - e portanto nao prende nada: trocar a cultura
/// fixa por <c>CurrentCulture</c>, ou "N2" por "C2", faria o DANFE inteiro
/// sair com ponto decimal e os testes continuariam verdes. Aqui os valores
/// sao literais, escritos como tem de aparecer no papel.
/// </summary>
public sealed class FormatacaoFiscalTests
{
    /// <summary>
    /// A cultura e do documento, nao da maquina. Um DANFE impresso numa
    /// estacao configurada em ingles nao pode sair com ponto no lugar da
    /// virgula.
    /// </summary>
    [Theory]
    [InlineData("en-US")]
    [InlineData("de-DE")]
    [InlineData("pt-BR")]
    [InlineData("")]
    public void Dinheiro_sai_em_pt_br_seja_qual_for_a_cultura_da_maquina(string cultura)
    {
        CultureInfo anterior = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultura);

            Assert.Equal("9.784,77", Formatos.Moeda(9784.77m));
            Assert.Equal("0,00", Formatos.Moeda(0m));
            Assert.Equal("1.234.567,89", Formatos.Moeda(1234567.89m));
            Assert.Equal("-50,00", Formatos.Moeda(-50m));
            Assert.Equal(string.Empty, Formatos.Moeda(null));
        }
        finally
        {
            CultureInfo.CurrentCulture = anterior;
        }
    }

    /// <summary>
    /// O preco unitario nao pode ser arredondado.
    ///
    /// <c>vUnCom</c> vem com ate dez casas (TDec_1110v), e no corpus real 190
    /// de 345 precos perdiam valor quando impressos com duas. Alem de imprimir
    /// um numero ausente do arquivo - o que o MOC 3.1 proibe -, fazia
    /// "quantidade x valor unitario" deixar de fechar com o valor total na
    /// cara do documento.
    /// </summary>
    [Theory]
    [InlineData("25.1293333333", "25,1293333333")]
    [InlineData("0.0689650000", "0,068965")]
    [InlineData("16.6330000000", "16,633")]
    [InlineData("82.0884", "82,0884")]
    [InlineData("7.50", "7,50")]
    [InlineData("1234.5", "1.234,50")]
    [InlineData("9", "9,00")]
    [InlineData("0", "0,00")]
    public void Valor_unitario_preserva_as_casas_que_o_arquivo_traz(string doXml, string esperado)
    {
        decimal v = decimal.Parse(doXml, CultureInfo.InvariantCulture);
        Assert.Equal(esperado, Formatos.ValorUnitario(v));
    }

    [Fact]
    public void Valor_unitario_nulo_sai_vazio_e_nao_zero()
    {
        // Campo ausente e campo vazio. Escrever "0,00" seria afirmar um preco
        // que o arquivo nao traz.
        Assert.Equal(string.Empty, Formatos.ValorUnitario(null));
    }

    /// <summary>
    /// O caso que motivou tudo: o preco unitario impresso tem de reproduzir o
    /// que esta no XML, e nao um arredondamento dele.
    /// </summary>
    [FatoComCorpusReal]
    public void Nenhum_preco_unitario_do_corpus_real_e_arredondado_na_impressao()
    {
        var perdidos = new List<string>();

        foreach (FileInfo arquivo in Amostras.ReaisNfe())
        {
            var ok = Assert.IsType<ResultadoLeitura.Ok>(LeitorDocumento.Ler(arquivo.FullName));
            var nfe = Assert.IsType<NfeDocumento>(ok.Documento);

            foreach (ItemNfe item in nfe.Itens)
            {
                if (item.ValorUnitario is not { } v)
                {
                    continue;
                }

                string impresso = Formatos.ValorUnitario(v);
                decimal lido = decimal.Parse(
                    impresso.Replace(".", string.Empty, StringComparison.Ordinal)
                            .Replace(',', '.'),
                    CultureInfo.InvariantCulture);

                if (lido != v)
                {
                    perdidos.Add($"{v} -> {impresso}");
                }
            }
        }

        Assert.True(
            perdidos.Count == 0,
            $"{perdidos.Count} precos unitarios sairiam alterados: "
            + string.Join(", ", perdidos.Take(5)));
    }

    /// <summary>
    /// Imprimir todas as casas so vale se elas couberem: um preco cortado
    /// seria pior que um arredondado, porque parece completo.
    ///
    /// A coluna VALOR UNIT tem 16,0 mm, 14,8 uteis depois do recuo. Medido no
    /// corpus real, o pior caso ("25,1293333333") ocupa 13,2 mm.
    /// </summary>
    [FatoComCorpusReal]
    public void Nenhum_preco_unitario_do_corpus_real_estoura_a_coluna()
    {
        const float UtilMm = 16.0f - (2 * 0.6f);

        var medidor = new FiscalDoc.Render.Wpf.MedidorTextoWpf();
        var estilo = new FiscalDoc.Layout.DisplayList.EstiloTexto(
            FiscalDoc.Layout.DisplayList.EstiloTexto.FamiliaPadrao, 6f);

        var estouram = new List<string>();

        foreach (FileInfo arquivo in Amostras.ReaisNfe())
        {
            var ok = Assert.IsType<ResultadoLeitura.Ok>(LeitorDocumento.Ler(arquivo.FullName));
            var nfe = Assert.IsType<NfeDocumento>(ok.Documento);

            foreach (ItemNfe item in nfe.Itens)
            {
                if (item.ValorUnitario is not { } v)
                {
                    continue;
                }

                string impresso = Formatos.ValorUnitario(v);
                float largura = medidor.LarguraMm(impresso, estilo);

                if (largura > UtilMm)
                {
                    estouram.Add($"{impresso} = {largura:F2} mm");
                }
            }
        }

        Assert.True(
            estouram.Count == 0,
            $"{estouram.Count} precos nao caberiam em {UtilMm:F1} mm: "
            + string.Join(", ", estouram.Take(5)));
    }

    /// <summary>
    /// Ambiente ausente ou ilegivel tem de cair em homologacao.
    ///
    /// O erro seguro e carimbar "SEM VALOR FISCAL" num documento valido. O
    /// inverso - um documento de homologacao impresso identico a um valido -
    /// e o que nao pode acontecer.
    /// </summary>
    [Fact]
    public void Ambiente_ausente_cai_em_homologacao_e_exige_sem_valor_fiscal()
    {
        string xml = Amostras.Sintetica("homologacao.xml");
        string texto = File.ReadAllText(xml);

        // Remove o tpAmb, deixando o resto do arquivo intacto.
        string semAmbiente = System.Text.RegularExpressions.Regex.Replace(
            texto, "<tpAmb>[^<]*</tpAmb>", string.Empty);

        string temporario = Path.Combine(Path.GetTempPath(), $"fiscaldoc-{Guid.NewGuid():N}.xml");
        File.WriteAllText(temporario, semAmbiente);

        try
        {
            var ok = Assert.IsType<ResultadoLeitura.Ok>(LeitorDocumento.Ler(temporario));
            var nfe = Assert.IsType<NfeDocumento>(ok.Documento);

            Assert.Equal(Ambiente.Homologacao, nfe.Ide.Ambiente);
            Assert.True(nfe.ExigeSemValorFiscal);
        }
        finally
        {
            File.Delete(temporario);
        }
    }

    /// <summary>
    /// Quantidade e peso dos volumes nao podem ser somados pelo aplicativo.
    ///
    /// A faixa do MOC comporta um volume; com varios grupos <c>vol</c> o
    /// layout imprime o primeiro inteiro. Somar produzia um numero ausente do
    /// arquivo ao lado da especie e da marca de um volume so.
    /// </summary>
    [FatoComCorpusReal]
    public void Volumes_nao_sao_somados_pelo_aplicativo()
    {
        string fonte = File.ReadAllText(Amostras.Real("RENASCENCA"));

        // Dois volumes bem diferentes: uma soma apareceria como 7 e 430.
        const string Dois =
            "<vol><qVol>5</qVol><esp>CAIXA</esp><pesoB>30.000</pesoB><pesoL>28.000</pesoL></vol>"
            + "<vol><qVol>2</qVol><esp>PALLET</esp><pesoB>400.000</pesoB><pesoL>390.000</pesoL></vol>";

        string comDois = System.Text.RegularExpressions.Regex.Replace(
            fonte, "<vol>.*?</vol>", Dois, System.Text.RegularExpressions.RegexOptions.Singleline);

        Assert.Contains("PALLET", comDois, StringComparison.Ordinal);

        string temporario = Path.Combine(Path.GetTempPath(), $"fiscaldoc-{Guid.NewGuid():N}.xml");
        File.WriteAllText(temporario, comDois);

        try
        {
            var ok = Assert.IsType<ResultadoLeitura.Ok>(LeitorDocumento.Ler(temporario));
            var nfe = Assert.IsType<NfeDocumento>(ok.Documento);

            Assert.Equal(2, nfe.Volumes.Count);

            var medidor = new FiscalDoc.Render.Wpf.MedidorTextoWpf();
            var c = FiscalDoc.Layout.Danfe.DanfeRetrato.Construir(nfe, medidor);

            List<string> textos = [.. TextosDe(c)];

            // 5 e 30 estao no arquivo; 7 e 430 seriam soma do aplicativo.
            Assert.Contains("5", textos);
            Assert.DoesNotContain("7", textos);
            Assert.DoesNotContain("430", textos);
        }
        finally
        {
            File.Delete(temporario);
        }
    }

    private static IEnumerable<string> TextosDe(FiscalDoc.Layout.DisplayList.ConjuntoPaginas c)
    {
        foreach (FiscalDoc.Layout.DisplayList.Pagina p in c.Paginas)
        {
            foreach (string t in TextosDe(p.Primitivas))
            {
                yield return t;
            }
        }
    }

    private static IEnumerable<string> TextosDe(
        IReadOnlyList<FiscalDoc.Layout.DisplayList.Primitiva> lista)
    {
        foreach (FiscalDoc.Layout.DisplayList.Primitiva p in lista)
        {
            switch (p)
            {
                case FiscalDoc.Layout.DisplayList.Primitiva.Texto t:
                    yield return t.Conteudo;
                    break;

                case FiscalDoc.Layout.DisplayList.Primitiva.Rotacionado r:
                    foreach (string f in TextosDe(r.Filhos))
                    {
                        yield return f;
                    }

                    break;
            }
        }
    }
}
