namespace FiscalDoc.Core.Values;

/// <summary>
/// Chave de acesso de 44 digitos, comum a NF-e, CT-e e MDF-e.
///
/// Composicao (MOC 7.0 Visao Geral, Tabela 2-1):
/// cUF(2) AAMM(4) CNPJ(14) mod(2) serie(3) nNF(9) tpEmis(1) cNF(8) cDV(1).
/// </summary>
public sealed record ChaveAcesso
{
    private ChaveAcesso(string digitos)
    {
        Digitos = digitos;
    }

    /// <summary>Os 44 digitos, sem prefixo e sem separador.</summary>
    public string Digitos { get; }

    public string CodigoUf => Digitos[..2];
    public string AnoMes => Digitos.Substring(2, 4);
    public string CnpjEmitente => Digitos.Substring(6, 14);
    public string Modelo => Digitos.Substring(20, 2);
    public string Serie => Digitos.Substring(22, 3);
    public string Numero => Digitos.Substring(25, 9);
    public string TipoEmissao => Digitos.Substring(34, 1);
    public string CodigoNumerico => Digitos.Substring(35, 8);
    public string DigitoVerificador => Digitos.Substring(43, 1);

    /// <summary>
    /// O DV informado confere com o modulo 11 calculado sobre os 43 primeiros.
    /// O app nao recusa chave com DV errado - so imprime o que esta no arquivo,
    /// conforme o MOC. Mas calcular e barato e a informacao fica disponivel.
    /// </summary>
    public bool DigitoVerificadorConfere => CalcularDv(Digitos[..43]) == Digitos[43] - '0';

    /// <summary>
    /// Formatacao de impressao: onze grupos de quatro digitos
    /// (MOC 7.0 Anexo II, 3.1.1).
    /// </summary>
    public string Formatada => string.Create(44 + 10, Digitos, static (destino, origem) =>
    {
        int j = 0;
        for (int i = 0; i < 44; i++)
        {
            if (i > 0 && i % 4 == 0)
            {
                destino[j++] = ' ';
            }

            destino[j++] = origem[i];
        }
    });

    public override string ToString() => Digitos;

    /// <summary>
    /// Extrai a chave do atributo Id de infNFe/infCte/infMDFe, que vem como
    /// "NFe" + 44 digitos (ou "CTe"/"MDFe"). Tolera o Id sem prefixo e
    /// separadores acidentais.
    /// </summary>
    public static ChaveAcesso? DeAtributoId(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        Span<char> so = stackalloc char[44];
        int n = 0;
        foreach (char c in id)
        {
            if (c is >= '0' and <= '9')
            {
                if (n == 44)
                {
                    return null; // mais de 44 digitos: nao e chave
                }

                so[n++] = c;
            }
        }

        return n == 44 ? new ChaveAcesso(new string(so)) : null;
    }

    /// <summary>Modulo 11 do MOC: pesos 2..9 ciclicos, da direita para a esquerda.</summary>
    public static int CalcularDv(string primeiros43)
    {
        int soma = 0;
        int peso = 2;

        for (int i = primeiros43.Length - 1; i >= 0; i--)
        {
            soma += (primeiros43[i] - '0') * peso;
            peso = peso == 9 ? 2 : peso + 1;
        }

        int resto = soma % 11;
        return resto is 0 or 1 ? 0 : 11 - resto;
    }
}
