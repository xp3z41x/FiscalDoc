using FiscalDoc.App;
using FiscalDoc.Layout.DisplayList;

namespace FiscalDoc.Tests;

/// <summary>
/// O roteamento de layout, preso por comportamento.
///
/// E a regra que o CLAUDE.md declara com mais enfase - "o modelo 65 vai
/// sempre para o DanfeNfce; o 55 vai para retrato ou paisagem conforme
/// tpImp" - e a que ate agora nao tinha teste nenhum, porque a suite nao
/// enxergava o projeto do aplicativo.
///
/// <para>Os testes olham o <b>resultado</b>, nao o caminho: a largura do
/// papel diz qual layout respondeu. Cupom sai em bobina de 80 mm; DANFE sai
/// em A4, em pe ou deitada. Trocar o despacho para <c>tpImp</c> quebraria
/// estes testes, que e exatamente o que se quer.</para>
/// </summary>
public sealed class RoteamentoLayoutTests
{
    private static ConjuntoPaginas Abrir(string caminho)
    {
        var abertura = new AberturaDocumento();
        AberturaDocumento.Resultado r = abertura.Abrir(caminho);

        Assert.Null(r.Mensagem);
        Assert.NotNull(r.Paginas);
        return r.Paginas!;
    }

    /// <summary>
    /// Toda NFC-e real do corpus tem de sair em bobina, seja qual for o
    /// <c>tpImp</c> que ela declare.
    /// </summary>
    [FatoComCorpusReal]
    public void Toda_nfce_do_corpus_sai_em_bobina()
    {
        foreach (FileInfo arquivo in Amostras.ReaisNfce())
        {
            ConjuntoPaginas c = Abrir(arquivo.FullName);

            Assert.True(
                c.Papel.LarguraMm < 100f,
                $"{arquivo.Name} saiu com papel de {c.Papel.LarguraMm:0.#} mm - "
                + "um cupom deveria sair em bobina, nao em A4");
        }
    }

    /// <summary>
    /// O caso que a regra existe para cobrir: modelo 65 com <c>tpImp</c> que
    /// nao e nem 1 nem 2. Despachar por tpImp mandaria este arquivo para o
    /// DANFE A4 - um cupom renderizado como nota inteira.
    /// </summary>
    [TeoriaComCorpusReal]
    [InlineData("5")]
    [InlineData("0")]
    [InlineData("9")]
    public void Modelo_65_sai_em_bobina_mesmo_com_tpImp_estranho(string tpImp)
    {
        string fonte = File.ReadAllText(Amostras.RealNfce("56096"));

        string alterado = System.Text.RegularExpressions.Regex.Replace(
            fonte, "<tpImp>[^<]*</tpImp>", $"<tpImp>{tpImp}</tpImp>");

        Assert.Contains($"<tpImp>{tpImp}</tpImp>", alterado, StringComparison.Ordinal);

        string temporario = Path.Combine(Path.GetTempPath(), $"fiscaldoc-{Guid.NewGuid():N}.xml");
        File.WriteAllText(temporario, alterado);

        try
        {
            ConjuntoPaginas c = Abrir(temporario);

            Assert.True(
                c.Papel.LarguraMm < 100f,
                $"com tpImp={tpImp} o cupom saiu com papel de {c.Papel.LarguraMm:0.#} mm");
        }
        finally
        {
            File.Delete(temporario);
        }
    }

    /// <summary>
    /// Modelo 55 segue o <c>tpImp</c>: 1 em pe, 2 deitado. E a A4 nos dois
    /// casos, girada.
    /// </summary>
    [FatoComCorpusReal]
    public void Modelo_55_segue_o_tpImp_para_escolher_a_orientacao()
    {
        ConjuntoPaginas retrato = Abrir(Amostras.Real("RENASCENCA"));

        Assert.True(
            retrato.Papel.AlturaMm > retrato.Papel.LarguraMm,
            "tpImp 1 deveria sair em retrato");

        ConjuntoPaginas paisagem = Abrir(Amostras.PaisagemComMaisItens());

        Assert.True(
            paisagem.Papel.LarguraMm > paisagem.Papel.AlturaMm,
            "tpImp 2 deveria sair em paisagem");

        // A mesma folha, girada - nao uma folha diferente.
        Assert.Equal(retrato.Papel.LarguraMm, paisagem.Papel.AlturaMm, 3);
        Assert.Equal(retrato.Papel.AlturaMm, paisagem.Papel.LarguraMm, 3);
    }

    /// <summary>
    /// Um arquivo ilegivel vira mensagem na tela, nunca excecao que escapa -
    /// e o contrato que o <c>ParseResult</c> declara.
    /// </summary>
    [Theory]
    [InlineData("nao-existe-mesmo.xml")]
    [InlineData("")]
    public void Arquivo_impossivel_vira_mensagem_e_nao_excecao(string caminho)
    {
        var abertura = new AberturaDocumento();

        AberturaDocumento.Resultado r = abertura.Abrir(caminho);

        Assert.Null(r.Paginas);
        Assert.False(string.IsNullOrWhiteSpace(r.Mensagem));
    }
}
