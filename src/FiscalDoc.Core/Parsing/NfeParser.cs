using System.Globalization;
using System.Xml.Linq;
using FiscalDoc.Core.Model;
using FiscalDoc.Core.Model.Nfe;
using FiscalDoc.Core.Values;

namespace FiscalDoc.Core.Parsing;

/// <summary>
/// NF-e modelo 55 e NFC-e modelo 65, leiaute 4.00, com e sem envelope nfeProc.
///
/// <para>Um parser so para os dois porque e um leiaute so: a NFC-e usa o mesmo
/// arquivo da NF-e, com <c>mod=65</c> e o grupo extra <c>infNFeSupl</c>
/// (QR Code e endereco de consulta). O que difere entre os dois modelos e o
/// desenho, nao a leitura.</para>
///
/// <para>Regra geral: campo ausente vira null, nunca excecao e nunca valor
/// inventado. O MOC 3.1 e categorico - "nao poderao ser impressas informacoes
/// que nao constem do arquivo da NF-e" - entao o parser nao preenche lacuna.
/// O Manual do DANFE NFC-e repete a regra com uma excecao nominal: as
/// informacoes do XML de retorno da autorizacao, que e o protocolo.</para>
/// </summary>
internal static class NfeParser
{
    internal static ResultadoLeitura Ler(XElement raiz)
    {
        XElement? inf = DocumentSniffer.AcharInfo(raiz, "NFe", "infNFe");
        if (inf is null)
        {
            return new ResultadoLeitura.NaoFiscal(raiz.Name.LocalName);
        }

        XElement? ide = inf.El("ide");
        string? modelo = ide.Str("mod");

        // Familia NF-e cobre 55 e 65, e os dois estao no escopo. Qualquer
        // outro valor aqui e arquivo de outro documento com envelope de NF-e.
        if (modelo is not (ModeloFiscal.Nfe or ModeloFiscal.Nfce))
        {
            return new ResultadoLeitura.ModeloForaDoEscopo(modelo ?? "?");
        }

        var doc = new NfeDocumento(
            Chave: ChaveAcesso.DeAtributoId(inf.Attr("Id")),
            VersaoLeiaute: inf.Attr("versao"),
            Ide: LerIde(ide),
            Emitente: LerEmitente(inf.El("emit")),
            Destinatario: LerDestinatario(inf.El("dest")),
            Itens: LerItens(inf),
            Totais: LerTotais(inf.Desce("total", "ICMSTot")),
            Issqn: LerIssqn(inf.Desce("total", "ISSQNtot")),
            IbsCbs: LerIbsCbsTotal(inf.El("total")),
            Frete: LerFrete(inf.Desce("transp", "modFrete")),
            Transportador: LerTransportador(inf.Desce("transp", "transporta")),
            Veiculo: LerVeiculo(inf.Desce("transp", "veicTransp")),
            Volumes: LerVolumes(inf.El("transp")),
            Fatura: LerFatura(inf.Desce("cobr", "fat")),
            Duplicatas: LerDuplicatas(inf.El("cobr")),
            InfoAdicionais: LerInfoAdicionais(inf.El("infAdic")),
            Pagamentos: LerPagamentos(inf.El("pag")),
            // infNFeSupl e irmao de infNFe dentro de NFe, entao acha-se do
            // mesmo jeito - com ou sem envelope nfeProc.
            Suplementares: LerSuplementares(
                DocumentSniffer.AcharInfo(raiz, "NFe", "infNFeSupl")),
            Protocolo: LerProtocolo(raiz.Desce("protNFe", "infProt")));

        return new ResultadoLeitura.Ok(doc);
    }

    private static Identificacao LerIde(XElement? ide) => new(
        CodigoUf: ide.Str("cUF"),
        NaturezaOperacao: ide.Str("natOp"),
        Modelo: ide.Str("mod"),
        Serie: ide.Str("serie"),
        Numero: ide.Str("nNF"),
        DataHoraEmissao: ide.DataHora("dhEmi"),
        DataHoraSaidaEntrada: ide.DataHora("dhSaiEnt"),
        TipoOperacao: (TipoOperacao)(ide.Int("tpNF") ?? 1),
        TipoImpressao: (TipoImpressao)(ide.Int("tpImp") ?? 1),
        TipoEmissao: (TipoEmissao)(ide.Int("tpEmis") ?? 1),
        Ambiente: XEl.LerAmbiente(ide.Int("tpAmb")),
        MunicipioFatoGerador: ide.Str("cMunFG"),
        DataHoraContingencia: ide.DataHora("dhCont"),
        JustificativaContingencia: ide.Str("xJust"));

    private static Endereco LerEndereco(XElement? e) => new(
        Logradouro: e.Str("xLgr"),
        Numero: e.Str("nro"),
        Complemento: e.Str("xCpl"),
        Bairro: e.Str("xBairro"),
        CodigoMunicipio: e.Str("cMun"),
        Municipio: e.Str("xMun"),
        Uf: e.Str("UF"),
        Cep: e.Str("CEP"),
        Pais: e.Str("xPais"),
        Fone: e.Str("fone"));

    private static Emitente LerEmitente(XElement? emit) => new(
        RazaoSocial: emit.Str("xNome"),
        NomeFantasia: emit.Str("xFant"),
        Cnpj: emit.Str("CNPJ"),
        Cpf: emit.Str("CPF"),
        InscricaoEstadual: emit.Str("IE"),
        InscricaoEstadualSt: emit.Str("IEST"),
        InscricaoMunicipal: emit.Str("IM"),
        Crt: emit.Int("CRT"),
        Endereco: LerEndereco(emit.El("enderEmit")));

    private static Destinatario LerDestinatario(XElement? dest) => new(
        RazaoSocial: dest.Str("xNome"),
        Cnpj: dest.Str("CNPJ"),
        Cpf: dest.Str("CPF"),
        IdEstrangeiro: dest.Str("idEstrangeiro"),
        InscricaoEstadual: dest.Str("IE"),
        InscricaoSuframa: dest.Str("ISUF"),
        Email: dest.Str("email"),
        Endereco: LerEndereco(dest.El("enderDest")));

    private static List<ItemNfe> LerItens(XElement inf)
    {
        var itens = new List<ItemNfe>();
        int sequencial = 0;

        foreach (XElement det in inf.Els("det"))
        {
            sequencial++;
            XElement? prod = det.El("prod");
            XElement? imposto = det.El("imposto");

            int numero = int.TryParse(
                det.Attr("nItem"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int n)
                ? n
                : sequencial;

            itens.Add(new ItemNfe(
                Numero: numero,
                Codigo: prod.Str("cProd"),
                Descricao: prod.Str("xProd"),
                Ncm: prod.Str("NCM"),
                Cest: prod.Str("CEST"),
                Cfop: prod.Str("CFOP"),
                Unidade: prod.Str("uCom"),
                Quantidade: prod.Dec("qCom"),
                ValorUnitario: prod.Dec("vUnCom"),
                ValorTotal: prod.Dec("vProd"),
                ValorDesconto: prod.Dec("vDesc"),
                ValorFrete: prod.Dec("vFrete"),
                ValorSeguro: prod.Dec("vSeg"),
                OutrasDespesas: prod.Dec("vOutro"),
                UnidadeTributavel: prod.Str("uTrib"),
                QuantidadeTributavel: prod.Dec("qTrib"),
                ValorUnitarioTributavel: prod.Dec("vUnTrib"),
                Icms: LerIcms(imposto.El("ICMS")),
                Ipi: LerIpi(imposto.El("IPI")),
                IbsCbs: LerIbsCbsItem(imposto.El("IBSCBS")),
                InformacaoAdicional: det.Str("infAdProd")));
        }

        return itens;
    }

    /// <summary>
    /// ICMS e um grupo de escolha: o filho unico pode ser ICMS00, ICMS20,
    /// ICMS40, ICMSSN101, ICMSSN102 e assim por diante. Como o DANFE imprime
    /// sempre as mesmas colunas, basta ler os campos pelo nome dentro de
    /// qualquer variante - o que nao existir naquela variante fica nulo.
    /// </summary>
    private static IcmsItem LerIcms(XElement? icms)
    {
        XElement? v = icms.FilhoUnico();

        return new IcmsItem(
            Origem: v.Str("orig"),
            Cst: v.Str("CST"),
            Csosn: v.Str("CSOSN"),
            BaseCalculo: v.Dec("vBC"),
            Aliquota: v.Dec("pICMS"),
            Valor: v.Dec("vICMS"),
            PercentualReducaoBc: v.Dec("pRedBC"),
            BaseCalculoSt: v.Dec("vBCST"),
            ValorSt: v.Dec("vICMSST"),
            AliquotaSt: v.Dec("pICMSST"));
    }

    private static IpiItem LerIpi(XElement? ipi)
    {
        // IPI carrega cEnq e um de IPITrib / IPINT. FilhoUnico nao serve aqui
        // porque cEnq tambem e filho direto; procura-se a variante pelo nome.
        XElement? v = ipi.El("IPITrib") ?? ipi.El("IPINT");

        return new IpiItem(
            Cst: v.Str("CST"),
            BaseCalculo: v.Dec("vBC"),
            Aliquota: v.Dec("pIPI"),
            Valor: v.Dec("vIPI"));
    }

    private static IbsCbsItem? LerIbsCbsItem(XElement? g)
    {
        if (g is null)
        {
            return null;
        }

        XElement? grupo = g.El("gIBSCBS");
        XElement? uf = grupo.El("gIBSUF");
        XElement? mun = grupo.El("gIBSMun");
        XElement? cbs = grupo.El("gCBS");

        return new IbsCbsItem(
            Cst: g.Str("CST"),
            ClassificacaoTributaria: g.Str("cClassTrib"),
            BaseCalculo: grupo.Dec("vBC"),
            ValorIbs: grupo.Dec("vIBS"),
            AliquotaIbsUf: uf.Dec("pIBSUF"),
            ValorIbsUf: uf.Dec("vIBSUF"),
            AliquotaIbsMun: mun.Dec("pIBSMun"),
            ValorIbsMun: mun.Dec("vIBSMun"),
            AliquotaCbs: cbs.Dec("pCBS"),
            ValorCbs: cbs.Dec("vCBS"));
    }

    private static TotaisIcms LerTotais(XElement? t) => new(
        BaseCalculoIcms: t.Dec("vBC"),
        ValorIcms: t.Dec("vICMS"),
        BaseCalculoIcmsSt: t.Dec("vBCST"),
        ValorIcmsSt: t.Dec("vST"),
        ValorTotalProdutos: t.Dec("vProd"),
        ValorFrete: t.Dec("vFrete"),
        ValorSeguro: t.Dec("vSeg"),
        ValorDesconto: t.Dec("vDesc"),
        OutrasDespesas: t.Dec("vOutro"),
        ValorIpi: t.Dec("vIPI"),
        ValorPis: t.Dec("vPIS"),
        ValorCofins: t.Dec("vCOFINS"),
        ValorIi: t.Dec("vII"),
        ValorIcmsDesonerado: t.Dec("vICMSDeson"),
        ValorFcp: t.Dec("vFCP"),
        ValorFcpSt: t.Dec("vFCPST"),
        ValorIpiDevolvido: t.Dec("vIPIDevol"),
        ValorTotalTributos: t.Dec("vTotTrib"),
        ValorTotalNota: t.Dec("vNF"));

    private static TotaisIssqn? LerIssqn(XElement? t) => t is null
        ? null
        : new TotaisIssqn(
            ValorTotalServicos: t.Dec("vServ"),
            BaseCalculo: t.Dec("vBC"),
            ValorIssqn: t.Dec("vISS"));

    /// <summary>
    /// Totais da reforma. Recebe o elemento <c>total</c> inteiro, e nao apenas
    /// o <c>IBSCBSTot</c>, porque dois dos campos que interessam sao irmaos
    /// dele e nao filhos: <c>ISTot/vIS</c> e <c>vNFTot</c>.
    /// </summary>
    private static TotaisIbsCbs? LerIbsCbsTotal(XElement? total)
    {
        XElement? t = total.El("IBSCBSTot");
        decimal? valorIs = total.Desce("ISTot").Dec("vIS");
        decimal? totalComTributos = total.Dec("vNFTot");

        // Sem nenhum dos tres nao ha reforma neste documento, e o bloco do
        // DANFE some por inteiro.
        if (t is null && valorIs is null && totalComTributos is null)
        {
            return null;
        }

        XElement? ibs = t.El("gIBS");
        XElement? cbs = t.El("gCBS");

        return new TotaisIbsCbs(
            BaseCalculo: t.Dec("vBCIBSCBS"),
            ValorIbs: ibs.Dec("vIBS"),
            ValorIbsUf: ibs.El("gIBSUF").Dec("vIBSUF"),
            ValorIbsMun: ibs.El("gIBSMun").Dec("vIBSMun"),
            ValorCbs: cbs.Dec("vCBS"),
            ValorIs: valorIs,
            ValorTotalNotaComTributos: totalComTributos);
    }

    private static ModalidadeFrete LerFrete(XElement? modFrete)
    {
        string? v = modFrete.Texto();
        return int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i)
            ? (ModalidadeFrete)i
            : ModalidadeFrete.SemTransporte;
    }

    private static Transportador? LerTransportador(XElement? t) => t is null
        ? null
        : new Transportador(
            RazaoSocial: t.Str("xNome"),
            Cnpj: t.Str("CNPJ"),
            Cpf: t.Str("CPF"),
            InscricaoEstadual: t.Str("IE"),
            EnderecoCompleto: t.Str("xEnder"),
            Municipio: t.Str("xMun"),
            Uf: t.Str("UF"));

    private static Veiculo? LerVeiculo(XElement? v) => v is null
        ? null
        : new Veiculo(v.Str("placa"), v.Str("UF"), v.Str("RNTC"));

    private static List<Volume> LerVolumes(XElement? transp)
    {
        var lista = new List<Volume>();
        foreach (XElement v in transp.Els("vol"))
        {
            lista.Add(new Volume(
                Quantidade: v.Dec("qVol"),
                Especie: v.Str("esp"),
                Marca: v.Str("marca"),
                Numeracao: v.Str("nVol"),
                PesoLiquido: v.Dec("pesoL"),
                PesoBruto: v.Dec("pesoB")));
        }

        return lista;
    }

    private static Fatura? LerFatura(XElement? f) => f is null
        ? null
        : new Fatura(
            Numero: f.Str("nFat"),
            ValorOriginal: f.Dec("vOrig"),
            ValorDesconto: f.Dec("vDesc"),
            ValorLiquido: f.Dec("vLiq"));

    private static List<Duplicata> LerDuplicatas(XElement? cobr)
    {
        var lista = new List<Duplicata>();
        foreach (XElement d in cobr.Els("dup"))
        {
            lista.Add(new Duplicata(
                Numero: d.Str("nDup"),
                Vencimento: d.Data("dVenc"),
                Valor: d.Dec("vDup")));
        }

        return lista;
    }

    /// <summary>
    /// Grupo pag (YA). Obrigatorio no leiaute 4.00 dos dois modelos, mas so o
    /// DANFE NFC-e tem quadro para ele.
    /// </summary>
    private static Pagamentos LerPagamentos(XElement? pag)
    {
        if (pag is null)
        {
            return Model.Nfe.Pagamentos.Vazio;
        }

        var formas = new List<Pagamento>();

        foreach (XElement d in pag.Els("detPag"))
        {
            formas.Add(new Pagamento(
                Codigo: d.Str("tPag"),
                Descricao: d.Str("xPag"),
                Valor: d.Dec("vPag")));
        }

        // vTroco e irmao dos detPag, nao filho de um deles.
        return new Pagamentos(formas, pag.Dec("vTroco"));
    }

    /// <summary>
    /// Grupo infNFeSupl (ZX), exclusivo do modelo 65. A urlChave costuma vir
    /// em CDATA; o leitor de XML ja resolve isso, e <c>Str</c> so apara.
    /// </summary>
    private static InformacoesSuplementares? LerSuplementares(XElement? s) => s is null
        ? null
        : new InformacoesSuplementares(
            QrCode: s.Str("qrCode"),
            UrlConsultaChave: s.Str("urlChave"));

    private static InformacoesAdicionais? LerInfoAdicionais(XElement? i) => i is null
        ? null
        : new InformacoesAdicionais(i.Str("infAdFisco"), i.Str("infCpl"));

    private static Protocolo? LerProtocolo(XElement? p) => p is null
        ? null
        : new Protocolo(
            Numero: p.Str("nProt"),
            DataHoraRecebimento: p.DataHora("dhRecbto"),
            CodigoStatus: p.Str("cStat"),
            Motivo: p.Str("xMotivo"),
            ChaveConfirmada: p.Str("chNFe"));
}
