using System.Globalization;
using System.Xml.Linq;
using FiscalDoc.Core.Model.Nfe;

namespace FiscalDoc.Core.Parsing;

/// <summary>
/// Navegacao de XML por <b>local-name</b>, ignorando namespace.
///
/// O MOC proibe prefixo de namespace e fixa a URI, mas arquivos irregulares
/// existem no mundo real - emissores que declaram prefixo, que erram a URI, ou
/// que a omitem. Casar por local-name torna o parser imune a isso sem afrouxar
/// nada que importe: a identificacao do documento ja foi feita pelo
/// <see cref="DocumentSniffer"/>, que verifica a URI.
///
/// Todo getter e tolerante a ausencia: campo opcional que nao veio devolve
/// null, nunca excecao. Ver plano 2.6.
/// </summary>
internal static class XEl
{
    /// <summary>Primeiro filho direto com este local-name, ou null.</summary>
    internal static XElement? El(this XElement? pai, string localName)
    {
        if (pai is null)
        {
            return null;
        }

        foreach (XElement e in pai.Elements())
        {
            if (e.Name.LocalName == localName)
            {
                return e;
            }
        }

        return null;
    }

    /// <summary>Todos os filhos diretos com este local-name.</summary>
    internal static IEnumerable<XElement> Els(this XElement? pai, string localName)
    {
        if (pai is null)
        {
            yield break;
        }

        foreach (XElement e in pai.Elements())
        {
            if (e.Name.LocalName == localName)
            {
                yield return e;
            }
        }
    }

    /// <summary>Desce uma sequencia de local-names. Para no primeiro ausente.</summary>
    internal static XElement? Desce(this XElement? pai, params string[] localNames)
    {
        XElement? atual = pai;
        foreach (string nome in localNames)
        {
            atual = atual.El(nome);
            if (atual is null)
            {
                return null;
            }
        }

        return atual;
    }

    /// <summary>
    /// Unico filho direto, seja qual for o nome. Usado nos grupos de escolha do
    /// leiaute - ICMS carrega exatamente um de ICMS00, ICMS20, ICMSSN101, etc.
    /// </summary>
    internal static XElement? FilhoUnico(this XElement? pai)
    {
        if (pai is null)
        {
            return null;
        }

        XElement? primeiro = null;
        foreach (XElement e in pai.Elements())
        {
            if (primeiro is not null)
            {
                return primeiro; // mais de um: devolve o primeiro, nao falha
            }

            primeiro = e;
        }

        return primeiro;
    }

    /// <summary>Texto do filho, aparado. Null se ausente ou so espacos.</summary>
    internal static string? Str(this XElement? pai, string localName)
    {
        string? v = pai.El(localName)?.Value;
        if (v is null)
        {
            return null;
        }

        v = v.Trim();
        return v.Length == 0 ? null : v;
    }

    /// <summary>Texto do proprio elemento, aparado.</summary>
    internal static string? Texto(this XElement? e)
    {
        string? v = e?.Value.Trim();
        return string.IsNullOrEmpty(v) ? null : v;
    }

    /// <summary>
    /// Decimal do filho. O XML fiscal usa sempre ponto como separador decimal,
    /// independentemente da cultura da maquina - dai InvariantCulture fixo.
    /// </summary>
    internal static decimal? Dec(this XElement? pai, string localName)
    {
        string? v = pai.Str(localName);
        return v is not null
            && decimal.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal d)
            ? d
            : null;
    }

    /// <summary>
    /// Ambiente do documento, com o default seguro.
    ///
    /// <para>Ausente, ilegivel ou <b>fora do dominio</b> cai em homologacao.
    /// O erro seguro e carimbar "SEM VALOR FISCAL" num documento valido; o
    /// inverso - um documento de homologacao impresso identico a um valido -
    /// e o que nao pode acontecer. Um <c>tpAmb</c> igual a 3 nao e producao
    /// nem homologacao, e o cast direto o deixava passar como "nao e
    /// homologacao", que e o mesmo que producao.</para>
    /// </summary>
    internal static Ambiente LerAmbiente(int? tpAmb) => tpAmb switch
    {
        (int)Ambiente.Producao => Ambiente.Producao,
        _ => Ambiente.Homologacao,
    };

    internal static int? Int(this XElement? pai, string localName)
    {
        string? v = pai.Str(localName);
        return v is not null
            && int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i)
            ? i
            : null;
    }

    /// <summary>
    /// Data/hora com offset (campos dh*, formato XML Schema dateTime com
    /// fuso, ex.: 2026-03-12T10:00:00-03:00).
    /// </summary>
    internal static DateTimeOffset? DataHora(this XElement? pai, string localName)
    {
        string? v = pai.Str(localName);
        return v is not null
            && DateTimeOffset.TryParse(v, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out DateTimeOffset dto)
            ? dto
            : null;
    }

    /// <summary>Data pura (campos d*, ex.: dVenc, dPrevEntrega).</summary>
    internal static DateOnly? Data(this XElement? pai, string localName)
    {
        string? v = pai.Str(localName);
        return v is not null
            && DateOnly.TryParse(v, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly d)
            ? d
            : null;
    }

    /// <summary>Valor de atributo por local-name.</summary>
    internal static string? Attr(this XElement? e, string localName)
    {
        if (e is null)
        {
            return null;
        }

        foreach (XAttribute a in e.Attributes())
        {
            if (a.Name.LocalName == localName)
            {
                string v = a.Value.Trim();
                return v.Length == 0 ? null : v;
            }
        }

        return null;
    }
}
