namespace FiscalDoc.Core.Model.Nfe;

/// <summary>
/// ICMS do item. O leiaute usa um grupo de escolha (ICMS00, ICMS20, ICMS40,
/// ICMSSN101, ICMSSN102, ...), e o DANFE imprime sempre as mesmas colunas
/// independentemente de qual veio. O parser achata a escolha aqui: o campo que
/// nao existe naquela variante fica nulo, e a coluna sai vazia.
/// </summary>
public sealed record IcmsItem(
    string? Origem,
    string? Cst,
    string? Csosn,
    decimal? BaseCalculo,
    decimal? Aliquota,
    decimal? Valor,
    decimal? PercentualReducaoBc,
    decimal? BaseCalculoSt,
    decimal? ValorSt,
    decimal? AliquotaSt)
{
    /// <summary>
    /// Coluna "CST" do DANFE: origem concatenada com CST ou CSOSN.
    /// Ex.: origem 0 + CST 00 => "000"; origem 0 + CSOSN 102 => "0102".
    /// </summary>
    public string? CstOuCsosnFormatado
    {
        get
        {
            string? codigo = Cst ?? Csosn;
            if (codigo is null)
            {
                return Origem;
            }

            return Origem is null ? codigo : Origem + codigo;
        }
    }
}

public sealed record IpiItem(
    string? Cst,
    decimal? BaseCalculo,
    decimal? Aliquota,
    decimal? Valor);

/// <summary>
/// Grupo IBSCBS do item (reforma tributaria). Presente em 9 das 10 amostras
/// reais de 2026. O DANFE do Anexo II (2020) nao tem quadro para esses
/// valores; eles sao lidos aqui para ficarem disponiveis, e a decisao de onde
/// imprimi-los e do layout, nao do parser.
/// </summary>
public sealed record IbsCbsItem(
    string? Cst,
    string? ClassificacaoTributaria,
    decimal? BaseCalculo,
    decimal? ValorIbs,
    decimal? AliquotaIbsUf,
    decimal? ValorIbsUf,
    decimal? AliquotaIbsMun,
    decimal? ValorIbsMun,
    decimal? AliquotaCbs,
    decimal? ValorCbs);

/// <summary>Uma linha do quadro DADOS DOS PRODUTOS / SERVICOS.</summary>
public sealed record ItemNfe(
    int Numero,
    string? Codigo,
    string? Descricao,
    string? Ncm,
    string? Cest,
    string? Cfop,
    string? Unidade,
    decimal? Quantidade,
    decimal? ValorUnitario,
    decimal? ValorTotal,
    decimal? ValorDesconto,
    decimal? ValorFrete,
    decimal? ValorSeguro,
    decimal? OutrasDespesas,
    string? UnidadeTributavel,
    decimal? QuantidadeTributavel,
    decimal? ValorUnitarioTributavel,
    IcmsItem Icms,
    IpiItem Ipi,
    IbsCbsItem? IbsCbs,
    string? InformacaoAdicional)
{
    /// <summary>
    /// Item que ocupa mais de uma linha no quadro. O MOC 3.1.7 exige destaque
    /// divisorio quando isso acontece, e o infAdProd deve sair imediatamente
    /// abaixo do seu item.
    /// </summary>
    public bool TemInformacaoAdicional => !string.IsNullOrWhiteSpace(InformacaoAdicional);
}
