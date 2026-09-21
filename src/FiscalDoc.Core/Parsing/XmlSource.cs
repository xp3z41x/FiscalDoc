using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace FiscalDoc.Core.Parsing;

/// <summary>
/// Abertura de XML fiscal a partir do disco, com duas preocupacoes que nao
/// podem ser delegadas ao chamador: encoding e seguranca.
///
/// <para><b>Encoding.</b> O arquivo e aberto como <see cref="Stream"/> e o
/// <see cref="XmlReader"/> honra a declaracao <c>encoding=</c> do proprio
/// documento, cobrindo UTF-8 e ISO-8859-1. Jamais <c>File.ReadAllText</c>, que
/// assume UTF-8 e corrompe Latin-1 em silencio. Arquivo sem declaracao vale
/// UTF-8 por padrao do XML - uma das amostras reais e assim.</para>
///
/// <para><b>Latin-1 mal declarado.</b> Emissores que declaram UTF-8 mas gravam
/// bytes Latin-1 existem. Nesse caso a primeira leitura falha, e ha uma
/// segunda tentativa forcando ISO-8859-1, que nao rejeita sequencia de byte
/// nenhuma. E melhor abrir a nota com um acento possivelmente errado do que
/// recusar um documento que o usuario consegue ler em qualquer outro lugar.</para>
///
/// <para><b>Seguranca.</b> DTD proibido e resolver nulo. Um XML fiscal chega de
/// terceiros - fornecedor, transportadora, e-mail - e sem isso uma entidade
/// externa poderia ler arquivos da maquina ou travar o processo. O FiscalDoc
/// nao acessa rede em nenhuma hipotese, e isso vale para o parser de XML.</para>
/// </summary>
/// <summary>
/// Arquivo alem do teto que um documento fiscal justifica.
/// </summary>
internal sealed class ArquivoGrandeDemaisException(long bytes)
    : Exception($"Arquivo de {bytes} bytes, acima do teto para documento fiscal.")
{
    internal long Bytes { get; } = bytes;
}

internal static class XmlSource
{
    /// <summary>
    /// Teto de caracteres do documento.
    ///
    /// <para>Um XML fiscal grande de verdade - NF-e de 990 itens com
    /// infAdProd em todos - fica na casa de 3 MB. Trinta milhoes de
    /// caracteres sao dez vezes o maior documento que o leiaute admite, com
    /// folga para qualquer emissor prolixo.</para>
    ///
    /// <para>Sem teto, um arquivo de 500 MB - que chega por e-mail como
    /// qualquer outro - era materializado inteiro por <c>XDocument.Load</c>,
    /// em cinco a dez vezes o tamanho do disco, na pilha de objetos grandes.
    /// O resultado nao era uma recusa: era a janela travada sem resposta e
    /// depois uma caixa dizendo que o aplicativo precisa fechar.</para>
    /// </summary>
    private const long MaximoCaracteres = 30_000_000;

    /// <summary>
    /// Teto de bytes do arquivo, checado antes de abrir. Existe alem do teto
    /// de caracteres porque recusar pelo tamanho do arquivo nao custa nem uma
    /// leitura.
    /// </summary>
    private const long MaximoBytes = 128L * 1024 * 1024;

    private static readonly XmlReaderSettings Seguro = new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
        IgnoreComments = true,
        IgnoreProcessingInstructions = true,
        IgnoreWhitespace = true,
        CloseInput = true,
        MaxCharactersInDocument = MaximoCaracteres,
    };

    static XmlSource()
    {
        // Necessario para ISO-8859-1 / windows-1252 em .NET moderno.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    /// <summary>
    /// Carrega o documento. Lanca <see cref="XmlException"/> se nem a leitura
    /// normal nem o fallback Latin-1 conseguirem, e <see cref="IOException"/> /
    /// <see cref="UnauthorizedAccessException"/> se o arquivo nao puder ser
    /// lido. O chamador converte isso em <see cref="ParseResult"/>.
    /// </summary>
    internal static XDocument Carregar(string caminho)
    {
        RecusarSeGigante(caminho);

        try
        {
            using FileStream fs = File.OpenRead(caminho);
            using XmlReader r = XmlReader.Create(fs, Seguro);
            return XDocument.Load(r, LoadOptions.None);
        }
        catch (XmlException)
        {
            return CarregarComoLatin1(caminho);
        }
    }

    /// <summary>Recusa pelo tamanho antes de abrir o arquivo.</summary>
    private static void RecusarSeGigante(string caminho)
    {
        var info = new FileInfo(caminho);

        if (info.Exists && info.Length > MaximoBytes)
        {
            throw new ArquivoGrandeDemaisException(info.Length);
        }
    }

    private static XDocument CarregarComoLatin1(string caminho)
    {
        using FileStream fs = File.OpenRead(caminho);
        using var sr = new StreamReader(fs, Encoding.Latin1, detectEncodingFromByteOrderMarks: false);
        using XmlReader r = XmlReader.Create(sr, Seguro);
        return XDocument.Load(r, LoadOptions.None);
    }

    /// <summary>
    /// Le so o suficiente para identificar a raiz, sem materializar o
    /// documento. Usado para recusar um arquivo que nao e fiscal antes de
    /// gastar memoria com ele. Ver plano 2.6.
    /// </summary>
    internal static (string LocalName, string NamespaceUri)? EspiarRaiz(string caminho)
    {
        try
        {
            using FileStream fs = File.OpenRead(caminho);
            using XmlReader r = XmlReader.Create(fs, Seguro);

            while (r.Read())
            {
                if (r.NodeType == XmlNodeType.Element)
                {
                    return (r.LocalName, r.NamespaceURI);
                }
            }
        }
        catch (Exception ex) when (ex is XmlException or IOException
            or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            // A espiada e so uma otimizacao: qualquer falha aqui devolve null e
            // deixa o Carregar produzir o erro de verdade, com a mensagem certa.
            // Nao pode lancar - o contrato do leitor e nunca lancar por arquivo.
            return null;
        }

        return null;
    }
}
