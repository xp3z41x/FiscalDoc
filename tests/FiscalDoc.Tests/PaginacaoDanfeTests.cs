using FiscalDoc.Core.Model.Nfe;
using FiscalDoc.Core.Parsing;
using FiscalDoc.Layout.Danfe;
using FiscalDoc.Layout.DisplayList;
using FiscalDoc.Render.Wpf;

namespace FiscalDoc.Tests;

/// <summary>
/// Nenhum item se perde na paginacao.
///
/// A suite media o DANFE de muitas formas e nunca esta: nada afirmava que
/// cada item do arquivo aparece no papel. E o quadro de produtos tem um
/// <c>break</c> no meio - quando um item nao cabe, ele e o resto da pagina
/// somem sem marca nenhuma, e a moldura e desenhada como se estivessem la.
/// Qualquer divergencia de um decimo de milimetro entre quem pagina e quem
/// desenha vira perda silenciosa de dado fiscal.
///
/// <para>O teste percorre o corpus real inteiro, nas duas orientacoes.</para>
/// </summary>
public sealed class PaginacaoDanfeTests
{
    private static IEnumerable<string> TextosDe(IReadOnlyList<Primitiva> lista)
    {
        foreach (Primitiva p in lista)
        {
            switch (p)
            {
                case Primitiva.Texto t when !string.IsNullOrEmpty(t.Conteudo):
                    yield return t.Conteudo;
                    break;

                case Primitiva.Rotacionado r:
                    foreach (string f in TextosDe(r.Filhos))
                    {
                        yield return f;
                    }

                    break;
            }
        }
    }

    private static List<string> TodosOsTextos(ConjuntoPaginas c)
    {
        var saida = new List<string>();

        foreach (Pagina p in c.Paginas)
        {
            saida.AddRange(TextosDe(p.Primitivas));
        }

        return saida;
    }

    public static TheoryData<string> Reais()
    {
        var dados = new TheoryData<string>();

        foreach (FileInfo f in Amostras.ReaisNfeSemGuarda())
        {
            dados.Add(f.FullName);
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

    [TeoriaComCorpusReal]
    [MemberData(nameof(Reais))]
    public void Todo_item_do_arquivo_aparece_no_documento(string caminho)
    {
        Amostras.ExigirCorpusReal();

        var ok = Assert.IsType<ResultadoLeitura.Ok>(LeitorDocumento.Ler(caminho));
        var nfe = Assert.IsType<NfeDocumento>(ok.Documento);

        var medidor = new MedidorTextoWpf();

        ConjuntoPaginas c = nfe.Paisagem
            ? DanfePaisagem.Construir(nfe, medidor)
            : DanfeRetrato.Construir(nfe, medidor);

        // O codigo pode sair quebrado em duas linhas (coluna de 14,8 mm), e
        // ai o texto da primitiva traz a quebra explicita no meio. Junta-se
        // tudo para procurar pelo codigo inteiro.
        string tudo = string.Concat(TodosOsTextos(c))
            .Replace("\n", string.Empty, StringComparison.Ordinal);

        var faltando = new List<string>();

        foreach (ItemNfe item in nfe.Itens)
        {
            // O codigo do produto e o identificador mais curto e mais estavel
            // do item; se ele esta na pagina, a linha foi desenhada.
            if (item.Codigo is not { Length: > 0 } codigo)
            {
                continue;
            }

            if (!tudo.Contains(codigo, StringComparison.Ordinal))
            {
                faltando.Add($"item {item.Numero} ({codigo})");
            }
        }

        Assert.True(
            faltando.Count == 0,
            $"{faltando.Count} de {nfe.Itens.Count} itens nao chegaram ao papel: "
            + string.Join(", ", faltando.Take(5)));
    }

    /// <summary>
    /// "FOLHA nn/nn" existe em toda pagina, e o total bate com o numero de
    /// paginas de verdade. E a razao pela qual o aplicativo pagina antes de
    /// desenhar qualquer primitiva.
    /// </summary>
    [TeoriaComCorpusReal]
    [MemberData(nameof(Reais))]
    public void Toda_pagina_tem_folha_n_de_n_com_o_total_certo(string caminho)
    {
        Amostras.ExigirCorpusReal();

        var ok = Assert.IsType<ResultadoLeitura.Ok>(LeitorDocumento.Ler(caminho));
        var nfe = Assert.IsType<NfeDocumento>(ok.Documento);

        var medidor = new MedidorTextoWpf();

        ConjuntoPaginas c = nfe.Paisagem
            ? DanfePaisagem.Construir(nfe, medidor)
            : DanfeRetrato.Construir(nfe, medidor);

        for (int i = 0; i < c.Total; i++)
        {
            string esperado = $"FOLHA {i + 1:00}/{c.Total:00}";

            Assert.Contains(
                TextosDe(c.Paginas[i].Primitivas),
                t => t.Contains(esperado, StringComparison.Ordinal));
        }
    }

    /// <summary>
    /// Nenhuma folha sai praticamente vazia. O rodape costumava ganhar uma
    /// folha so para ele, sem nem o quadro de produtos - cabecalho, rodape e
    /// cerca de 200 mm de branco no meio.
    /// </summary>
    [TeoriaComCorpusReal]
    [MemberData(nameof(Reais))]
    public void Nenhuma_folha_sai_praticamente_vazia(string caminho)
    {
        Amostras.ExigirCorpusReal();

        var ok = Assert.IsType<ResultadoLeitura.Ok>(LeitorDocumento.Ler(caminho));
        var nfe = Assert.IsType<NfeDocumento>(ok.Documento);

        var medidor = new MedidorTextoWpf();

        ConjuntoPaginas c = nfe.Paisagem
            ? DanfePaisagem.Construir(nfe, medidor)
            : DanfeRetrato.Construir(nfe, medidor);

        if (c.Total == 1)
        {
            return;
        }

        for (int i = 0; i < c.Total; i++)
        {
            int textos = TextosDe(c.Paginas[i].Primitivas).Count();

            Assert.True(
                textos > 40,
                $"a folha {i + 1} de {c.Total} tem so {textos} textos - "
                + "uma folha de continuacao precisa carregar documento, nao so cabecalho e rodape");
        }
    }
}
