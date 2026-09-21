namespace FiscalDoc.Core.Model;

/// <summary>Familia do documento, decidida pela raiz do XML e pelo namespace.</summary>
public enum FamiliaDocumento
{
    Desconhecida = 0,
    Nfe,
    Cte,
    Mdfe,
    EventoNfe,
    EventoCte,
    EventoMdfe,
}

/// <summary>
/// Modelos de documento fiscal. Apenas 55, 65, 57 e 58 estao no escopo; os
/// demais existem aqui para que a recusa consiga dizer <i>qual</i> modelo o
/// arquivo e, em vez de um "nao suportado" sem informacao.
/// </summary>
public static class ModeloFiscal
{
    public const string Nfe = "55";
    public const string Nfce = "65";
    public const string Cte = "57";
    public const string CteOs = "67";
    public const string GtvE = "64";
    public const string Mdfe = "58";

    public static bool NoEscopo(string? modelo) =>
        modelo is Nfe or Nfce or Cte or Mdfe;

    public static string Descrever(string? modelo) => modelo switch
    {
        Nfe => "NF-e (Nota Fiscal Eletrônica)",
        Nfce => "NFC-e (Nota Fiscal de Consumidor Eletrônica)",
        Cte => "CT-e (Conhecimento de Transporte Eletrônico)",
        CteOs => "CT-e OS (Outros Serviços)",
        GtvE => "GTV-e (Guia de Transporte de Valores)",
        Mdfe => "MDF-e (Manifesto Eletrônico de Documentos Fiscais)",
        null => "sem modelo identificado",
        _ => $"modelo {modelo}",
    };
}

/// <summary>Base comum a tudo que o app sabe exibir.</summary>
public abstract record DocumentoFiscal
{
    public abstract FamiliaDocumento Familia { get; }

    /// <summary>Titulo curto para a barra da janela.</summary>
    public abstract string TituloCurto { get; }
}
