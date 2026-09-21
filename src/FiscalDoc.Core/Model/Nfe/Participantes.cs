namespace FiscalDoc.Core.Model.Nfe;

/// <summary>Endereco de emitente ou destinatario.</summary>
public sealed record Endereco(
    string? Logradouro,
    string? Numero,
    string? Complemento,
    string? Bairro,
    string? CodigoMunicipio,
    string? Municipio,
    string? Uf,
    string? Cep,
    string? Pais,
    string? Fone)
{
    /// <summary>"Rua X, 123 - Sala 4" para uma linha unica do DANFE.</summary>
    public string LinhaLogradouro
    {
        get
        {
            string b = Logradouro ?? string.Empty;
            if (!string.IsNullOrEmpty(Numero))
            {
                b = b.Length > 0 ? $"{b}, {Numero}" : Numero;
            }

            if (!string.IsNullOrEmpty(Complemento))
            {
                b = b.Length > 0 ? $"{b} - {Complemento}" : Complemento;
            }

            return b;
        }
    }
}

public sealed record Emitente(
    string? RazaoSocial,
    string? NomeFantasia,
    string? Cnpj,
    string? Cpf,
    string? InscricaoEstadual,
    string? InscricaoEstadualSt,
    string? InscricaoMunicipal,
    int? Crt,
    Endereco Endereco)
{
    public string? Documento => Cnpj ?? Cpf;
}

public sealed record Destinatario(
    string? RazaoSocial,
    string? Cnpj,
    string? Cpf,
    string? IdEstrangeiro,
    string? InscricaoEstadual,
    string? InscricaoSuframa,
    string? Email,
    Endereco Endereco)
{
    public string? Documento => Cnpj ?? Cpf ?? IdEstrangeiro;
}

/// <summary>Bloco TRANSPORTADOR do DANFE. Todos os campos sao opcionais.</summary>
public sealed record Transportador(
    string? RazaoSocial,
    string? Cnpj,
    string? Cpf,
    string? InscricaoEstadual,
    string? EnderecoCompleto,
    string? Municipio,
    string? Uf)
{
    public string? Documento => Cnpj ?? Cpf;
}

/// <summary>Dados do veiculo (grupo X18), impressos no bloco do transportador.</summary>
public sealed record Veiculo(string? Placa, string? Uf, string? Rntc);

/// <summary>Grupo vol (X26). Uma NF-e pode ter varios volumes.</summary>
public sealed record Volume(
    decimal? Quantidade,
    string? Especie,
    string? Marca,
    string? Numeracao,
    decimal? PesoLiquido,
    decimal? PesoBruto);
