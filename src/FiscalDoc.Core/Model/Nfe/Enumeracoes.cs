namespace FiscalDoc.Core.Model.Nfe;

/// <summary>tpNF (B11): sentido da operacao, impresso no quadro 0/1 do DANFE.</summary>
public enum TipoOperacao
{
    Entrada = 0,
    Saida = 1,
}

/// <summary>
/// tpImp (B21). Dominio completo do leiaute 4.00; so Retrato e Paisagem estao
/// no escopo. Os demais existem para que a recusa seja informativa.
/// </summary>
public enum TipoImpressao
{
    SemDanfe = 0,
    Retrato = 1,
    Paisagem = 2,
    Simplificado = 3,
    Nfce = 4,
    NfceMensagemEletronica = 5,
}

/// <summary>
/// tpEmis (B22). Atencao a duas armadilhas: o valor 3 <b>nao</b> e mais SCAN
/// (extinto) e sim Regime Especial NFF; e o valor 4 aparece no XSD como "DPEC"
/// mas o MOC vigente chama de EPEC - mesmo valor.
/// </summary>
public enum TipoEmissao
{
    Normal = 1,
    ContingenciaFsIa = 2,
    RegimeEspecialNff = 3,
    ContingenciaEpec = 4,
    ContingenciaFsDa = 5,
    ContingenciaSvcAn = 6,
    ContingenciaSvcRs = 7,
    ContingenciaOfflineNfce = 9,
}

/// <summary>tpAmb (B24).</summary>
public enum Ambiente
{
    Producao = 1,
    Homologacao = 2,
}

/// <summary>modFrete (X02), impresso como codigo + rotulo (MOC Anexo II 3.1.10).</summary>
public enum ModalidadeFrete
{
    ContaRemetenteCif = 0,
    ContaDestinatarioFob = 1,
    ContaTerceiros = 2,
    TransporteProprioRemetente = 3,
    TransporteProprioDestinatario = 4,
    SemTransporte = 9,
}

public static class Rotulos
{
    public static string Frete(ModalidadeFrete m) => m switch
    {
        ModalidadeFrete.ContaRemetenteCif => "0 - Rem/CIF",
        ModalidadeFrete.ContaDestinatarioFob => "1 - Dest/FOB",
        ModalidadeFrete.ContaTerceiros => "2 - Terceiros",
        ModalidadeFrete.TransporteProprioRemetente => "3 - Proprio/Rem",
        ModalidadeFrete.TransporteProprioDestinatario => "4 - Proprio/Dest",
        ModalidadeFrete.SemTransporte => "9 - Sem Frete",
        _ => ((int)m).ToString(System.Globalization.CultureInfo.InvariantCulture),
    };

    /// <summary>
    /// Dizeres obrigatorios de contingencia, impressos no corpo do DANFE
    /// (MOC 7.0 Anexo III). Normal e NFF nao tem dizer proprio.
    /// </summary>
    public static string? DizerContingencia(TipoEmissao t) => t switch
    {
        TipoEmissao.ContingenciaFsIa or TipoEmissao.ContingenciaFsDa =>
            "DANFE em Contingência - impresso em decorrência de problemas técnicos",

        TipoEmissao.ContingenciaEpec =>
            "DANFE impresso em contingência - EPEC regularmente recebida pela Receita Federal do Brasil",

        // SVC-AN e SVC-RS nao tem legenda propria; o que se imprime e o
        // motivo (xJust) e a data/hora de inicio (dhCont).
        _ => null,
    };

    public static bool ExigeJustificativaContingencia(TipoEmissao t) => t
        is TipoEmissao.ContingenciaFsIa
        or TipoEmissao.ContingenciaFsDa
        or TipoEmissao.ContingenciaSvcAn
        or TipoEmissao.ContingenciaSvcRs;

    /// <summary>
    /// tPag (YA02) por extenso, para o quadro de formas de pagamento do DANFE
    /// NFC-e. Tabela do leiaute 4.00 com os codigos 20, 21 e 22 acrescentados
    /// pela NT 2023.004.
    ///
    /// <para>Codigo desconhecido devolve o proprio codigo, nunca um rotulo
    /// aproximado: uma tabela que envelhece nao pode inventar o significado de
    /// um codigo novo. E o mesmo criterio de <see cref="Frete"/>.</para>
    /// </summary>
    public static string FormaPagamento(string? tPag) => tPag switch
    {
        "01" => "Dinheiro",
        "02" => "Cheque",
        "03" => "Cartão de Crédito",
        "04" => "Cartão de Débito",
        "05" => "Crédito Loja",
        "10" => "Vale Alimentação",
        "11" => "Vale Refeição",
        "12" => "Vale Presente",
        "13" => "Vale Combustível",
        "14" => "Duplicata Mercantil",
        "15" => "Boleto Bancário",
        "16" => "Depósito Bancário",
        "17" => "Pagamento Instantâneo (PIX) - Dinâmico",
        "18" => "Transferência bancária, Carteira Digital",
        "19" => "Programa de fidelidade, Cashback, Crédito Virtual",
        "20" => "Pagamento Instantâneo (PIX) - Estático",
        "21" => "Crédito em Loja",
        "22" => "Pagamento Eletrônico não Informado",
        "90" => "Sem pagamento",
        "99" => "Outros",
        null => string.Empty,
        _ => tPag,
    };
}
