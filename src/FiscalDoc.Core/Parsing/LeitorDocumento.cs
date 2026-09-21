using System.Xml;
using System.Xml.Linq;
using FiscalDoc.Core.Model;

namespace FiscalDoc.Core.Parsing;

/// <summary>
/// Porta de entrada da leitura: caminho de arquivo em
/// <see cref="ResultadoLeitura"/>. Nao lanca excecao por conteudo de arquivo.
/// </summary>
public static class LeitorDocumento
{
    public static ResultadoLeitura Ler(string caminho)
    {
        // Espiada barata: recusa um arquivo que nao e fiscal sem materializar
        // o documento. Devolve null quando o XML precisa do fallback Latin-1,
        // e nesse caso a decisao fica para depois do carregamento.
        var raiz = XmlSource.EspiarRaiz(caminho);
        if (raiz is { } r && Classificar(r.LocalName, r.NamespaceUri) == FamiliaDocumento.Desconhecida)
        {
            return new ResultadoLeitura.NaoFiscal(r.LocalName);
        }

        XDocument doc;
        try
        {
            doc = XmlSource.Carregar(caminho);
        }
        catch (ArquivoGrandeDemaisException ex)
        {
            return new ResultadoLeitura.ArquivoGrandeDemais(ex.Bytes);
        }
        catch (XmlException ex)
        {
            return new ResultadoLeitura.XmlInvalido(ex.Message, ex.LineNumber, ex.LinePosition);
        }
        catch (Exception ex) when (ex is IOException
                                      or UnauthorizedAccessException
                                      or NotSupportedException
                                      or ArgumentException
                                      or System.Security.SecurityException
                                      or OutOfMemoryException)
        {
            // OutOfMemoryException entra na lista porque o teto de entrada
            // reduz o risco mas nao o elimina: um documento dentro do teto
            // ainda pode nao caber numa maquina carregada. Perder o arquivo
            // com uma mensagem e melhor do que derrubar o aplicativo.
            //
            // ArgumentException cobre caminho vazio ou com caractere ilegal -
            // que chega quando o aplicativo e invocado com um argumento vazio
            // por um atalho ou script. Sem ela, a excecao escapava ate o
            // tratador global e o usuario via "erro inesperado" com pilha de
            // chamadas, em vez da mensagem desenhada na propria area do
            // documento. Este tipo promete NAO deixar excecao escapar por
            // arquivo; a lista precisa ser larga o bastante para cumprir.
            return new ResultadoLeitura.ArquivoIlegivel(ex.Message);
        }

        XElement? elementoRaiz = doc.Root;
        if (elementoRaiz is null)
        {
            return new ResultadoLeitura.XmlInvalido("Documento vazio.", 0, 0);
        }

        FamiliaDocumento familia = Classificar(
            elementoRaiz.Name.LocalName,
            elementoRaiz.Name.NamespaceName);

        return familia switch
        {
            FamiliaDocumento.Nfe => NfeParser.Ler(elementoRaiz),
            FamiliaDocumento.Cte => CteParser.Ler(elementoRaiz),
            FamiliaDocumento.Mdfe => MdfeParser.Ler(elementoRaiz),

            // Os tres eventos compartilham parser: a forma e a mesma, muda o
            // nome dos elementos externos e o namespace.
            FamiliaDocumento.EventoNfe
                or FamiliaDocumento.EventoCte
                or FamiliaDocumento.EventoMdfe => EventoParser.Ler(elementoRaiz, familia),

            _ => new ResultadoLeitura.NaoFiscal(elementoRaiz.Name.LocalName),
        };
    }

    private static FamiliaDocumento Classificar(string localName, string ns)
    {
        FamiliaDocumento f = DocumentSniffer.Identificar(localName);
        return f != FamiliaDocumento.Desconhecida
            ? f
            : DocumentSniffer.IdentificarPorNamespace(localName, ns);
    }

}
