using System.Xml.Linq;
using FiscalDoc.Core.Model;

namespace FiscalDoc.Core.Parsing;

/// <summary>
/// Identifica a familia do documento pela raiz do XML.
///
/// A verificacao e deliberadamente frouxa quanto ao namespace: casa pelo
/// local-name da raiz e usa a URI apenas como reforco. O MOC fixa a URI e
/// proibe prefixo, mas recusar um documento legitimo por causa de um emissor
/// desleixado seria punir o usuario pelo erro de um terceiro - e ele consegue
/// abrir esse mesmo arquivo em qualquer outro visualizador.
/// </summary>
internal static class DocumentSniffer
{
    internal const string NsNfe = "http://www.portalfiscal.inf.br/nfe";
    internal const string NsCte = "http://www.portalfiscal.inf.br/cte";
    internal const string NsMdfe = "http://www.portalfiscal.inf.br/mdfe";

    internal static FamiliaDocumento Identificar(string raizLocalName) => raizLocalName switch
    {
        "nfeProc" or "NFe" => FamiliaDocumento.Nfe,
        "procEventoNFe" or "retEnvEvento" or "envEvento" => FamiliaDocumento.EventoNfe,

        "cteProc" or "CTe" or "cteOSProc" or "CTeOS" => FamiliaDocumento.Cte,
        "procEventoCTe" or "eventoCTe" => FamiliaDocumento.EventoCte,

        "mdfeProc" or "MDFe" => FamiliaDocumento.Mdfe,
        "procEventoMDFe" or "eventoMDFe" => FamiliaDocumento.EventoMdfe,

        // "evento" nu, sem envelope: so e evento se o namespace disser.
        _ => FamiliaDocumento.Desconhecida,
    };

    /// <summary>
    /// Segunda chance para raizes ambiguas, usando o namespace. Cobre o caso do
    /// elemento &lt;evento&gt; avulso, que existe nas tres familias.
    /// </summary>
    internal static FamiliaDocumento IdentificarPorNamespace(string raizLocalName, string ns)
    {
        if (raizLocalName is not ("evento" or "procEvento"))
        {
            return FamiliaDocumento.Desconhecida;
        }

        return ns switch
        {
            NsNfe => FamiliaDocumento.EventoNfe,
            NsCte => FamiliaDocumento.EventoCte,
            NsMdfe => FamiliaDocumento.EventoMdfe,
            _ => FamiliaDocumento.Desconhecida,
        };
    }

    /// <summary>
    /// Localiza o elemento de informacoes do documento sob qualquer envelope.
    /// nfeProc/NFe/infNFe e NFe/infNFe levam ao mesmo lugar.
    /// </summary>
    internal static XElement? AcharInfo(XElement raiz, string nomeDocumento, string nomeInfo)
    {
        // Com envelope: raiz/NFe/infNFe
        XElement? info = raiz.Desce(nomeDocumento, nomeInfo);
        if (info is not null)
        {
            return info;
        }

        // Sem envelope: a propria raiz e NFe
        return raiz.Name.LocalName == nomeDocumento ? raiz.El(nomeInfo) : null;
    }
}
