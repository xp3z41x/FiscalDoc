using System.Xml.Linq;
using FiscalDoc.Core.Model;
using FiscalDoc.Core.Model.Mdfe;
using FiscalDoc.Core.Model.Nfe;
using FiscalDoc.Core.Values;

namespace FiscalDoc.Core.Parsing;

/// <summary>MDF-e modelo 58, leiaute 3.00, com e sem envelope mdfeProc.</summary>
internal static class MdfeParser
{
    internal static ResultadoLeitura Ler(XElement raiz)
    {
        XElement? inf = DocumentSniffer.AcharInfo(raiz, "MDFe", "infMDFe");
        if (inf is null)
        {
            return new ResultadoLeitura.NaoFiscal(raiz.Name.LocalName);
        }

        XElement? ide = inf.El("ide");
        string? modelo = ide.Str("mod");

        if (modelo != ModeloFiscal.Mdfe)
        {
            return new ResultadoLeitura.ModeloForaDoEscopo(modelo ?? "?");
        }

        XElement? rodo = inf.Desce("infModal", "rodo");

        var doc = new MdfeDocumento(
            Chave: ChaveAcesso.DeAtributoId(inf.Attr("Id")),
            VersaoLeiaute: inf.Attr("versao"),
            Numero: ide.Str("nMDF"),
            Serie: ide.Str("serie"),
            DataHoraEmissao: ide.DataHora("dhEmi"),
            Modal: (ModalMdfe)(ide.Int("modal") ?? 1),
            TipoEmitente: (TipoEmitenteMdfe)(ide.Int("tpEmit") ?? 1),
            TipoImpressao: (TipoImpressao)(ide.Int("tpImp") ?? 1),
            TipoEmissao: (TipoEmissao)(ide.Int("tpEmis") ?? 1),
            Ambiente: XEl.LerAmbiente(ide.Int("tpAmb")),
            UfInicio: ide.Str("UFIni"),
            UfFim: ide.Str("UFFim"),
            MunicipiosCarregamento: LerMunicipiosCarregamento(ide),
            Percurso: LerPercurso(ide),
            DataHoraInicioViagem: ide.DataHora("dhIniViagem"),
            Emitente: LerEmitente(inf.El("emit")),
            Rntrc: rodo.Desce("infANTT").Str("RNTRC") ?? rodo.Str("RNTRC"),
            VeiculoTracao: LerVeiculo(rodo.El("veicTracao")),
            Reboques: LerReboques(rodo),
            Documentos: LerDocumentos(inf.El("infDoc")),
            Totais: LerTotais(inf.El("tot")),
            Lacres: LerLacres(inf),
            ProdutoPredominante: inf.Desce("prodPred").Str("xProd"),
            Observacoes: inf.Desce("infAdic").Str("infCpl"),
            DataHoraContingencia: ide.DataHora("dhCont"),
            JustificativaContingencia: ide.Str("xJust"),
            Protocolo: LerProtocolo(raiz.Desce("protMDFe", "infProt")));

        return new ResultadoLeitura.Ok(doc);
    }

    private static List<string> LerMunicipiosCarregamento(XElement? ide)
    {
        var lista = new List<string>();
        foreach (XElement m in ide.Els("infMunCarrega"))
        {
            string? nome = m.Str("xMunCarrega");
            if (nome is not null)
            {
                lista.Add(nome);
            }
        }

        return lista;
    }

    private static List<string> LerPercurso(XElement? ide)
    {
        var lista = new List<string>();
        foreach (XElement p in ide.Els("infPercurso"))
        {
            string? uf = p.Str("UFPer");
            if (uf is not null)
            {
                lista.Add(uf);
            }
        }

        return lista;
    }

    private static ParticipanteMdfe LerEmitente(XElement? emit) => new(
        RazaoSocial: emit.Str("xNome"),
        NomeFantasia: emit.Str("xFant"),
        Cnpj: emit.Str("CNPJ"),
        Cpf: emit.Str("CPF"),
        InscricaoEstadual: emit.Str("IE"),
        Endereco: new Endereco(
            emit.Desce("enderEmit").Str("xLgr"),
            emit.Desce("enderEmit").Str("nro"),
            emit.Desce("enderEmit").Str("xCpl"),
            emit.Desce("enderEmit").Str("xBairro"),
            emit.Desce("enderEmit").Str("cMun"),
            emit.Desce("enderEmit").Str("xMun"),
            emit.Desce("enderEmit").Str("UF"),
            emit.Desce("enderEmit").Str("CEP"),
            null,
            emit.Desce("enderEmit").Str("fone")));

    private static VeiculoMdfe? LerVeiculo(XElement? v) => v is null
        ? null
        : new VeiculoMdfe(
            Placa: v.Str("placa"),
            Renavam: v.Str("RENAVAM"),
            Uf: v.Str("UF"),
            Tara: v.Dec("tara"),
            CapacidadeKg: v.Dec("capKG"),
            Condutores: LerCondutores(v));

    private static List<Condutor> LerCondutores(XElement? v)
    {
        var lista = new List<Condutor>();
        foreach (XElement c in v.Els("condutor"))
        {
            lista.Add(new Condutor(c.Str("xNome"), c.Str("CPF")));
        }

        return lista;
    }

    private static List<VeiculoMdfe> LerReboques(XElement? rodo)
    {
        var lista = new List<VeiculoMdfe>();
        foreach (XElement r in rodo.Els("veicReboque"))
        {
            VeiculoMdfe? v = LerVeiculo(r);
            if (v is not null)
            {
                lista.Add(v);
            }
        }

        return lista;
    }

    /// <summary>
    /// Documentos vinculados, agrupados por municipio de descarga - que e como
    /// o DAMDFE os imprime: o motorista precisa ver o que entrega onde.
    /// </summary>
    private static List<DocumentoDescarga> LerDocumentos(XElement? infDoc)
    {
        var lista = new List<DocumentoDescarga>();

        foreach (XElement mun in infDoc.Els("infMunDescarga"))
        {
            string? nome = mun.Str("xMunDescarga");
            string? codigo = mun.Str("cMunDescarga");

            foreach (XElement c in mun.Els("infCTe"))
            {
                lista.Add(new DocumentoDescarga(nome, codigo, "CT-e", c.Str("chCTe")));
            }

            foreach (XElement n in mun.Els("infNFe"))
            {
                lista.Add(new DocumentoDescarga(nome, codigo, "NF-e", n.Str("chNFe")));
            }

            foreach (XElement m in mun.Els("infMDFeTransp"))
            {
                lista.Add(new DocumentoDescarga(nome, codigo, "MDF-e", m.Str("chMDFe")));
            }
        }

        return lista;
    }

    private static TotaisMdfe LerTotais(XElement? t) => new(
        QuantidadeCte: t.Int("qCTe"),
        QuantidadeNfe: t.Int("qNFe"),
        QuantidadeMdfe: t.Int("qMDFe"),
        ValorCarga: t.Dec("vCarga"),
        UnidadeMedida: t.Str("cUnid") switch
        {
            "01" => "KG",
            "02" => "TON",
            _ => t.Str("cUnid"),
        },
        PesoCarga: t.Dec("qCarga"));

    private static List<string> LerLacres(XElement? inf)
    {
        var lista = new List<string>();
        foreach (XElement l in inf.Els("lacres"))
        {
            string? n = l.Str("nLacre");
            if (n is not null)
            {
                lista.Add(n);
            }
        }

        return lista;
    }

    private static Protocolo? LerProtocolo(XElement? p) => p is null
        ? null
        : new Protocolo(
            Numero: p.Str("nProt"),
            DataHoraRecebimento: p.DataHora("dhRecbto"),
            CodigoStatus: p.Str("cStat"),
            Motivo: p.Str("xMotivo"),
            ChaveConfirmada: p.Str("chMDFe"));
}
