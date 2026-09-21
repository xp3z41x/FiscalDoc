using System.Reflection;
using FiscalDoc.Core.Model;
using FiscalDoc.Core.Model.Nfe;
using FiscalDoc.Core.Parsing;

namespace FiscalDoc.Tests;

/// <summary>
/// Localiza o corpus de teste. As amostras reais ficam em "Exemplos XML" na
/// raiz; as sinteticas em tests/Amostras, geradas por
/// tools/gerar-amostras-sinteticas.py.
///
/// <para>O corpus real tem duas familias que o mesmo parser le mas que viram
/// documentos auxiliares diferentes: NF-e modelo 55 e NFC-e modelo 65. A
/// separacao e feita <b>lendo o modelo do arquivo</b>, e nao pelo nome:
/// arquivo baixado do portal nao tem convencao de nome garantida.</para>
/// </summary>
internal static class Amostras
{
    internal static DirectoryInfo Raiz { get; } = AcharRaiz();

    /// <summary>
    /// O corpus real <b>nao acompanha o repositorio</b>. Sao notas fiscais de
    /// verdade: CPF e nome completo de oito pessoas, CNPJ, endereco, telefone
    /// e dado comercial de vinte e duas empresas. Publicar isso seria expor
    /// dado pessoal de terceiro, que nao e nosso para publicar.
    ///
    /// <para>Quem clona recebe as amostras sinteticas de <c>tests/Amostras</c>,
    /// que cobrem contingência, homologacao, ISSQN, Latin-1, CT-e, MDF-e,
    /// eventos e as variacoes de NFC-e. Os testes que exigem documento real se
    /// declaram <b>ignorados</b>, e nao falhos - a diferenca importa: falha
    /// quer dizer defeito, e nao ha defeito nenhum em nao ter o corpus.</para>
    /// </summary>
    internal const string MotivoCorpusAusente =
        "o corpus real nao acompanha o repositorio (dado pessoal de terceiros). "
        + "Para rodar este teste, coloque os XML em \"Exemplos XML\" na raiz.";

    private static DirectoryInfo PastaReal =>
        new(Path.Combine(Raiz.FullName, "Exemplos XML"));

    /// <summary>
    /// Ha corpus real nesta copia de trabalho? Resolvido uma vez por processo:
    /// e consultado na construcao de cada [FatoComCorpusReal], e sao dezenas.
    /// </summary>
    internal static bool TemCorpusReal { get; } = ReaisSemGuarda().Length > 0;

    /// <summary>
    /// Declara o teste ignorado quando nao ha corpus real. Chamada de dentro
    /// de <see cref="Reais"/>, <see cref="ReaisNfe"/> e companhia, de modo que
    /// todo teste que peca documento real ja venha protegido - nao ha como
    /// esquecer a guarda num teste novo.
    ///
    /// <para><b>Nao</b> pode ser chamada de provedor de <c>[MemberData]</c>:
    /// aquilo roda na descoberta, fora do contexto de um teste. Os provedores
    /// usam as versoes ...SemGuarda.</para>
    /// </summary>
    /// <summary>
    /// Rede de seguranca para quem escrever um teste novo sobre o corpus real
    /// e esquecer de marca-lo com <c>[FatoComCorpusReal]</c>.
    ///
    /// <para>Com a marcacao correta o teste nem chega aqui - ele e ignorado na
    /// descoberta. Sem ela, este metodo FALHA com a explicacao, em vez de
    /// deixar o teste passar em verde sobre uma lista vazia, que e o desfecho
    /// perigoso: um <c>foreach</c> sobre zero arquivos nao assegura nada e
    /// ninguem percebe.</para>
    /// </summary>
    internal static void ExigirCorpusReal() =>
        Assert.True(
            TemCorpusReal,
            "este teste percorre o corpus real e nao esta marcado com "
            + "[FatoComCorpusReal] / [TeoriaComCorpusReal]. Sem a marcacao ele "
            + "passaria em verde sobre uma lista vazia. " + MotivoCorpusAusente);

    internal static string Real(string nomeParcial)
    {
        FileInfo[] achados = Reais()
            .Where(f => f.Name.Contains(nomeParcial, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.True(achados.Length > 0, $"nenhuma amostra real contendo \"{nomeParcial}\"");
        return achados[0].FullName;
    }

    internal static FileInfo[] Reais()
    {
        ExigirCorpusReal();
        return ReaisSemGuarda();
    }

    /// <summary>
    /// A lista crua, sem declarar o teste ignorado. Devolve vazio quando a
    /// pasta nao existe, em vez de lancar <see cref="DirectoryNotFoundException"/>
    /// - o que acontecia numa copia recem-clonada, e derrubava 68 testes com
    /// um erro que parecia defeito do aplicativo.
    /// </summary>
    internal static FileInfo[] ReaisSemGuarda()
    {
        DirectoryInfo pasta = PastaReal;

        return pasta.Exists
            ? pasta.GetFiles("*.xml").OrderBy(f => f.Name, StringComparer.Ordinal).ToArray()
            : [];
    }

    /// <summary>As NF-e reais, modelo 55.</summary>
    internal static FileInfo[] ReaisNfe()
    {
        ExigirCorpusReal();
        return ReaisNfeSemGuarda();
    }

    /// <summary>As NFC-e reais, modelo 65.</summary>
    internal static FileInfo[] ReaisNfce()
    {
        ExigirCorpusReal();
        return ReaisNfceSemGuarda();
    }

    /// <summary>
    /// Escolhe a NF-e real pelo que ela EXERCITA, e nao pelo nome do arquivo.
    ///
    /// <para>Existe por um motivo especifico: o nome do arquivo de uma NF-e e
    /// a chave de acesso, e chave de acesso e credencial de consulta no portal
    /// da SEFAZ. Este repositorio e publico; um <c>Real("352608...")</c>
    /// espalhado pelos testes publicaria a chave de um documento real mesmo
    /// com o corpus de fora.</para>
    ///
    /// <para>De quebra, o teste passa a dizer o que precisa - "a paisagem com
    /// mais itens" - em vez de um numero de 44 digitos que nao explica nada a
    /// quem le.</para>
    /// </summary>
    internal static string RealNfePor(
        Func<NfeDocumento, bool> filtro,
        Func<NfeDocumento, int> ordem,
        string descricao)
    {
        ExigirCorpusReal();

        (FileInfo Arquivo, NfeDocumento Doc)? melhor = null;

        foreach (FileInfo f in ReaisNfeSemGuarda())
        {
            if (LeitorDocumento.Ler(f.FullName) is not ResultadoLeitura.Ok
                { Documento: NfeDocumento nfe } || !filtro(nfe))
            {
                continue;
            }

            if (melhor is null || ordem(nfe) > ordem(melhor.Value.Doc))
            {
                melhor = (f, nfe);
            }
        }

        Assert.True(melhor is not null, $"nenhuma NF-e real serve como {descricao}");
        return melhor!.Value.Arquivo.FullName;
    }

    /// <summary>A NF-e paisagem com mais itens: 27, e a que pagina.</summary>
    internal static string PaisagemComMaisItens() =>
        RealNfePor(n => n.Paisagem, n => n.Itens.Count, "paisagem com mais itens");

    /// <summary>A NF-e paisagem com menos itens: cabe folgado numa folha.</summary>
    internal static string PaisagemComMenosItens() =>
        RealNfePor(n => n.Paisagem, n => -n.Itens.Count, "paisagem com menos itens");

    /// <summary>A NF-e retrato mais curta do corpus.</summary>
    internal static string RetratoComMenosItens() =>
        RealNfePor(n => !n.Paisagem, n => -n.Itens.Count, "retrato com menos itens");

    internal static FileInfo[] ReaisNfeSemGuarda() => PorModelo.Value[ModeloFiscal.Nfe];

    internal static FileInfo[] ReaisNfceSemGuarda() => PorModelo.Value[ModeloFiscal.Nfce];

    /// <summary>
    /// Primeira amostra real do modelo dado cujo nome contenha o trecho.
    /// </summary>
    internal static string RealNfce(string nomeParcial)
    {
        FileInfo[] achados = ReaisNfce()
            .Where(f => f.Name.Contains(nomeParcial, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.True(achados.Length > 0, $"nenhuma NFC-e real contendo \"{nomeParcial}\"");
        return achados[0].FullName;
    }

    /// <summary>
    /// Classificacao por modelo, feita uma vez por processo: sao 22 arquivos e
    /// cada [Theory] pediria a lista de novo.
    /// </summary>
    private static readonly Lazy<Dictionary<string, FileInfo[]>> PorModelo = new(() =>
    {
        var mapa = new Dictionary<string, List<FileInfo>>(StringComparer.Ordinal)
        {
            [ModeloFiscal.Nfe] = [],
            [ModeloFiscal.Nfce] = [],
        };

        foreach (FileInfo f in ReaisSemGuarda())
        {
            if (LeitorDocumento.Ler(f.FullName) is ResultadoLeitura.Ok { Documento: NfeDocumento n }
                && n.Ide.Modelo is { } modelo
                && mapa.TryGetValue(modelo, out List<FileInfo>? lista))
            {
                lista.Add(f);
            }
        }

        return mapa.ToDictionary(p => p.Key, p => p.Value.ToArray(), StringComparer.Ordinal);
    });

    internal static string Sintetica(string nomeArquivo)
    {
        string caminho = Path.Combine(Raiz.FullName, "tests", "Amostras", nomeArquivo);
        Assert.True(
            File.Exists(caminho),
            $"amostra sintetica ausente: {nomeArquivo}\n"
            + "Gere com: python tools/gerar-amostras-sinteticas.py");
        return caminho;
    }

    /// <summary>Sobe a partir do assembly ate achar o .sln.</summary>
    private static DirectoryInfo AcharRaiz()
    {
        var dir = new DirectoryInfo(
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!);

        // O .NET 10 gera .slnx; o padrao cobre os dois formatos.
        while (dir is not null && dir.GetFiles("FiscalDoc.sln*").Length == 0)
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return dir!;
    }
}
