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
    /// CNPJ ou CPF do emitente, com o tamanho certo para ser formatado.
    ///
    /// <para>O campo da chave tem 14 posicoes e, quando o emitente e pessoa
    /// fisica - produtor rural na NF-e, transportador autonomo no CT-e -, traz
    /// o CPF completado com zeros a esquerda. Os digitos verificadores
    /// desempatam: vale CNPJ se o CNPJ confere, e CPF se so o CPF confere.
    /// Quando os dois conferem ao mesmo tempo, o que acontece em cerca de 1 %
    /// das chaves que comecam por 000, fica o CNPJ - pessoa juridica e de
    /// longe o emitente mais comum, e o Banco do Brasil, 00.000.000/0001-91,
    /// e um exemplo real do empate.</para>
    /// </summary>
    public string DocumentoEmitente
    {
        get
        {
            string campo = CnpjEmitente;

            if (!CnpjConfere(campo) && campo.StartsWith("000", StringComparison.Ordinal)
                && CpfConfere(campo[3..]))
            {
                return campo[3..];
            }

            return campo;
        }
    }

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

    private static bool CnpjConfere(string d)
    {
        if (d.Length != 14)
        {
            return false;
        }

        int[] pesos1 = [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
        int[] pesos2 = [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];

        return DvCadastro(d, pesos1) == d[12] - '0'
            && DvCadastro(d, pesos2) == d[13] - '0';
    }

    private static bool CpfConfere(string d)
    {
        if (d.Length != 11)
        {
            return false;
        }

        int[] pesos1 = [10, 9, 8, 7, 6, 5, 4, 3, 2];
        int[] pesos2 = [11, 10, 9, 8, 7, 6, 5, 4, 3, 2];

        return DvCadastro(d, pesos1) == d[9] - '0'
            && DvCadastro(d, pesos2) == d[10] - '0';
    }

    /// <summary>
    /// Modulo 11 do CNPJ e do CPF sobre os primeiros <c>pesos.Length</c>
    /// digitos: resto menor que 2 vira zero.
    /// </summary>
    private static int DvCadastro(string d, int[] pesos)
    {
        int soma = 0;
        for (int i = 0; i < pesos.Length; i++)
        {
            soma += (d[i] - '0') * pesos[i];
        }

        int resto = soma % 11;
        return resto < 2 ? 0 : 11 - resto;
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
