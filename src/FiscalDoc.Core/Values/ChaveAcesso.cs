namespace FiscalDoc.Core.Values;

/// <summary>
/// Chave de acesso de 44 posicoes, comum a NF-e, CT-e e MDF-e.
///
/// Composicao (MOC 7.0 Visao Geral, Tabela 2-1):
/// cUF(2) AAMM(4) CNPJ(14) mod(2) serie(3) nNF(9) tpEmis(1) cNF(8) cDV(1).
///
/// <para>Com o CNPJ alfanumerico (IN RFB 2.229/2024, emitido desde julho de
/// 2026), as doze primeiras posicoes do CNPJ podem trazer letra maiuscula - e
/// so elas: a expressao da chave passou a <c>[0-9]{6}[A-Z0-9]{12}[0-9]{26}</c>
/// (NT Conjunta 2025.001, item 5, repetida pela NT 2026.004 da NF-e, pela NT
/// 2025.001 do CT-e, item 17, e pela NT 2025.001 do MDF-e, item 4). Por isso a
/// chave e texto de 44 caracteres, e nao numero de 44 digitos.</para>
/// </summary>
public sealed record ChaveAcesso
{
    private const int Tamanho = 44;

    /// <summary>Posicao do CNPJ do emitente na chave, contada de zero.</summary>
    private const int InicioCnpj = 6;

    /// <summary>Raiz e ordem do CNPJ: as posicoes que admitem letra.</summary>
    private const int PosicoesAlfanumericas = 12;

    private ChaveAcesso(string caracteres)
    {
        Caracteres = caracteres;
    }

    /// <summary>Os 44 caracteres, sem prefixo e sem separador.</summary>
    public string Caracteres { get; }

    public string CodigoUf => Caracteres[..2];
    public string AnoMes => Caracteres.Substring(2, 4);
    public string CnpjEmitente => Caracteres.Substring(InicioCnpj, 14);
    public string Modelo => Caracteres.Substring(20, 2);
    public string Serie => Caracteres.Substring(22, 3);
    public string Numero => Caracteres.Substring(25, 9);
    public string TipoEmissao => Caracteres.Substring(34, 1);
    public string CodigoNumerico => Caracteres.Substring(35, 8);
    public string DigitoVerificador => Caracteres.Substring(43, 1);

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
    ///
    /// <para>Campo com letra e CNPJ alfanumerico sem discussao: CPF so tem
    /// digitos.</para>
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
    public bool DigitoVerificadorConfere => CalcularDv(Caracteres[..43]) == Caracteres[43] - '0';

    /// <summary>
    /// Formatacao de impressao: onze grupos de quatro caracteres
    /// (MOC 7.0 Anexo II, 3.1.1).
    /// </summary>
    public string Formatada => string.Create(Tamanho + 10, Caracteres, static (destino, origem) =>
    {
        int j = 0;
        for (int i = 0; i < Tamanho; i++)
        {
            if (i > 0 && i % 4 == 0)
            {
                destino[j++] = ' ';
            }

            destino[j++] = origem[i];
        }
    });

    public override string ToString() => Caracteres;

    /// <summary>
    /// Extrai a chave do atributo Id de infNFe/infCte/infMDFe, que vem como
    /// "NFe" + 44 caracteres (ou "CTe"/"MDFe"), ou de uma tag de chave, que vem
    /// sem prefixo. Tolera separadores acidentais.
    ///
    /// <para>O prefixo se reconhece por ser feito so de letras: a chave comeca
    /// por cUF e AAMM, seis digitos, e nenhuma letra antes deles pode ser dela.
    /// Guardar so os digitos - como isto fazia ate o CNPJ alfanumerico - jogava
    /// fora as letras do CNPJ e recusava a chave inteira: o documento saia sem
    /// chave e sem codigo de barras.</para>
    ///
    /// <para>Letra so vale nas posicoes do CNPJ, e so maiuscula, que e o que o
    /// schema admite. Em qualquer outro lugar, nao e chave.</para>
    /// </summary>
    public static ChaveAcesso? DeAtributoId(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        Span<char> so = stackalloc char[Tamanho];
        int n = 0;
        bool noPrefixo = true;

        foreach (char c in id)
        {
            if (!char.IsAsciiLetterOrDigit(c))
            {
                continue; // separador
            }

            if (noPrefixo && char.IsAsciiLetter(c))
            {
                continue; // "NFe", "CTe", "MDFe"
            }

            noPrefixo = false;

            if (n == Tamanho)
            {
                return null; // mais de 44 caracteres: nao e chave
            }

            so[n++] = c;
        }

        return n == Tamanho && TemFormaDeChave(so) ? new ChaveAcesso(new string(so)) : null;
    }

    /// <summary><c>[0-9]{6}[A-Z0-9]{12}[0-9]{26}</c>.</summary>
    private static bool TemFormaDeChave(ReadOnlySpan<char> c)
    {
        for (int i = 0; i < c.Length; i++)
        {
            bool noCnpj = i is >= InicioCnpj and < InicioCnpj + PosicoesAlfanumericas;

            if (!char.IsAsciiDigit(c[i]) && !(noCnpj && char.IsAsciiLetterUpper(c[i])))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// CNPJ de 14 posicoes, <c>[A-Z0-9]{12}[0-9]{2}</c>, com os dois DV
    /// conferindo. Serve igual ao numerico e ao alfanumerico: a regra nova do
    /// DV foi escolhida para dar o mesmo resultado nos CNPJ que ja existem
    /// (NT Conjunta 2025.001, item 2).
    /// </summary>
    private static bool CnpjConfere(string d)
    {
        if (d.Length != 14 || !char.IsAsciiDigit(d[12]) || !char.IsAsciiDigit(d[13]))
        {
            return false;
        }

        for (int i = 0; i < PosicoesAlfanumericas; i++)
        {
            if (!char.IsAsciiDigit(d[i]) && !char.IsAsciiLetterUpper(d[i]))
            {
                return false;
            }
        }

        int[] pesos1 = [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
        int[] pesos2 = [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];

        return DvCadastro(d, pesos1) == d[12] - '0'
            && DvCadastro(d, pesos2) == d[13] - '0';
    }

    /// <summary>CPF de 11 digitos - nunca tem letra - com os dois DV conferindo.</summary>
    private static bool CpfConfere(string d)
    {
        if (d.Length != 11 || !d.All(char.IsAsciiDigit))
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
    /// caracteres: resto menor que 2 vira zero.
    /// </summary>
    private static int DvCadastro(string d, int[] pesos)
    {
        int soma = 0;
        for (int i = 0; i < pesos.Length; i++)
        {
            soma += Valor(d[i]) * pesos[i];
        }

        int resto = soma % 11;
        return resto < 2 ? 0 : 11 - resto;
    }

    /// <summary>
    /// Modulo 11 do MOC: pesos 2..9 ciclicos, da direita para a esquerda, sobre
    /// o <see cref="Valor"/> de cada caractere (NT Conjunta 2025.001, item 5).
    /// </summary>
    public static int CalcularDv(string primeiros43)
    {
        int soma = 0;
        int peso = 2;

        for (int i = primeiros43.Length - 1; i >= 0; i--)
        {
            soma += Valor(primeiros43[i]) * peso;
            peso = peso == 9 ? 2 : peso + 1;
        }

        int resto = soma % 11;
        return resto is 0 or 1 ? 0 : 11 - resto;
    }

    /// <summary>
    /// Quanto um caractere vale no modulo 11: o codigo ASCII menos 48. O
    /// digito vale ele mesmo, e a letra vale de 17 ("A") a 42 ("Z") - regra
    /// do CNPJ alfanumerico (NT Conjunta 2025.001, item 2; RFB, Perguntas e
    /// Respostas do CNPJ alfanumerico, pergunta 14), que a NT estende a chave
    /// inteira (item 5).
    /// </summary>
    private static int Valor(char c) => c - '0';
}
