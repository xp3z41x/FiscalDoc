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
///
/// <para>O que e lido segue a secao 3 do manual do DACTE (MOC CT-e 4.00,
/// Anexo II, "Correlacao dos campos do XML do CT-e x DACTE"), mais os casos
/// especificos da secao 2.21.4 e, fora do manual, a cobranca, a previsao de
/// entrega, as caracteristicas adicionais e as ordens de coleta - que o
/// usuario pediu, e que estao no arquivo.</para>
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
        XElement? imp = inf.El("imp");
        XElement? carga = norm.El("infCarga");
        ParticipanteCte? remetente = LerOpcional(inf.El("rem"), "enderReme");

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

            // "Informar valor 1 quando for Globalizado e nao informar a tag
            // quando nao tratar de CT-e Globalizado" - ausencia e o "nao".
            Globalizado: ide.Int("indGlobalizado") == 1,
            MunicipioEnvio: ide.Str("xMunEnv"),
            UfEnvio: ide.Str("UFEnv"),
            MunicipioInicio: ide.Str("xMunIni"),
            UfInicio: ide.Str("UFIni"),
            MunicipioFim: ide.Str("xMunFim"),
            UfFim: ide.Str("UFFim"),
            CodigoTomador: LerCodigoTomador(ide),
            Emitente: LerParticipante(inf.El("emit"), "enderEmit"),
            Remetente: remetente,
            Expedidor: LerOpcional(inf.El("exped"), "enderExped"),
            Recebedor: LerOpcional(inf.El("receb"), "enderReceb"),
            Destinatario: LerOpcional(inf.El("dest"), "enderDest"),
            Tomador: LerOpcional(ide.Desce("toma4"), "enderToma"),
            ValorTotalPrestacao: inf.Desce("vPrest").Dec("vTPrest"),
            ValorReceber: inf.Desce("vPrest").Dec("vRec"),
            Componentes: LerComponentes(inf.El("vPrest")),
            Icms: LerIcms(imp.El("ICMS")),
            ValorTotalTributos: imp.Dec("vTotTrib"),
            IbsCbs: LerIbsCbs(imp),
            ValorCarga: carga.Dec("vCarga"),
            ProdutoPredominante: carga.Str("proPred"),
            OutrasCaracteristicasCarga: carga.Str("xOutCat"),
            Quantidades: LerQuantidades(carga),
            DocumentosOriginarios: LerDocumentos(norm.El("infDoc"), remetente),
            DocumentosAnteriores: LerDocumentosAnteriores(norm.El("docAnt")),
            Referencias: LerReferencias(inf, norm),
            InformacoesGlobalizado: norm.Desce("infGlobalizado").Str("xObs"),
            VeiculosNovos: LerVeiculosNovos(norm),
            Cobranca: LerCobranca(norm.El("cobr")),
            Rntrc: LerRntrc(norm.El("infModal")),
            DetalheModal: LerModal(norm.El("infModal")),
            Complemento: LerComplemento(compl),
            Observacoes: compl.Str("xObs"),
            ObservacoesFisco: imp.Str("infAdFisco"),
            DataHoraContingencia: ide.DataHora("dhCont"),
            JustificativaContingencia: ide.Str("xJust"),

            // "0 - sim; 1 - nao". Ausente e o "nao": o campo e obrigatorio, e
            // arquivo sem ele nao afirma que o recebedor retira nada.
            RecebedorRetira: ide.Int("retira") == 0,
            DetalhesRetirada: ide.Str("xDetRetira"),
            QrCode: LerQrCode(raiz, inf),
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
        Endereco: LerEndereco(p.El(nomeEndereco)),

        // So o destinatario tem ISUF; nos demais papeis fica null sozinho.
        InscricaoSuframa: p.Str("ISUF"));

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
    ///
    /// <para>O ICMS60 tem nome proprio para cada campo - vBCSTRet, pICMSSTRet,
    /// vICMSSTRet - e o manual do DACTE manda imprimi-los nas mesmas caixas
    /// de base, aliquota e valor ("vBC, vBCSTRet ou vBCOutraUF"). Sem eles, o
    /// CT-e com ICMS cobrado por substituicao saia com as tres caixas em
    /// branco - justamente o caso em que o tomador precisa do valor, porque e
    /// ele quem recolhe.</para>
    /// </summary>
    private static IcmsCte LerIcms(XElement? icms)
    {
        XElement? v = icms.FilhoUnico();
        string? cst = v.Str("CST");

        return new IcmsCte(
            Cst: cst,
            BaseCalculo: v.Dec("vBC") ?? v.Dec("vBCSTRet") ?? v.Dec("vBCOutraUF"),
            Aliquota: v.Dec("pICMS") ?? v.Dec("pICMSSTRet") ?? v.Dec("pICMSOutraUF"),
            Valor: v.Dec("vICMS") ?? v.Dec("vICMSSTRet") ?? v.Dec("vICMSOutraUF"),
            PercentualReducaoBc: v.Dec("pRedBC") ?? v.Dec("pRedBCOutraUF"),
            Descricao: DescreverIcms(v?.Name.LocalName, cst));
    }

    /// <summary>
    /// O CST por extenso. O grupo desempata o que o numero sozinho nao diz:
    /// CST 90 e "outros" em ICMS90, ICMS da UF de origem em ICMSOutraUF e
    /// Simples Nacional em ICMSSN. Os nomes sao os que o proprio schema da a
    /// cada grupo.
    /// </summary>
    private static string? DescreverIcms(string? grupo, string? cst)
    {
        string? nome = grupo switch
        {
            "ICMS00" => "Tributação normal",
            "ICMS20" => "Com redução de base de cálculo",
            "ICMS45" => cst switch
            {
                "40" => "Isenção",
                "41" => "Não tributado",
                "51" => "Diferimento",
                _ => "Isento, não tributado ou diferido",
            },
            "ICMS60" => "ICMS cobrado por substituição tributária",
            "ICMS90" => "Outros",
            "ICMSOutraUF" => "ICMS devido à UF de origem",
            "ICMSSN" => "Simples Nacional",
            _ => null,
        };

        if (nome is null)
        {
            return cst;
        }

        return cst is null ? nome : $"{cst} - {nome}";
    }

    /// <summary>
    /// IBS e CBS do CT-e (imp/IBSCBS e imp/vTotDFe), no mesmo registro dos
    /// totais da NF-e, para que o DACTE os imprima como o DANFE imprime.
    ///
    /// <para>No CT-e o grupo e do documento inteiro, e nao por item, e nao ha
    /// Imposto Seletivo sobre prestacao de transporte. <c>vTotDFe</c> e o
    /// analogo do <c>vNFTot</c>: "vTPrest + total do IBS + total da CBS".
    /// Sem nenhum dos dois o documento nao e da reforma e o quadro some.</para>
    /// </summary>
    private static TotaisIbsCbs? LerIbsCbs(XElement? imp)
    {
        XElement? grupo = imp.El("IBSCBS");
        decimal? total = imp.Dec("vTotDFe");

        if (grupo is null && total is null)
        {
            return null;
        }

        XElement? g = grupo.El("gIBSCBS");

        return new TotaisIbsCbs(
            BaseCalculo: g.Dec("vBC"),
            ValorIbs: g.Dec("vIBS"),
            ValorIbsUf: g.El("gIBSUF").Dec("vIBSUF"),
            ValorIbsMun: g.El("gIBSMun").Dec("vIBSMun"),
            ValorCbs: g.El("gCBS").Dec("vCBS"),
            ValorIs: null,
            ValorTotalNotaComTributos: total);
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
    /// Documentos originarios (infCTeNorm/infDoc). O grupo e de escolha: um
    /// CT-e cita notas em papel (infNF), NF-e, "outros" (infOutros) ou DC-e -
    /// um tipo so. No modal aquaviario o MOC permite omitir o quadro e
    /// detalhar os conteineres em seu lugar.
    ///
    /// <para>Cada documento sai nas colunas do manual do DACTE: TP DOC,
    /// CNPJ/CPF EMITENTE e SERIE/No DOCUMENTO. NF-e e DC-e trazem os tres na
    /// chave. Para papel e "outros" a correlacao do manual e explicita -
    /// "CNPJ/CPF Emitente: CNPJ/CPF, em rem" -, porque a nota da carga em
    /// papel e emitida por quem a despacha.</para>
    /// </summary>
    private static List<DocumentoReferenciado> LerDocumentos(XElement? infDoc, ParticipanteCte? remetente)
    {
        var lista = new List<DocumentoReferenciado>();
        string? documentoRemetente = remetente?.Documento;

        foreach (XElement n in infDoc.Els("infNFe"))
        {
            lista.Add(DocumentoReferenciado.Eletronico("NF-e", n.Str("chave")));
        }

        // Declaracao de Conteudo eletronica: a chave segue a mesma composicao
        // de toda chave de DF-e, entao emitente, serie e numero saem dela
        // como na NF-e.
        foreach (XElement n in infDoc.Els("infDCe"))
        {
            lista.Add(DocumentoReferenciado.Eletronico("DC-e", n.Str("chave")));
        }

        foreach (XElement n in infDoc.Els("infNF"))
        {
            lista.Add(new DocumentoReferenciado(
                n.Str("mod") == "04" ? "NF produtor" : "NF",
                documentoRemetente,
                n.Str("serie"),
                n.Str("nDoc"),
                DataEmissao: n.Data("dEmi")));
        }

        foreach (XElement n in infDoc.Els("infOutros"))
        {
            lista.Add(new DocumentoReferenciado(
                DescreverOutro(n.Str("tpDoc"), n.Str("descOutros")),
                documentoRemetente,
                null,
                n.Str("nDoc"),
                DataEmissao: n.Data("dEmi")));
        }

        return lista;
    }

    /// <summary>
    /// Tipo do documento em infOutros. No 99 ("outros") o leiaute manda
    /// descrever o documento em descOutros, e e essa descricao que o leitor
    /// reconhece.
    /// </summary>
    private static string DescreverOutro(string? tp, string? descricao) => tp switch
    {
        "00" => "Declaração",
        "10" => "Dutoviário",
        "59" => "CF-e SAT",
        "65" => "NFC-e",
        "99" => descricao ?? "Outros",
        _ => descricao ?? "Documento",
    };

    /// <summary>
    /// Documentos de transporte anterior (infCTeNorm/docAnt): na
    /// subcontratacao e no redespacho, o conhecimento de quem levou a carga
    /// antes. O manual do DACTE preve folhas adicionais para eles (2.1), ao
    /// lado dos documentos originarios.
    ///
    /// <para>O emitente vem uma vez por grupo emiDocAnt, e cada grupo cita
    /// documentos em papel (idDocAntPap) ou CT-e (idDocAntEle). O modelo
    /// guarda o mesmo agrupamento, para o DACTE nao repetir nome e CNPJ do
    /// transportador em cada linha.</para>
    /// </summary>
    private static List<EmissorDocumentoAnterior> LerDocumentosAnteriores(XElement? docAnt)
    {
        var lista = new List<EmissorDocumentoAnterior>();

        foreach (XElement emi in docAnt.Els("emiDocAnt"))
        {
            string? documento = emi.Str("CNPJ") ?? emi.Str("CPF");
            var documentos = new List<DocumentoReferenciado>();

            foreach (XElement id in emi.Els("idDocAnt"))
            {
                foreach (XElement p in id.Els("idDocAntPap"))
                {
                    string? serie = p.Str("serie");
                    string? subserie = p.Str("subser");

                    documentos.Add(new DocumentoReferenciado(
                        DescreverDocumentoAnterior(p.Str("tpDoc")),
                        documento,
                        subserie is null ? serie : $"{serie}-{subserie}",
                        p.Str("nDoc"),
                        DataEmissao: p.Data("dEmi")));
                }

                // chCTe desde o leiaute 3.00; "chave" no 2.00.
                foreach (XElement e in id.Els("idDocAntEle"))
                {
                    documentos.Add(DocumentoReferenciado.Eletronico(
                        "CT-e", e.Str("chCTe") ?? e.Str("chave"), documento));
                }
            }

            lista.Add(new EmissorDocumentoAnterior(
                emi.Str("xNome"), documento, emi.Str("IE"), emi.Str("UF"), documentos));
        }

        return lista;
    }

    private static string DescreverDocumentoAnterior(string? tp) => tp switch
    {
        "07" => "ATRE",
        "08" => "DTA",
        "09" => "Conh. aéreo internacional",
        "10" => "Carta de porte internacional",
        "11" => "Conhecimento avulso",
        "12" => "TIF",
        "13" => "BL",
        _ => tp ?? "Documento",
    };

    /// <summary>
    /// CT-e citados pelo tipo do documento: o complementado (infCteComp, fora
    /// de infCTeNorm, de 1 a 10 no leiaute 4.00), o substituido
    /// (infCteSub), o anulado (infCteAnu, so no 3.00) e os multimodais do
    /// servico vinculado (infServVinc). Sem eles, um CT-e de complemento
    /// chegava ao papel sem dizer o que complementa.
    /// </summary>
    private static ReferenciasCte LerReferencias(XElement inf, XElement? norm)
    {
        var complementados = new List<DocumentoReferenciado>();
        foreach (XElement c in inf.Els("infCteComp"))
        {
            // chCTe desde o leiaute 3.00; "chave" no 2.00.
            complementados.Add(DocumentoReferenciado.Eletronico("CT-e", c.Str("chCTe") ?? c.Str("chave")));
        }

        XElement? sub = norm.El("infCteSub");
        XElement? anu = inf.El("infCteAnu");

        var multimodais = new List<DocumentoReferenciado>();
        foreach (XElement m in norm.Desce("infServVinc").Els("infCTeMultimodal"))
        {
            multimodais.Add(DocumentoReferenciado.Eletronico("CT-e", m.Str("chCTeMultimodal")));
        }

        // O schema grafa chCte, com "e" minusculo, nos dois grupos abaixo - e
        // chCTe em todo o resto. Aceitar as duas grafias custa uma chamada.
        return new ReferenciasCte(
            complementados,
            sub is null ? null : DocumentoReferenciado.Eletronico("CT-e", sub.Str("chCte") ?? sub.Str("chCTe")),
            sub.Int("indAlteraToma") == 1,
            anu is null
                ? null
                : DocumentoReferenciado.Eletronico(
                    "CT-e", anu.Str("chCte") ?? anu.Str("chCTe"), dataEmissao: anu.Data("dEmi")),
            multimodais);
    }

    private static List<VeiculoNovo> LerVeiculosNovos(XElement? norm)
    {
        var lista = new List<VeiculoNovo>();
        foreach (XElement v in norm.Els("veicNovos"))
        {
            lista.Add(new VeiculoNovo(
                Chassi: v.Str("chassi"),
                CodigoCor: v.Str("cCor"),
                Cor: v.Str("xCor"),
                MarcaModelo: v.Str("cMod"),
                ValorUnitario: v.Dec("vUnit"),
                FreteUnitario: v.Dec("vFrete")));
        }

        return lista;
    }

    /// <summary>Cobranca do frete (infCTeNorm/cobr): fatura e duplicatas.</summary>
    private static CobrancaCte? LerCobranca(XElement? cobr)
    {
        if (cobr is null)
        {
            return null;
        }

        XElement? fat = cobr.El("fat");

        var duplicatas = new List<Duplicata>();
        foreach (XElement d in cobr.Els("dup"))
        {
            duplicatas.Add(new Duplicata(
                Numero: d.Str("nDup"),
                Vencimento: d.Data("dVenc"),
                Valor: d.Dec("vDup")));
        }

        return new CobrancaCte(
            fat is null
                ? null
                : new Fatura(fat.Str("nFat"), fat.Dec("vOrig"), fat.Dec("vDesc"), fat.Dec("vLiq")),
            duplicatas);
    }

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

    /// <summary>
    /// Informacoes especificas do modal. O grupo infModal traz um unico filho,
    /// com o schema do modal: rodo, aereo, aquav, ferrov, duto ou multimodal.
    /// Os campos lidos sao os da correlacao do manual do DACTE (secao 3).
    /// </summary>
    private static DetalheModal? LerModal(XElement? infModal)
    {
        XElement? g = infModal.FilhoUnico();

        return g?.Name.LocalName switch
        {
            "rodo" => new DetalheRodoviario(LerOrdensColeta(g)),
            "aereo" => LerAereo(g),
            "aquav" => LerAquaviario(g),
            "ferrov" => LerFerroviario(g),
            "duto" => LerDutoviario(g),
            "multimodal" => LerMultimodal(g),
            _ => null,
        };
    }

    private static List<OrdemColeta> LerOrdensColeta(XElement rodo)
    {
        var lista = new List<OrdemColeta>();
        foreach (XElement o in rodo.Els("occ"))
        {
            XElement? emi = o.El("emiOcc");

            lista.Add(new OrdemColeta(
                Serie: o.Str("serie"),
                Numero: o.Str("nOcc"),
                DataEmissao: o.Data("dEmi"),
                CnpjEmitente: emi.Str("CNPJ"),
                CodigoInterno: emi.Str("cInt"),
                InscricaoEstadual: emi.Str("IE"),
                Uf: emi.Str("UF"),
                Fone: emi.Str("fone")));
        }

        return lista;
    }

    private static DetalheAereo LerAereo(XElement a)
    {
        XElement? natureza = a.El("natCarga");
        XElement? tarifa = a.El("tarifa");

        var manuseio = new List<string>();
        foreach (XElement m in natureza.Els("cInfManu"))
        {
            if (m.Texto() is { } codigo)
            {
                manuseio.Add(DescreverManuseio(codigo));
            }
        }

        var perigosos = new List<ArtigoPerigoso>();
        foreach (XElement p in a.Els("peri"))
        {
            XElement? total = p.El("infTotAP");

            perigosos.Add(new ArtigoPerigoso(
                NumeroOnu: p.Str("nONU"),
                QuantidadeVolumes: p.Str("qTotEmb"),
                QuantidadeTotal: total.Dec("qTotProd"),
                Unidade: DescreverUnidadePerigoso(total.Str("uniAP"))));
        }

        return new DetalheAereo(
            NumeroMinuta: a.Str("nMinu"),
            NumeroOperacional: a.Str("nOCA"),
            DataPrevistaEntrega: a.Data("dPrevAereo"),
            Dimensao: natureza.Str("xDime"),
            InformacoesManuseio: manuseio,
            ClasseTarifa: Descrever(
                tarifa.Str("CL"), ("M", "Tarifa mínima"), ("G", "Tarifa geral"), ("E", "Tarifa específica")),
            CodigoTarifa: tarifa.Str("cTar"),
            ValorTarifa: tarifa.Dec("vTar"),
            ArtigosPerigosos: perigosos);
    }

    /// <summary>
    /// Informacoes de manuseio do modal aereo. O texto e o do schema, sem as
    /// instrucoes ao emitente ("especificar no campo observacoes...").
    /// </summary>
    private static string DescreverManuseio(string codigo) => Descrever(
        codigo,
        ("01", "Certificado do expedidor para embarque de animal vivo"),
        ("02", "Artigo perigoso conforme Declaração do Expedidor anexa"),
        ("03", "Somente em aeronave cargueira"),
        ("04", "Artigo perigoso - declaração do expedidor não requerida"),
        ("05", "Artigo perigoso em quantidade isenta"),
        ("06", "Gelo seco para refrigeração"),
        ("07", "Não restrito"),
        ("08", "Artigo perigoso em carga consolidada"),
        ("09", "Autorização da autoridade governamental anexa"),
        ("10", "Baterias de íons de lítio - Seção II da PI965 - CAO"),
        ("11", "Baterias de íons de lítio - Seção II da PI966"),
        ("12", "Baterias de íons de lítio - Seção II da PI967"),
        ("13", "Baterias de metal lítio - Seção II da PI968 - CAO"),
        ("14", "Baterias de metal lítio - Seção II da PI969"),
        ("15", "Baterias de metal lítio - Seção II da PI970"),
        ("99", "Outro"))!;

    private static string? DescreverUnidadePerigoso(string? c) => c switch
    {
        "1" => "KG",
        "2" => "KG G (quilograma bruto)",
        "3" => "LITROS",
        "4" => "TI (índice de transporte)",
        "5" => "UNIDADES",
        _ => c,
    };

    private static DetalheAquaviario LerAquaviario(XElement a)
    {
        var balsas = new List<string>();
        foreach (XElement b in a.Els("balsa"))
        {
            if (b.Str("xBalsa") is { } balsa)
            {
                balsas.Add(balsa);
            }
        }

        var conteineres = new List<Conteiner>();
        foreach (XElement c in a.Els("detCont"))
        {
            var lacres = new List<string>();
            foreach (XElement l in c.Els("lacre"))
            {
                if (l.Str("nLacre") is { } lacre)
                {
                    lacres.Add(lacre);
                }
            }

            XElement? docs = c.El("infDoc");
            var documentos = new List<DocumentoReferenciado>();

            foreach (XElement n in docs.Els("infNF"))
            {
                documentos.Add(new DocumentoReferenciado("NF", null, n.Str("serie"), n.Str("nDoc")));
            }

            foreach (XElement n in docs.Els("infNFe"))
            {
                documentos.Add(DocumentoReferenciado.Eletronico("NF-e", n.Str("chave")));
            }

            conteineres.Add(new Conteiner(c.Str("nCont"), lacres, documentos));
        }

        return new DetalheAquaviario(
            ValorBaseAfrmm: a.Dec("vPrest"),
            ValorAfrmm: a.Dec("vAFRMM"),
            Navio: a.Str("xNavio"),
            Balsas: balsas,
            NumeroViagem: a.Str("nViag"),
            Direcao: a.Str("direc") switch
            {
                "N" => "Norte",
                "L" => "Leste",
                "S" => "Sul",
                "O" => "Oeste",
                var d => d,
            },
            Irin: a.Str("irin"),
            TipoNavegacao: Descrever(a.Str("tpNav"), ("0", "Interior"), ("1", "Cabotagem")),
            Conteineres: conteineres);
    }

    private static DetalheFerroviario LerFerroviario(XElement f)
    {
        XElement? mutuo = f.El("trafMut");

        var ferrovias = new List<FerroviaEnvolvida>();
        foreach (XElement e in mutuo.Els("ferroEnv"))
        {
            ferrovias.Add(new FerroviaEnvolvida(e.Str("CNPJ"), e.Str("cInt"), e.Str("IE"), e.Str("xNome")));
        }

        (string, string)[] ferrovia = [("1", "Ferrovia de origem"), ("2", "Ferrovia de destino")];

        return new DetalheFerroviario(
            TipoTrafego: Descrever(
                f.Str("tpTraf"), ("0", "Próprio"), ("1", "Mútuo"), ("2", "Rodoferroviário"), ("3", "Rodoviário")),
            Fluxo: f.Str("fluxo"),
            ResponsavelFaturamento: Descrever(mutuo.Str("respFat"), ferrovia),
            FerroviaEmitente: Descrever(mutuo.Str("ferrEmi"), ferrovia),
            ValorFrete: mutuo.Dec("vFrete"),
            ChaveCteFerroviaOrigem: mutuo.Str("chCTeFerroOrigem"),
            Ferrovias: ferrovias);
    }

    private static DetalheDutoviario LerDutoviario(XElement d) => new(
        ValorTarifa: d.Dec("vTar"),
        DataInicio: d.Data("dIni"),
        DataFim: d.Data("dFim"),
        Classificacao: Descrever(d.Str("classDuto"), ("1", "Gasoduto"), ("2", "Mineroduto"), ("3", "Oleoduto")),
        TipoContratacao: Descrever(
            d.Str("tpContratacao"),
            ("0", "Ponto a ponto"),
            ("1", "Capacidade de entrada"),
            ("2", "Capacidade de saída")),
        PontoEntrada: d.Str("codPontoEntrada"),
        PontoSaida: d.Str("codPontoSaida"),
        Contrato: d.Str("nContrato"));

    private static DetalheMultimodal LerMultimodal(XElement m)
    {
        XElement? seguro = m.El("seg");
        XElement? seguradora = seguro.El("infSeg");

        return new DetalheMultimodal(
            Cotm: m.Str("COTM"),
            Negociavel: Descrever(m.Str("indNegociavel"), ("0", "Não negociável"), ("1", "Negociável")),
            Seguradora: seguradora.Str("xSeg"),
            CnpjSeguradora: seguradora.Str("CNPJ"),
            Apolice: seguro.Str("nApol"),
            Averbacao: seguro.Str("nAver"));
    }

    /// <summary>
    /// "1 - Mutuo" a partir de "1". Codigo fora da tabela sai como veio: o
    /// DACTE nao pode esconder o que esta no arquivo, nem inventar-lhe nome.
    /// </summary>
    private static string? Descrever(string? codigo, params (string Codigo, string Nome)[] tabela)
    {
        if (codigo is null)
        {
            return null;
        }

        foreach ((string c, string nome) in tabela)
        {
            if (c == codigo)
            {
                return $"{c} - {nome}";
            }
        }

        return codigo;
    }

    /// <summary>Dados complementares operacionais (grupo compl).</summary>
    private static ComplementoCte LerComplemento(XElement? compl)
    {
        XElement? fluxo = compl.El("fluxo");

        FluxoCarga? previsaoFluxo = null;
        if (fluxo is not null)
        {
            var passagens = new List<string>();
            foreach (XElement p in fluxo.Els("pass"))
            {
                if (p.Str("xPass") is { } passagem)
                {
                    passagens.Add(passagem);
                }
            }

            previsaoFluxo = new FluxoCarga(fluxo.Str("xOrig"), passagens, fluxo.Str("xDest"), fluxo.Str("xRota"));
        }

        return new ComplementoCte(
            CaracteristicaTransporte: compl.Str("xCaracAd"),
            CaracteristicaServico: compl.Str("xCaracSer"),
            Fluxo: previsaoFluxo,
            Entrega: LerEntrega(compl.El("Entrega")),
            CamposContribuinte: LerCamposLivres(compl, "ObsCont"),
            CamposFisco: LerCamposLivres(compl, "ObsFisco"));
    }

    /// <summary>
    /// Previsao de entrega (compl/Entrega): dois grupos de escolha, um para a
    /// data (semData, comData, noPeriodo) e outro para a hora (semHora,
    /// comHora, noInter), cada um com o tipo que diz como ler o valor.
    /// </summary>
    private static PrevisaoEntrega? LerEntrega(XElement? e)
    {
        if (e is null)
        {
            return null;
        }

        XElement? comData = e.El("comData");
        XElement? periodo = e.El("noPeriodo");
        XElement? data = e.El("semData") ?? comData ?? periodo;

        XElement? comHora = e.El("comHora");
        XElement? intervalo = e.El("noInter");
        XElement? hora = e.El("semHora") ?? comHora ?? intervalo;

        return new PrevisaoEntrega(
            TipoData: data.Int("tpPer"),
            DataProgramada: comData.Data("dProg"),
            DataInicial: periodo.Data("dIni"),
            DataFinal: periodo.Data("dFim"),
            TipoHora: hora.Int("tpHor"),
            HoraProgramada: comHora.Hora("hProg"),
            HoraInicial: intervalo.Hora("hIni"),
            HoraFinal: intervalo.Hora("hFim"));
    }

    /// <summary>ObsCont e ObsFisco: nome no atributo xCampo, conteudo em xTexto.</summary>
    private static List<CampoLivre> LerCamposLivres(XElement? compl, string nome)
    {
        var lista = new List<CampoLivre>();
        foreach (XElement o in compl.Els(nome))
        {
            lista.Add(new CampoLivre(o.Attr("xCampo"), o.Str("xTexto")));
        }

        return lista;
    }

    /// <summary>
    /// QR Code do DACTE (infCTeSupl/qrCodCTe), obrigatorio no documento
    /// auxiliar desde o leiaute 4.00 (manual do DACTE, 2.19.1, campo 3).
    ///
    /// <para>Pelo schema o grupo e irmao de infCte, dentro de CTe. Ele e
    /// procurado tambem dentro de infCte, porque um arquivo que o grave ali
    /// ainda traz o QR Code - e quem so le nao ganha nada recusando-o.</para>
    /// </summary>
    private static string? LerQrCode(XElement raiz, XElement inf) =>
        (DocumentSniffer.AcharInfo(raiz, "CTe", "infCTeSupl") ?? inf.El("infCTeSupl")).Str("qrCodCTe");

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
