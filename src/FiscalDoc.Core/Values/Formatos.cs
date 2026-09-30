using System.Globalization;

namespace FiscalDoc.Core.Values;

/// <summary>
/// Formatacao pt-BR dos valores fiscais.
///
/// A cultura e fixada em "pt-BR" explicitamente, e nao herdada da maquina: um
/// DANFE impresso numa estacao configurada em ingles nao pode sair com ponto
/// no lugar da virgula decimal. O documento e brasileiro, o formato tambem.
/// </summary>
public static class Formatos
{
    private static readonly CultureInfo Br = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>Valor monetario com duas casas: 1.234,56. Nulo vira vazio.</summary>
    public static string Moeda(decimal? v) =>
        v is null ? string.Empty : v.Value.ToString("N2", Br);

    /// <summary>
    /// Valor unitario do item.
    ///
    /// Diferente dos demais monetarios, imprime ate <b>dez</b> casas - que e o
    /// que o schema reserva para <c>vUnCom</c> e <c>vUnTrib</c> (TDec_1110v) -
    /// e corta so os zeros a direita que nao significam nada.
    ///
    /// <para>Arredondar para duas casas imprimia um numero que nao esta no
    /// arquivo, o que o MOC 3.1 proibe, e ainda fazia
    /// "quantidade x valor unitario" deixar de fechar com o valor total na
    /// cara do documento. No corpus real, 190 de 345 precos unitarios perdiam
    /// valor desse jeito.</para>
    /// </summary>
    public static string ValorUnitario(decimal? v)
    {
        if (v is null)
        {
            return string.Empty;
        }

        string s = v.Value.ToString("N10", Br);

        if (!s.Contains(',', StringComparison.Ordinal))
        {
            return s;
        }

        s = s.TrimEnd('0');

        // Duas casas e o minimo: "5,1" num campo de dinheiro parece truncado.
        int virgula = s.IndexOf(',', StringComparison.Ordinal);
        int casas = s.Length - virgula - 1;

        return casas switch
        {
            0 => s + "00",
            1 => s + "0",
            _ => s,
        };
    }

    /// <summary>
    /// Quantidade com ate quatro casas, sem zeros a direita inuteis:
    /// 10 sai "10", 10,5 sai "10,5", 10,1234 sai "10,1234".
    /// </summary>
    public static string Quantidade(decimal? v)
    {
        if (v is null)
        {
            return string.Empty;
        }

        string s = v.Value.ToString("N4", Br);

        if (!s.Contains(',', StringComparison.Ordinal))
        {
            return s;
        }

        s = s.TrimEnd('0');
        return s.EndsWith(',') ? s[..^1] : s;
    }

    /// <summary>Aliquota ou percentual com duas casas.</summary>
    public static string Percentual(decimal? v) =>
        v is null ? string.Empty : v.Value.ToString("N2", Br);

    /// <summary>
    /// 00.000.000/0000-00. O CNPJ alfanumerico (IN RFB 2.229/2024) usa a mesma
    /// mascara, com as letras onde antes so havia digito: 12.ABC.345/01DE-35
    /// (RFB, Perguntas e Respostas do CNPJ alfanumerico, pergunta 21).
    /// </summary>
    public static string Cnpj(string? v)
    {
        string d = SoLetrasEDigitos(v);
        return !TemFormaDeCnpj(d)
            ? v ?? string.Empty
            : $"{d[..2]}.{d.Substring(2, 3)}.{d.Substring(5, 3)}/{d.Substring(8, 4)}-{d.Substring(12, 2)}";
    }

    /// <summary>000.000.000-00. CPF so tem digitos: com letra, sai como veio.</summary>
    public static string Cpf(string? v)
    {
        string d = SoLetrasEDigitos(v);
        return !TemFormaDeCpf(d)
            ? v ?? string.Empty
            : $"{d[..3]}.{d.Substring(3, 3)}.{d.Substring(6, 3)}-{d.Substring(9, 2)}";
    }

    /// <summary>
    /// Escolhe entre CNPJ e CPF pela forma. E o que o campo "CNPJ / CPF" do
    /// DANFE precisa, ja que a mesma caixa recebe os dois.
    ///
    /// <para>Pela forma, e nao pelo numero de digitos: um CNPJ alfanumerico com
    /// tres letras tem onze digitos, e contar so os digitos imprimia
    /// 12ABC345000188 como o CPF 123.450.001-88 - que nao esta no arquivo
    /// (MOC 3.1).</para>
    /// </summary>
    public static string CnpjOuCpf(string? v)
    {
        string d = SoLetrasEDigitos(v);

        if (TemFormaDeCnpj(d))
        {
            return Cnpj(d);
        }

        return TemFormaDeCpf(d) ? Cpf(d) : v ?? string.Empty;
    }

    /// <summary>
    /// <c>[A-Z0-9]{12}[0-9]{2}</c>: letra maiuscula so na raiz e na ordem, e o
    /// DV sempre numerico (NT Conjunta 2025.001, item 4).
    /// </summary>
    private static bool TemFormaDeCnpj(string d)
    {
        if (d.Length != 14)
        {
            return false;
        }

        for (int i = 0; i < d.Length; i++)
        {
            if (!char.IsAsciiDigit(d[i]) && !(i < 12 && char.IsAsciiLetterUpper(d[i])))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TemFormaDeCpf(string d) => d.Length == 11 && d.All(char.IsAsciiDigit);

    /// <summary>00000-000</summary>
    public static string Cep(string? v)
    {
        string d = SoDigitos(v);
        return d.Length != 8 ? v ?? string.Empty : $"{d[..5]}-{d.Substring(5, 3)}";
    }

    /// <summary>(00) 0000-0000 ou (00) 00000-0000.</summary>
    public static string Telefone(string? v)
    {
        string d = SoDigitos(v);

        return d.Length switch
        {
            10 => $"({d[..2]}) {d.Substring(2, 4)}-{d.Substring(6, 4)}",
            11 => $"({d[..2]}) {d.Substring(2, 5)}-{d.Substring(7, 4)}",
            8 => $"{d[..4]}-{d.Substring(4, 4)}",
            9 => $"{d[..5]}-{d.Substring(5, 4)}",
            _ => v ?? string.Empty,
        };
    }

    /// <summary>dd/MM/yyyy</summary>
    public static string Data(DateTimeOffset? v) =>
        v is null ? string.Empty : v.Value.ToString("dd/MM/yyyy", Br);

    public static string Data(DateOnly? v) =>
        v is null ? string.Empty : v.Value.ToString("dd/MM/yyyy", Br);

    /// <summary>HH:mm:ss</summary>
    public static string Hora(DateTimeOffset? v) =>
        v is null ? string.Empty : v.Value.ToString("HH:mm:ss", Br);

    /// <summary>dd/MM/yyyy HH:mm:ss</summary>
    public static string DataHora(DateTimeOffset? v) =>
        v is null ? string.Empty : v.Value.ToString("dd/MM/yyyy HH:mm:ss", Br);

    /// <summary>
    /// Numero da nota em grupos de tres: 000.000.214. E como o MOC mostra o
    /// numero no quadro de identificacao.
    /// </summary>
    public static string NumeroNota(string? nNF)
    {
        string d = SoDigitos(nNF).TrimStart('0');
        if (d.Length == 0)
        {
            return "0";
        }

        d = d.PadLeft(9, '0');
        return $"{d[..3]}.{d.Substring(3, 3)}.{d.Substring(6, 3)}";
    }

    /// <summary>Serie sem zeros a esquerda.</summary>
    public static string Serie(string? serie)
    {
        string d = SoDigitos(serie).TrimStart('0');
        return d.Length == 0 ? "0" : d;
    }

    private static string SoDigitos(string? v) => Filtrar(v, char.IsAsciiDigit);

    /// <summary>
    /// Tira a mascara - ponto, barra, hifen, espaco - e fica com o resto. A
    /// letra fica: e parte do CNPJ alfanumerico, e jogada fora deixava onze
    /// digitos que se passavam por CPF.
    /// </summary>
    private static string SoLetrasEDigitos(string? v) => Filtrar(v, char.IsAsciiLetterOrDigit);

    private static string Filtrar(string? v, Func<char, bool> manter)
    {
        if (string.IsNullOrEmpty(v))
        {
            return string.Empty;
        }

        Span<char> buffer = v.Length <= 64 ? stackalloc char[v.Length] : new char[v.Length];
        int n = 0;

        foreach (char c in v)
        {
            if (manter(c))
            {
                buffer[n++] = c;
            }
        }

        return new string(buffer[..n]);
    }
}
