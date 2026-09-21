using System.Globalization;
using System.Xml.Linq;
using FiscalDoc.Core.Model;
using FiscalDoc.Core.Model.Cte;
using FiscalDoc.Core.Model.Nfe;
using FiscalDoc.Core.Values;

namespace FiscalDoc.Core.Parsing;

/// <summary>
/// CT-e modelo 57, leiautes <b>3.00 e 4.00</b>, com e sem envelope cteProc.
///
/// As duas versoes sao lidas pelo mesmo caminho porque a diferenca entre elas,
/// nos campos que o DACTE imprime, se resume a campos que passaram a existir -
/// e campo ausente ja vira null por contrato. O CT-e 4.00 so entrou em
/// producao em 06/2023 e a guarda fiscal e de 5 anos, entao documentos 3.00
/// continuarao aparecendo em arquivo ate cerca de 2028. Ver plano 1.4.
/// </summary>
internal static class CteParser
{
    internal static ResultadoLeitura Ler(XElement raiz)
    {
        XElement? inf = DocumentSniffer.AcharInfo(raiz, "CTe", "infCte");

        if (inf is null)
        {
            // CT-e OS (modelo 67) usa CTeOS/infCte e esta fora do escopo.
            XElement? os = DocumentSniffer.AcharInfo(raiz, "CTeOS", "infCte");
            return os is not null
                ? new ResultadoLeitura.ModeloForaDoEscopo(os.El("ide").Str("mod") ?? ModeloFiscal.CteOs)
                : new ResultadoLeitura.NaoFiscal(raiz.Name.LocalName);
        }

        XElement? ide = inf.El("ide");
        string? modelo = ide.Str("mod");

        if (modelo != ModeloFiscal.Cte)
        {
            return new ResultadoLeitura.ModeloForaDoEscopo(modelo ?? "?");
        }

        XElement? norm = inf.El("infCTeNorm");
        XElement? compl = inf.El("compl");

        var doc = new CteDocumento(
            Chave: ChaveAcesso.DeAtributoId(inf.Attr("Id")),
            VersaoLeiaute: inf.Attr("versao"),
            Numero: ide.Str("nCT"),
            Serie: ide.Str("serie"),
            DataHoraEmissao: ide.DataHora("dhEmi"),
            NaturezaOperacao: ide.Str("natOp"),
            Cfop: ide.Str("CFOP"),
            Tipo: (TipoCte)(ide.Int("tpCTe") ?? 0),
            Servico: (TipoServicoCte)(ide.Int("tpServ") ?? 0),
            Modal: (ModalCte)(ide.Int("modal") ?? 1),
            TipoImpressao: (TipoImpressao)(ide.Int("tpImp") ?? 2),
            TipoEmissao: (TipoEmissao)(ide.Int("tpEmis") ?? 1),
            Ambiente: XEl.LerAmbiente(ide.Int("tpAmb")),
            MunicipioEnvio: ide.Str("xMunEnv"),
            UfEnvio: ide.Str("UFEnv"),
            MunicipioInicio: ide.Str("xMunIni"),
            UfInicio: ide.Str("UFIni"),
            MunicipioFim: ide.Str("xMunFim"),
            UfFim: ide.Str("UFFim"),
            CodigoTomador: LerCodigoTomador(ide),
            Emitente: LerParticipante(inf.El("emit"), "enderEmit"),
            Remetente: LerOpcional(inf.El("rem"), "enderReme"),
            Expedidor: LerOpcional(inf.El("exped"), "enderExped"),
            Recebedor: LerOpcional(inf.El("receb"), "enderReceb"),
            Destinatario: LerOpcional(inf.El("dest"), "enderDest"),
            Tomador: LerOpcional(ide.Desce("toma4"), "enderToma"),
            ValorTotalPrestacao: inf.Desce("vPrest").Dec("vTPrest"),
            ValorReceber: inf.Desce("vPrest").Dec("vRec"),
            Componentes: LerComponentes(inf.El("vPrest")),
            Icms: LerIcms(inf.Desce("imp", "ICMS")),
            ValorTotalTributos: inf.Desce("imp").Dec("vTotTrib"),
            ValorCarga: norm.Desce("infCarga").Dec("vCarga"),
            ProdutoPredominante: norm.Desce("infCarga").Str("proPred"),
            Quantidades: LerQuantidades(norm.El("infCarga")),
            DocumentosOriginarios: LerDocumentos(norm.El("infDoc")),
            Rntrc: LerRntrc(norm.El("infModal")),
            Observacoes: compl.Str("xObs"),
            ObservacoesFisco: inf.Desce("imp").Str("infAdFisco"),
            DataHoraContingencia: ide.DataHora("dhCont"),
            JustificativaContingencia: ide.Str("xJust"),
            Protocolo: LerProtocolo(raiz.Desce("protCTe", "infProt")));

        return new ResultadoLeitura.Ok(doc);
    }

    /// <summary>
    /// O tomador vem em toma3/toma (codigo 0 a 3) ou, quando e "outros", em
    /// toma4/toma com valor 4 e os dados do participante no proprio toma4.
    /// </summary>
    private static int? LerCodigoTomador(XElement? ide)
    {
        XElement? toma3 = ide.El("toma3");
        if (toma3 is not null)
        {
            return toma3.Int("toma");
        }

        XElement? toma4 = ide.El("toma4");
        return toma4?.Int("toma") ?? 4;
    }

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

    private static ParticipanteCte LerParticipante(XElement? p, string nomeEndereco) => new(
        RazaoSocial: p.Str("xNome"),
        NomeFantasia: p.Str("xFant"),
        Cnpj: p.Str("CNPJ"),
        Cpf: p.Str("CPF"),
        InscricaoEstadual: p.Str("IE"),
        Fone: p.Str("fone") ?? p.Desce(nomeEndereco).Str("fone"),
        Email: p.Str("email"),
        Endereco: LerEndereco(p.El(nomeEndereco)));

    private static ParticipanteCte? LerOpcional(XElement? p, string nomeEndereco) =>
        p is null ? null : LerParticipante(p, nomeEndereco);

    private static List<ComponenteValor> LerComponentes(XElement? vPrest)
    {
        var lista = new List<ComponenteValor>();
        foreach (XElement c in vPrest.Els("Comp"))
        {
            lista.Add(new ComponenteValor(c.Str("xNome"), c.Dec("vComp")));
        }

        return lista;
    }

    /// <summary>
    /// ICMS do CT-e e grupo de escolha: ICMS00, ICMS20, ICMS45, ICMS60,
    /// ICMS90, ICMSOutraUF ou ICMSSN. O DACTE imprime as mesmas informacoes
    /// para todos, entao vale o mesmo achatamento do ICMS da NF-e.
    /// </summary>
    private static IcmsCte LerIcms(XElement? icms)
    {
        XElement? v = icms.FilhoUnico();

        return new IcmsCte(
            Cst: v.Str("CST"),
            BaseCalculo: v.Dec("vBC") ?? v.Dec("vBCOutraUF"),
            Aliquota: v.Dec("pICMS") ?? v.Dec("pICMSOutraUF"),
            Valor: v.Dec("vICMS") ?? v.Dec("vICMSOutraUF"),
            PercentualReducaoBc: v.Dec("pRedBC") ?? v.Dec("pRedBCOutraUF"),

            // ICMS45 (isento/nao tributado) traz o motivo da desoneracao.
            Motivo: v?.Name.LocalName is "ICMS45" ? "Isento / não tributado" : null);
    }

    private static List<QuantidadeCarga> LerQuantidades(XElement? infCarga)
    {
        var lista = new List<QuantidadeCarga>();
        foreach (XElement q in infCarga.Els("infQ"))
        {
            lista.Add(new QuantidadeCarga(
                Unidade: DescreverUnidade(q.Str("cUnid")),
                TipoMedida: q.Str("tpMed"),
                Quantidade: q.Dec("qCarga")));
        }

        return lista;
    }

    private static string DescreverUnidade(string? c) => c switch
    {
        "00" => "M3",
        "01" => "KG",
        "02" => "TON",
        "03" => "UNIDADE",
        "04" => "LITROS",
        "05" => "MMBTU",
        _ => c ?? string.Empty,
    };

    /// <summary>
    /// Documentos originarios. O grupo infDoc traz infNFe (chave), infNF
    /// (documento em papel) ou infOutros. No modal aquaviario o MOC permite
    /// omitir o quadro e detalhar os conteineres em seu lugar.
    /// </summary>
    private static List<DocumentoOriginario> LerDocumentos(XElement? infDoc)
    {
        var lista = new List<DocumentoOriginario>();

        foreach (XElement n in infDoc.Els("infNFe"))
        {
            lista.Add(new DocumentoOriginario("NF-e", n.Str("chave"), null, null, null));
        }

        foreach (XElement n in infDoc.Els("infNF"))
        {
            lista.Add(new DocumentoOriginario(
                "NF", null, n.Str("nDoc"), n.Str("serie"), n.Dec("vNF")));
        }

        foreach (XElement n in infDoc.Els("infOutros"))
        {
            lista.Add(new DocumentoOriginario(
                DescreverOutro(n.Str("tpDoc")), null, n.Str("nDoc"), null, n.Dec("vDocFisc")));
        }

        return lista;
    }

    private static string DescreverOutro(string? tp) => tp switch
    {
        "00" => "Declaração",
        "10" => "Dutoviário",
        "59" => "CF-e SAT",
        "65" => "NFC-e",
        "99" => "Outros",
        _ => "Documento",
    };

    /// <summary>
    /// RNTRC vive dentro do grupo do modal, em caminho diferente conforme o
    /// modal e a versao do leiaute. Procurar pelo nome em qualquer
    /// profundidade evita um emaranhado de condicionais por modal.
    /// </summary>
    private static string? LerRntrc(XElement? infModal)
    {
        if (infModal is null)
        {
            return null;
        }

        foreach (XElement e in infModal.Descendants())
        {
            if (e.Name.LocalName is "RNTRC")
            {
                string v = e.Value.Trim();
                if (v.Length > 0)
                {
                    return v;
                }
            }
        }

        return null;
    }

    private static Protocolo? LerProtocolo(XElement? p) => p is null
        ? null
        : new Protocolo(
            Numero: p.Str("nProt"),
            DataHoraRecebimento: p.DataHora("dhRecbto"),
            CodigoStatus: p.Str("cStat"),
            Motivo: p.Str("xMotivo"),
            ChaveConfirmada: p.Str("chCTe"));

    internal static string Formatar(decimal? v) =>
        v?.ToString("N2", CultureInfo.GetCultureInfo("pt-BR")) ?? string.Empty;
}
