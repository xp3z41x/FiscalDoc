using FiscalDoc.Core.Model.Cte;
using FiscalDoc.Core.Model.Nfe;
using FiscalDoc.Core.Values;
using FiscalDoc.Layout.Composition;
using FiscalDoc.Layout.DisplayList;

namespace FiscalDoc.Layout.Dacte;

/// <summary>
/// DACTE - representacao grafica do CT-e modelo 57.
///
/// <para><b>Aviso de precisao, que vale a pena ler.</b> Diferente do DANFE, o
/// MOC do CT-e 4.00 Anexo II <b>nao publica tabela de coordenadas</b>, nem
/// tamanho minimo de fonte, nem regra de margem: o layout e definido apenas
/// pelos desenhos das secoes 5 e 6, que sao imagens. Uma varredura do manual
/// inteiro por "fonte", "pontos", "Times", "negrito" e "margem" so encontra a
/// margem clara do codigo de barras e a do QR Code.</para>
///
/// <para>Logo, "DACTE conforme o MOC" nao e uma afirmacao verificavel do mesmo
/// modo que para o DANFE - nao ha contra o que verificar. O que este layout
/// garante e o que o criterio acordado pede: <b>todos os blocos do modelo
/// oficial, na ordem do modelo oficial, legiveis</b>. A geometria vem do
/// <see cref="MontadorDocumento"/>, e as convencoes tipograficas sao herdadas
/// do DANFE por analogia, por serem os unicos valores normativos do conjunto.</para>
///
/// <para>O que e <b>normativo</b> e o conteudo: a secao 3 do mesmo anexo,
/// "Correlacao dos campos do XML do CT-e x DACTE", diz qual tag vai em qual
/// quadro, e cada quadro abaixo cita a regra que o traz. Alem dela, quatro
/// blocos sao convencao da casa, pedidos pelo usuario e presentes no arquivo:
/// cobranca, previsao de entrega, caracteristicas adicionais e ordens de
/// coleta. O canhoto, que o manual torna opcional (2.21.5), fica de fora -
/// ele serve a quem entrega a carga, e quem abre o CT-e aqui e quem o recebe.</para>
///
/// <para>Orientacao: o MOC publica o DACTE em A5/A4 paisagem, que e como o
/// mercado imprime. tpImp = 1 no arquivo forca retrato.</para>
/// </summary>
public static class DacteLayout
{
    private const float MargemMm = 5.0f;

    public static ConjuntoPaginas Construir(
        CteDocumento cte, IMedidorTexto medidor, EstilosDanfe? estilos = null)
    {
        ArgumentNullException.ThrowIfNull(cte);
        ArgumentNullException.ThrowIfNull(medidor);

        EstilosDanfe e = estilos ?? new EstilosDanfe();
        TamanhoPapel papel = cte.Paisagem ? TamanhoPapel.A4.Girado : TamanhoPapel.A4;

        var m = new MontadorDocumento(papel, MargemMm, e, medidor)
        {
            AlturaLinha = 7.6f,
            AlturaSecao = 4.4f,
        };

        DadosCabecalho cab = MontarCabecalho(cte);

        return m.Montar(
            MontarBlocos(cte),
            (campo, pagina, total) =>
                CabecalhoFiscal.Desenhar(campo, m, e, cab, pagina, total));
    }

    private static DadosCabecalho MontarCabecalho(CteDocumento cte) => new(
        Sigla: "DACTE",
        Descricao: "Documento Auxiliar do Conhecimento de Transporte Eletrônico",
        Numero: cte.Numero,
        Serie: cte.Serie,
        ModalOuTipo: $"{RotulosCte.Modal(cte.Modal)} · {RotulosCte.Tipo(cte.Tipo)}",
        Chave: cte.Chave,
        RazaoSocialEmitente: cte.Emitente.RazaoSocial,
        EnderecoEmitente: cte.Emitente.Endereco,
        DocumentoEmitente: cte.Emitente.Documento,
        InscricaoEstadual: cte.Emitente.InscricaoEstadual,
        Protocolo: cte.Protocolo,
        ExigeSemValorFiscal: cte.ExigeSemValorFiscal,
        DizerContingencia: RotulosCte.DizerContingencia(cte.TipoEmissao)?.ToUpperInvariant(),
        TextoConsulta: "www.cte.fazenda.gov.br/portal",

        // Manual, 2.19.1: "campo 3: informara o QR Code".
        QrCode: CabecalhoFiscal.CodificarQr(cte.QrCode));

    private static List<BlocoDoc> MontarBlocos(CteDocumento cte)
    {
        ComplementoCte compl = cte.Complemento;

        var b = new List<BlocoDoc>
        {
            new BlocoDoc.Linha(
            [
                new CampoDoc("NATUREZA DA OPERAÇÃO", cte.NaturezaOperacao, 2.2f),
                new CampoDoc("CFOP", cte.Cfop, 0.6f),
                new CampoDoc("TIPO DO SERVIÇO", RotulosCte.Servico(cte.Servico), 1.5f),

                // "Indicador do CT-e Globalizado": o leiaute manda omitir a
                // tag quando nao e globalizado, entao a ausencia e o "nao".
                new CampoDoc("CT-E GLOBALIZADO", cte.Globalizado ? "Sim" : "Não", 0.8f, AlinhamentoH.Centro),
                new CampoDoc("DATA E HORA DE EMISSÃO", Formatos.DataHora(cte.DataHoraEmissao), 1.3f),
            ]),

            new BlocoDoc.Linha(
            [
                new CampoDoc("INÍCIO DA PRESTAÇÃO", Local(cte.MunicipioInicio, cte.UfInicio), 1.6f),
                new CampoDoc("TÉRMINO DA PRESTAÇÃO", Local(cte.MunicipioFim, cte.UfFim), 1.6f),
                new CampoDoc("MUNICÍPIO DE ENVIO", Local(cte.MunicipioEnvio, cte.UfEnvio), 1.6f),
                new CampoDoc("TOMADOR DO SERVIÇO", cte.NomeTomador, 1.2f, Destacado: true),
            ]),
        };

        CaracteristicasEEntrega(b, compl);

        if (!string.IsNullOrWhiteSpace(cte.InformacoesGlobalizado))
        {
            b.Add(new BlocoDoc.Texto("INFORMAÇÕES DO CT-E GLOBALIZADO", cte.InformacoesGlobalizado, 8f));
        }

        // Participantes, na ordem de leitura do modelo oficial: remetente e
        // destinatario, expedidor e recebedor, tomador. Expedidor e recebedor
        // so aparecem quando existem no arquivo - o manual (2.7) permite
        // omitir os quadros ausentes.
        Participante(b, "Remetente", cte.Remetente);
        Participante(b, "Destinatário", cte.Destinatario);
        Participante(b, "Expedidor", cte.Expedidor);
        Participante(b, "Recebedor", cte.Recebedor);

        // O quadro do tomador sai sempre: com os dados de toma4 ou, se o
        // tomador e um dos participantes, com os dados dele (manual, secao 3).
        // O titulo diz qual participante e - e o que o financeiro procura.
        if (cte.TomadorEfetivo is { } tomador)
        {
            string titulo = cte.CodigoTomador is >= 0 and <= 3
                ? $"Tomador do Serviço ({cte.NomeTomador})"
                : "Tomador do Serviço";

            Participante(b, titulo, tomador);
        }

        Carga(b, cte);
        Valores(b, cte);
        Cobranca(b, cte.Cobranca, cte.Paisagem);
        Imposto(b, cte);
        Documentos(b, cte);
        VeiculosNovos(b, cte.VeiculosNovos);
        Fluxo(b, compl.Fluxo);

        b.Add(new BlocoDoc.Secao("Observações"));
        b.Add(new BlocoDoc.Texto("OBSERVAÇÕES GERAIS", MontarObservacoes(cte), 16f));

        Modal(b, cte);

        // Manual, secao 3: "Uso Exclusivo do Emissor de CT-e: xTexto, em
        // ObsCont" e "Reservado ao Fisco: xTexto, em ObsFisco". Sem conteudo
        // no arquivo, o quadro nao aparece - numa tela nao ha fiscal para
        // carimbar o espaco em branco.
        if (compl.CamposContribuinte.Count > 0)
        {
            b.Add(new BlocoDoc.Texto(
                "USO EXCLUSIVO DO EMISSOR DO CT-E", Juntar(compl.CamposContribuinte), 8f));
        }

        if (compl.CamposFisco.Count > 0)
        {
            b.Add(new BlocoDoc.Texto("RESERVADO AO FISCO", Juntar(compl.CamposFisco), 8f));
        }

        return ComTituloNaContinuacao(b);
    }

    /// <summary>
    /// Toda tabela que se divide entre folhas reabre, na continuacao, com o
    /// titulo do quadro a que pertence - o ultimo titulo de secao antes dela.
    /// O modelo oficial faz isso com os documentos originarios (manual,
    /// secao 5.1, segunda folha); aqui vale para todos os quadros, porque uma
    /// tabela sem nome no alto de uma folha nao diz ao leitor o que ela e.
    /// </summary>
    private static List<BlocoDoc> ComTituloNaContinuacao(List<BlocoDoc> blocos)
    {
        string? titulo = null;

        for (int i = 0; i < blocos.Count; i++)
        {
            switch (blocos[i])
            {
                case BlocoDoc.Secao s:
                    titulo = s.Titulo;
                    break;

                case BlocoDoc.Tabela t when t.TituloNaContinuacao is null && titulo is not null:
                    blocos[i] = t with { TituloNaContinuacao = titulo };
                    break;
            }
        }

        return blocos;
    }

    /// <summary>
    /// Caracteristicas adicionais (compl/xCaracAd e xCaracSer) e previsao de
    /// entrega (compl/Entrega). Nenhum dos dois e quadro do modelo oficial:
    /// sao convencao da casa, pedidos pelo usuario, e so aparecem quando o
    /// arquivo os traz.
    /// </summary>
    private static void CaracteristicasEEntrega(List<BlocoDoc> b, ComplementoCte compl)
    {
        var campos = new List<CampoDoc>();

        if (!string.IsNullOrWhiteSpace(compl.CaracteristicaTransporte))
        {
            campos.Add(new CampoDoc("CARACTERÍSTICA ADICIONAL DO TRANSPORTE", compl.CaracteristicaTransporte, 1.5f));
        }

        if (!string.IsNullOrWhiteSpace(compl.CaracteristicaServico))
        {
            campos.Add(new CampoDoc("CARACTERÍSTICA ADICIONAL DO SERVIÇO", compl.CaracteristicaServico, 1.8f));
        }

        if (compl.Entrega is { } entrega)
        {
            campos.Add(new CampoDoc("PREVISÃO DE ENTREGA", entrega.DescricaoData, 1.4f));
            campos.Add(new CampoDoc("HORÁRIO PREVISTO", entrega.DescricaoHora, 1.2f));
        }

        if (campos.Count > 0)
        {
            b.Add(new BlocoDoc.Linha(campos));
        }
    }

    private static void Participante(List<BlocoDoc> b, string papel, ParticipanteCte? p)
    {
        if (p is null)
        {
            return;
        }

        Endereco en = p.Endereco;

        b.Add(new BlocoDoc.Secao(papel));

        var identificacao = new List<CampoDoc>
        {
            new("NOME / RAZÃO SOCIAL", p.RazaoSocial, 2.4f),
            new("CNPJ / CPF", Formatos.CnpjOuCpf(p.Documento), 1.2f),
            new("INSCRIÇÃO ESTADUAL", p.InscricaoEstadual, 1.1f),
        };

        // Manual, secao 3: "Inscricao SUFRAMA Destinatario: ISUF, de dest".
        if (!string.IsNullOrWhiteSpace(p.InscricaoSuframa))
        {
            identificacao.Add(new CampoDoc("INSCRIÇÃO SUFRAMA", p.InscricaoSuframa, 1.0f));
        }

        identificacao.Add(new CampoDoc("FONE", Formatos.Telefone(p.Fone), 0.9f));
        b.Add(new BlocoDoc.Linha(identificacao));

        var endereco = new List<CampoDoc>
        {
            new("ENDEREÇO", en.LinhaLogradouro, 2.4f),
            new("BAIRRO", en.Bairro, 1.0f),
            new("MUNICÍPIO", en.Municipio, 1.2f),
            new("UF", en.Uf, 0.4f, AlinhamentoH.Centro),
            new("CEP", Formatos.Cep(en.Cep), 0.6f),
        };

        // Manual, secao 3: "Pais: xPais" em todos os participantes. O campo e
        // opcional no leiaute, e so aparece quando o arquivo o traz.
        if (!string.IsNullOrWhiteSpace(en.Pais))
        {
            endereco.Add(new CampoDoc("PAÍS", en.Pais, 0.8f));
        }

        b.Add(new BlocoDoc.Linha(endereco));
    }

    /// <summary>
    /// Informacoes da carga. O CT-e de complemento nao tem infCTeNorm, e
    /// portanto nao tem carga: o quadro so aparece quando ha o que dizer.
    /// </summary>
    private static void Carga(List<BlocoDoc> b, CteDocumento cte)
    {
        bool temCarga = cte.ProdutoPredominante is not null
            || cte.OutrasCaracteristicasCarga is not null
            || cte.ValorCarga is not null
            || cte.Quantidades.Count > 0;

        if (!temCarga)
        {
            return;
        }

        b.Add(new BlocoDoc.Secao("Informações da Carga"));
        b.Add(new BlocoDoc.Linha(
        [
            new CampoDoc("PRODUTO PREDOMINANTE", cte.ProdutoPredominante, 2.2f),
            new CampoDoc("OUTRAS CARACTERÍSTICAS DA CARGA", cte.OutrasCaracteristicasCarga, 2.0f),
            new CampoDoc("VALOR TOTAL DA CARGA", Formatos.Moeda(cte.ValorCarga), 1.2f, AlinhamentoH.Direita),
        ]));

        if (cte.Quantidades.Count > 0)
        {
            var linhas = new List<string[]>(cte.Quantidades.Count);
            foreach (QuantidadeCarga q in cte.Quantidades)
            {
                linhas.Add([q.TipoMedida ?? string.Empty, q.Unidade ?? string.Empty,
                            Formatos.Quantidade(q.Quantidade)]);
            }

            b.Add(new BlocoDoc.Tabela(
            [
                new ColunaDoc("TIPO DE MEDIDA", 2f),
                new ColunaDoc("UNIDADE", 1f),
                new ColunaDoc("QUANTIDADE", 1f, AlinhamentoH.Direita),
            ], linhas));
        }
    }

    private static void Valores(List<BlocoDoc> b, CteDocumento cte)
    {
        b.Add(new BlocoDoc.Secao("Componentes do Valor da Prestação do Serviço"));

        if (cte.Componentes.Count > 0)
        {
            var linhas = new List<string[]>(cte.Componentes.Count);
            foreach (ComponenteValor comp in cte.Componentes)
            {
                linhas.Add([comp.Nome ?? string.Empty, Formatos.Moeda(comp.Valor)]);
            }

            b.Add(new BlocoDoc.Tabela(
            [
                new ColunaDoc("NOME DO COMPONENTE", 3f),
                new ColunaDoc("VALOR", 1f, AlinhamentoH.Direita),
            ], linhas));
        }

        b.Add(new BlocoDoc.Linha(
        [
            new CampoDoc("VALOR TOTAL DA PRESTAÇÃO", Formatos.Moeda(cte.ValorTotalPrestacao),
                1.5f, AlinhamentoH.Direita, Destacado: true),
            new CampoDoc("VALOR A RECEBER", Formatos.Moeda(cte.ValorReceber), 1.5f, AlinhamentoH.Direita),
        ]));
    }

    /// <summary>
    /// Fatura e duplicatas do frete (infCTeNorm/cobr). Nao e quadro do modelo
    /// oficial - e convencao da casa, pedida pelo usuario, no formato do quadro
    /// FATURA/DUPLICATAS do DANFE: e o que o financeiro paga, e quando.
    /// </summary>
    private static void Cobranca(List<BlocoDoc> b, CobrancaCte? cobranca, bool paisagem)
    {
        if (cobranca is null || (cobranca.Fatura is null && cobranca.Duplicatas.Count == 0))
        {
            return;
        }

        b.Add(new BlocoDoc.Secao("Fatura / Duplicatas"));

        if (cobranca.Fatura is { } f)
        {
            b.Add(new BlocoDoc.Linha(
            [
                new CampoDoc("NÚMERO DA FATURA", f.Numero, 1.6f),
                new CampoDoc("VALOR ORIGINAL", Formatos.Moeda(f.ValorOriginal), 1f, AlinhamentoH.Direita),
                new CampoDoc("VALOR DO DESCONTO", Formatos.Moeda(f.ValorDesconto), 1f, AlinhamentoH.Direita),
                new CampoDoc("VALOR LÍQUIDO", Formatos.Moeda(f.ValorLiquido), 1f, AlinhamentoH.Direita, Destacado: true),
            ]));
        }

        if (cobranca.Duplicatas.Count > 0)
        {
            var itens = new List<string[]>(cobranca.Duplicatas.Count);
            foreach (Duplicata d in cobranca.Duplicatas)
            {
                itens.Add([d.Numero ?? string.Empty, Formatos.Data(d.Vencimento), Formatos.Moeda(d.Valor)]);
            }

            b.Add(EmConjuntos(
            [
                new ColunaDoc("DUPLICATA", 1.4f),
                new ColunaDoc("VENCIMENTO", 1f, AlinhamentoH.Centro),
                new ColunaDoc("VALOR", 1f, AlinhamentoH.Direita),
            ], itens, paisagem ? 3 : 2));
        }
    }

    private static void Imposto(List<BlocoDoc> b, CteDocumento cte)
    {
        b.Add(new BlocoDoc.Secao("Informações Relativas ao Imposto"));
        b.Add(new BlocoDoc.Linha(
        [
            new CampoDoc("SITUAÇÃO TRIBUTÁRIA", cte.Icms.Descricao ?? cte.Icms.Cst, 2.6f),
            new CampoDoc("BASE DE CÁLCULO", Formatos.Moeda(cte.Icms.BaseCalculo), 1.1f, AlinhamentoH.Direita),
            new CampoDoc("ALÍQUOTA", Formatos.Percentual(cte.Icms.Aliquota), 0.7f, AlinhamentoH.Direita),
            new CampoDoc("VALOR DO ICMS", Formatos.Moeda(cte.Icms.Valor), 1.1f, AlinhamentoH.Direita),
            new CampoDoc("% RED. BC", Formatos.Percentual(cte.Icms.PercentualReducaoBc), 0.7f, AlinhamentoH.Direita),
            new CampoDoc("TOTAL DE TRIBUTOS", Formatos.Moeda(cte.ValorTotalTributos), 1.1f, AlinhamentoH.Direita),
        ]));

        IbsCbs(b, cte);
    }

    /// <summary>
    /// IBS e CBS (imp/IBSCBS). A NT 2025.001-RTC do CT-e nao diz uma palavra
    /// sobre o DACTE, entao isto e convencao da casa - a mesma do DANFE, com os
    /// mesmos rotulos (ver <c>DanfeRetrato.DesenharIbsCbs</c>, que explica de
    /// onde vem o "(+)"). O quadro some quando o arquivo nao traz o grupo.
    /// </summary>
    private static void IbsCbs(List<BlocoDoc> b, CteDocumento cte)
    {
        if (cte.IbsCbs is not { } t)
        {
            return;
        }

        var campos = new List<CampoDoc>
        {
            new("BASE DE CÁLCULO IBS/CBS", Formatos.Moeda(t.BaseCalculo), 1.5f, AlinhamentoH.Direita),
            new("IBS ESTADUAL", Formatos.Moeda(t.ValorIbsUf), 1f, AlinhamentoH.Direita),
            new("IBS MUNICIPAL", Formatos.Moeda(t.ValorIbsMun), 1f, AlinhamentoH.Direita),
            new("(+) IBS R$", Formatos.Moeda(t.ValorIbs), 1f, AlinhamentoH.Direita, Destacado: true),
            new("(+) CBS R$", Formatos.Moeda(t.ValorCbs), 1f, AlinhamentoH.Direita, Destacado: true),
        };

        // vTotDFe so interessa quando de fato diverge de vTPrest - repetir o
        // mesmo numero em dois campos seria ruido, como no DANFE.
        if (t.TotalDivergeDoValorDaNota(cte.ValorTotalPrestacao))
        {
            campos.Add(new CampoDoc(
                "TOTAL COM IBS/CBS", Formatos.Moeda(t.ValorTotalNotaComTributos), 1.4f,
                AlinhamentoH.Direita, Destacado: true));
        }

        b.Add(new BlocoDoc.Secao("Tributos da Reforma Tributária (LC 214/2025)"));
        b.Add(new BlocoDoc.Linha(campos));
    }

    /// <summary>
    /// Os documentos que o CT-e cita: os originarios (a nota da carga), os de
    /// transporte anterior e os CT-e referenciados pelo tipo do documento. Os
    /// quadros so aparecem quando ha documento - o CT-e de complemento, por
    /// exemplo, nao tem nota nenhuma, e sim o CT-e que complementa.
    /// </summary>
    private static void Documentos(List<BlocoDoc> b, CteDocumento cte)
    {
        if (cte.DocumentosOriginarios.Count > 0)
        {
            b.Add(new BlocoDoc.Secao("Documentos Originários"));
            b.Add(TabelaDocumentos(cte.DocumentosOriginarios, cte.Paisagem));
        }

        if (cte.DocumentosAnteriores.Count > 0)
        {
            b.Add(new BlocoDoc.Secao("Documentos de Transporte Anterior"));

            foreach (EmissorDocumentoAnterior emissor in cte.DocumentosAnteriores)
            {
                b.Add(new BlocoDoc.Linha(
                [
                    new CampoDoc("EMITENTE DO DOCUMENTO ANTERIOR", emissor.Nome, 3.0f),
                    new CampoDoc("CNPJ / CPF", Formatos.CnpjOuCpf(emissor.Documento), 1.3f),
                    new CampoDoc("INSCRIÇÃO ESTADUAL", emissor.InscricaoEstadual, 1.1f),
                    new CampoDoc("UF", emissor.Uf, 0.4f, AlinhamentoH.Centro),
                ],
                MantemComProximo: emissor.Documentos.Count > 0));

                if (emissor.Documentos.Count > 0)
                {
                    b.Add(TabelaDocumentosAnteriores(emissor.Documentos));
                }
            }
        }

        ReferenciasCte r = cte.Referencias;

        if (r.Complementados.Count > 0)
        {
            b.Add(new BlocoDoc.Secao("CT-e Complementado"));
            b.Add(TabelaDocumentos(r.Complementados, cte.Paisagem));
        }

        if (r.Substituido is { } substituido)
        {
            b.Add(new BlocoDoc.Secao(
                r.AlteraTomador ? "CT-e Substituído (alteração de tomador)" : "CT-e Substituído"));
            b.Add(TabelaDocumentos([substituido], cte.Paisagem));
        }

        if (r.Anulado is { } anulado)
        {
            b.Add(new BlocoDoc.Secao("CT-e Anulado"));
            b.Add(TabelaDocumentos([anulado], cte.Paisagem));

            // No leiaute 3.00, infCteAnu/dEmi e a data da declaracao do
            // tomador nao contribuinte - nao e a data do CT-e anulado.
            if (anulado.DataEmissao is not null)
            {
                b.Add(new BlocoDoc.Linha(
                [
                    new CampoDoc("DATA DA DECLARAÇÃO DO TOMADOR", Formatos.Data(anulado.DataEmissao), 1f),
                ]));
            }
        }

        if (r.Multimodais.Count > 0)
        {
            b.Add(new BlocoDoc.Secao("CT-e Multimodal Vinculado"));
            b.Add(TabelaDocumentos(r.Multimodais, cte.Paisagem));
        }
    }

    /// <summary>
    /// O quadro "Documentos Originarios" do manual do DACTE (secao 3): TP DOC,
    /// CNPJ/CPF EMITENTE e SERIE/No DOCUMENTO - mais a chave de acesso, que o
    /// usuario pediu para poder consultar a nota. Como no modelo oficial, dois
    /// documentos dividem a linha quando a largura comporta: sempre em
    /// paisagem e, sem chave, tambem no retrato.
    ///
    /// <para>O grupo infDoc e de escolha, entao um CT-e so cita um tipo: ou
    /// todos tem chave, ou nenhum tem. Sem chave, a coluna nao existe, e o
    /// tipo - onde vai a descricao dos "outros" - fica com a largura.</para>
    /// </summary>
    private static BlocoDoc.Tabela TabelaDocumentos(IReadOnlyList<DocumentoReferenciado> docs, bool paisagem)
    {
        bool comChave = docs.Any(d => d.ChaveInformada is not null);

        ColunaDoc[] conjunto = comChave
            ?
            [
                new ColunaDoc("TP DOC.", 1.3f),
                new ColunaDoc("CNPJ/CPF EMITENTE", 1.9f),
                new ColunaDoc("SÉRIE/Nº DOCUMENTO", 1.7f),
                new ColunaDoc("CHAVE DE ACESSO", 5.5f),
            ]
            :
            [
                new ColunaDoc("TP DOC.", 2.4f),
                new ColunaDoc("CNPJ/CPF EMITENTE", 1.9f),
                new ColunaDoc("SÉRIE/Nº DOCUMENTO", 1.7f),
            ];

        var itens = new List<string[]>(docs.Count);
        foreach (DocumentoReferenciado d in docs)
        {
            string[] celulas =
            [
                d.Tipo,
                Formatos.CnpjOuCpf(d.DocumentoEmitente),
                SerieNumero(d),
            ];

            itens.Add(comChave
                ? [.. celulas, d.Chave?.Formatada ?? d.ChaveInformada ?? string.Empty]
                : celulas);
        }

        return EmConjuntos(conjunto, itens, paisagem || !comChave ? 2 : 1);
    }

    /// <summary>
    /// Documentos de um transportador anterior. O emitente ja saiu na linha
    /// de cima; aqui vai so o que muda de um documento para o outro.
    /// </summary>
    private static BlocoDoc.Tabela TabelaDocumentosAnteriores(IReadOnlyList<DocumentoReferenciado> docs)
    {
        var linhas = new List<string[]>(docs.Count);
        foreach (DocumentoReferenciado d in docs)
        {
            linhas.Add(
            [
                d.Tipo,
                SerieNumero(d),
                Formatos.Data(d.DataEmissao),
                d.Chave?.Formatada ?? d.ChaveInformada ?? string.Empty,
            ]);
        }

        return new BlocoDoc.Tabela(
        [
            new ColunaDoc("TP DOC.", 2.2f),
            new ColunaDoc("SÉRIE/Nº DOCUMENTO", 1.7f),
            new ColunaDoc("DATA DE EMISSÃO", 1.2f, AlinhamentoH.Centro),
            new ColunaDoc("CHAVE DE ACESSO", 5.5f),
        ], linhas);
    }

    /// <summary>
    /// Repete o conjunto de colunas <paramref name="porLinha"/> vezes e reparte
    /// os itens em linhas: o "dois documentos por linha" do modelo oficial.
    /// Com um item so, um conjunto so - um quadro de um CT-e complementado nao
    /// precisa de metade vazia.
    /// </summary>
    private static BlocoDoc.Tabela EmConjuntos(
        ColunaDoc[] conjunto, IReadOnlyList<string[]> itens, int porLinha)
    {
        porLinha = Math.Clamp(Math.Min(porLinha, itens.Count), 1, int.MaxValue);

        var colunas = new List<ColunaDoc>(conjunto.Length * porLinha);
        for (int i = 0; i < porLinha; i++)
        {
            colunas.AddRange(conjunto);
        }

        var linhas = new List<string[]>((itens.Count + porLinha - 1) / porLinha);
        for (int i = 0; i < itens.Count; i += porLinha)
        {
            var linha = new List<string>(colunas.Count);
            for (int j = 0; j < porLinha; j++)
            {
                linha.AddRange(i + j < itens.Count
                    ? itens[i + j]
                    : Enumerable.Repeat(string.Empty, conjunto.Length));
            }

            linhas.Add([.. linha]);
        }

        return new BlocoDoc.Tabela(colunas, linhas);
    }

    /// <summary>
    /// "1 / 000.294.000". Serie e numero de nota eletronica tem so digitos e
    /// saem no formato do DANFE; documento em papel pode trazer serie "U" ou
    /// numero de mais de nove digitos, e esses saem como vieram - agrupar em
    /// milhares um numero de vinte digitos cortaria o numero.
    /// </summary>
    private static string SerieNumero(DocumentoReferenciado d)
    {
        string serie = d.Serie is null ? string.Empty
            : SoDigitos(d.Serie) ? Formatos.Serie(d.Serie) : d.Serie;

        string numero = d.Numero is null ? string.Empty
            : SoDigitos(d.Numero) && d.Numero.Length <= 9 ? Formatos.NumeroNota(d.Numero) : d.Numero;

        if (serie.Length == 0)
        {
            return numero;
        }

        return numero.Length == 0 ? serie : $"{serie} / {numero}";
    }

    private static bool SoDigitos(string s) => s.Length > 0 && s.All(char.IsAsciiDigit);

    /// <summary>
    /// Veiculos novos transportados: manual, 2.21.4 - "No caso de veiculos
    /// novos transportados, informar com os campos, conforme exemplo".
    /// </summary>
    private static void VeiculosNovos(List<BlocoDoc> b, IReadOnlyList<VeiculoNovo> veiculos)
    {
        if (veiculos.Count == 0)
        {
            return;
        }

        var linhas = new List<string[]>(veiculos.Count);
        foreach (VeiculoNovo v in veiculos)
        {
            linhas.Add(
            [
                v.Chassi ?? string.Empty,
                Juntar(v.CodigoCor, v.Cor),
                v.MarcaModelo ?? string.Empty,
                Formatos.Moeda(v.ValorUnitario),
                Formatos.Moeda(v.FreteUnitario),
            ]);
        }

        b.Add(new BlocoDoc.Secao("Informações sobre os Veículos Novos Transportados"));
        b.Add(new BlocoDoc.Tabela(
        [
            new ColunaDoc("CHASSI", 2f),
            new ColunaDoc("COR", 2.2f),
            new ColunaDoc("MARCA/MODELO", 1.2f),
            new ColunaDoc("VR. UNIT. DO VEÍCULO", 1.2f, AlinhamentoH.Direita),
            new ColunaDoc("FRETE UNITÁRIO", 1.2f, AlinhamentoH.Direita),
        ], linhas));
    }

    /// <summary>
    /// Previsao do fluxo da carga (compl/fluxo): siglas ou codigos internos
    /// da filial, porto, estacao ou aeroporto de origem, passagem e destino
    /// (manual, secao 3).
    /// </summary>
    private static void Fluxo(List<BlocoDoc> b, FluxoCarga? f)
    {
        if (f is null)
        {
            return;
        }

        var campos = new List<CampoDoc>
        {
            new("ORIGEM", f.Origem, 1f),
            new("PASSAGEM", string.Join(", ", f.Passagens), 2f),
            new("DESTINO", f.Destino, 1f),
        };

        if (!string.IsNullOrWhiteSpace(f.Rota))
        {
            campos.Add(new CampoDoc("ROTA DE ENTREGA", f.Rota, 1f));
        }

        b.Add(new BlocoDoc.Secao("Previsão do Fluxo da Carga"));
        b.Add(new BlocoDoc.Linha(campos));
    }

    /// <summary>
    /// "Informacoes especificas do modal": o quadro que muda com o modal, com
    /// os campos da correlacao do manual (secao 3) e, no rodoviario, as ordens
    /// de coleta que o usuario pediu.
    /// </summary>
    private static void Modal(List<BlocoDoc> b, CteDocumento cte)
    {
        switch (cte.DetalheModal)
        {
            case DetalheAereo a:
                ModalAereo(b, cte, a);
                break;

            case DetalheAquaviario a:
                ModalAquaviario(b, cte, a);
                break;

            case DetalheFerroviario f:
                ModalFerroviario(b, cte, f);
                break;

            case DetalheDutoviario d:
                ModalDutoviario(b, cte, d);
                break;

            case DetalheMultimodal mm:
                ModalMultimodal(b, cte, mm);
                break;

            default:
                ModalRodoviario(b, cte, cte.DetalheModal as DetalheRodoviario);
                break;
        }

        // xDetRetira e de ide, e nao do modal: vale para qualquer um em que o
        // recebedor retire a carga. No aereo ele ja saiu no proprio quadro.
        if (cte.DetalheModal is not DetalheAereo && !string.IsNullOrWhiteSpace(cte.DetalhesRetirada))
        {
            b.Add(new BlocoDoc.Texto("DADOS RELATIVOS À RETIRADA DA CARGA", cte.DetalhesRetirada, 8f));
        }
    }

    /// <summary>
    /// Rodoviario: "RNTRC da Empresa: RNTRC, em rodo". Um CT-e sem grupo de
    /// modal cai aqui tambem, porque o rodoviario e o modal da quase
    /// totalidade dos conhecimentos - e o RNTRC, se houver, nao pode sumir.
    /// </summary>
    private static void ModalRodoviario(List<BlocoDoc> b, CteDocumento cte, DetalheRodoviario? r)
    {
        if (r is null && cte.Rntrc is null)
        {
            return;
        }

        b.Add(new BlocoDoc.Secao("Informações Específicas do Modal Rodoviário"));
        b.Add(new BlocoDoc.Linha([new CampoDoc("RNTRC DA EMPRESA", cte.Rntrc, 1f)]));

        if (r is null || r.OrdensColeta.Count == 0)
        {
            return;
        }

        var linhas = new List<string[]>(r.OrdensColeta.Count);
        foreach (OrdemColeta o in r.OrdensColeta)
        {
            linhas.Add(
            [
                o.Serie ?? string.Empty,
                o.Numero ?? string.Empty,
                Formatos.Data(o.DataEmissao),
                Formatos.Cnpj(o.CnpjEmitente),
                o.InscricaoEstadual ?? string.Empty,
                o.Uf ?? string.Empty,
                Formatos.Telefone(o.Fone),
                o.CodigoInterno ?? string.Empty,
            ]);
        }

        b.Add(new BlocoDoc.Secao("Ordens de Coleta Associadas"));
        b.Add(new BlocoDoc.Tabela(
        [
            new ColunaDoc("SÉRIE", 0.6f, AlinhamentoH.Centro),
            new ColunaDoc("NÚMERO", 1f),
            new ColunaDoc("DATA DE EMISSÃO", 1f, AlinhamentoH.Centro),
            new ColunaDoc("CNPJ DO EMITENTE", 1.5f),
            new ColunaDoc("INSCRIÇÃO ESTADUAL", 1.2f),
            new ColunaDoc("UF", 0.4f, AlinhamentoH.Centro),
            new ColunaDoc("FONE", 1f),
            new ColunaDoc("CÓD. INTERNO", 1f),
        ], linhas));
    }

    private static void ModalAereo(List<BlocoDoc> b, CteDocumento cte, DetalheAereo a)
    {
        b.Add(new BlocoDoc.Secao("Informações Específicas do Modal Aéreo"));
        b.Add(new BlocoDoc.Linha(
        [
            new CampoDoc("NÚMERO OPERACIONAL DO CONHECIMENTO AÉREO", a.NumeroOperacional, 2.2f),
            new CampoDoc("NÚMERO DA MINUTA", a.NumeroMinuta, 1.2f),
            new CampoDoc("DATA PREVISTA DE ENTREGA", Formatos.Data(a.DataPrevistaEntrega), 1.3f),
            new CampoDoc("RETIRA", cte.RecebedorRetira ? "Sim" : "Não", 0.6f, AlinhamentoH.Centro),
        ]));

        b.Add(new BlocoDoc.Linha(
        [
            new CampoDoc("CLASSE", a.ClasseTarifa, 1.2f),
            new CampoDoc("CÓDIGO DA TARIFA", a.CodigoTarifa, 1f),
            new CampoDoc("VALOR DA TARIFA", Formatos.Moeda(a.ValorTarifa), 1f, AlinhamentoH.Direita),
            new CampoDoc("DIMENSÃO", a.Dimensao, 2f),
        ]));

        if (!string.IsNullOrWhiteSpace(cte.DetalhesRetirada))
        {
            b.Add(new BlocoDoc.Texto("DADOS RELATIVOS À RETIRADA DA CARGA", cte.DetalhesRetirada, 8f));
        }

        if (a.InformacoesManuseio.Count > 0)
        {
            b.Add(new BlocoDoc.Texto(
                "INFORMAÇÕES DE MANUSEIO", string.Join('\n', a.InformacoesManuseio), 8f));
        }

        // Manual, 2.21.4: "No caso de produto perigoso, informar com os
        // campos, conforme exemplo".
        if (a.ArtigosPerigosos.Count > 0)
        {
            var linhas = new List<string[]>(a.ArtigosPerigosos.Count);
            foreach (ArtigoPerigoso p in a.ArtigosPerigosos)
            {
                linhas.Add(
                [
                    p.NumeroOnu ?? string.Empty,
                    p.QuantidadeVolumes ?? string.Empty,
                    Formatos.Quantidade(p.QuantidadeTotal),
                    p.Unidade ?? string.Empty,
                ]);
            }

            b.Add(new BlocoDoc.Secao("Informações sobre os Artigos Perigosos"));
            b.Add(new BlocoDoc.Tabela(
            [
                new ColunaDoc("NÚMERO ONU/UN", 1f),
                new ColunaDoc("QUANTIDADE TOTAL DE VOLUMES CONTENDO ARTIGOS PERIGOSOS", 2.4f),
                new ColunaDoc("QUANTIDADE TOTAL DE ARTIGOS PERIGOSOS", 1.8f, AlinhamentoH.Direita),
                new ColunaDoc("UNIDADE DE MEDIDA", 1.2f),
            ], linhas));
        }

        Rntrc(b, cte);
    }

    private static void ModalAquaviario(List<BlocoDoc> b, CteDocumento cte, DetalheAquaviario a)
    {
        b.Add(new BlocoDoc.Secao("Informações Específicas do Modal Aquaviário"));
        b.Add(new BlocoDoc.Linha(
        [
            new CampoDoc("IDENTIFICAÇÃO DO NAVIO/REBOCADOR", a.Navio, 2f),
            new CampoDoc("Nº DA VIAGEM", a.NumeroViagem, 0.9f),
            new CampoDoc("DIREÇÃO", a.Direcao, 0.7f),
            new CampoDoc("TIPO DE NAVEGAÇÃO", a.TipoNavegacao, 1.1f),
            new CampoDoc("IRIN", a.Irin, 0.9f),
        ]));

        b.Add(new BlocoDoc.Linha(
        [
            new CampoDoc("BASE DE CÁLCULO DO AFRMM", Formatos.Moeda(a.ValorBaseAfrmm), 1.2f, AlinhamentoH.Direita),
            new CampoDoc("VALOR DO AFRMM", Formatos.Moeda(a.ValorAfrmm), 1f, AlinhamentoH.Direita),
            new CampoDoc("IDENTIFICAÇÃO DA BALSA", string.Join(", ", a.Balsas), 3.4f),
        ]));

        // Manual, 2.21.4: no aquaviario o quadro de documentos pode ser
        // trocado pelo detalhamento dos conteineres - lacres e documentos de
        // cada um.
        if (a.Conteineres.Count > 0)
        {
            var linhas = new List<string[]>(a.Conteineres.Count);
            foreach (Conteiner c in a.Conteineres)
            {
                var documentos = new List<string>(c.Documentos.Count);
                foreach (DocumentoReferenciado d in c.Documentos)
                {
                    documentos.Add($"{d.Tipo} {SerieNumero(d)}");
                }

                linhas.Add([c.Numero ?? string.Empty, string.Join(", ", c.Lacres), string.Join(", ", documentos)]);
            }

            b.Add(new BlocoDoc.Secao("Contêineres"));
            b.Add(new BlocoDoc.Tabela(
            [
                new ColunaDoc("NRO. CONTÊINER", 1.2f),
                new ColunaDoc("NRO. LACRE", 1.6f),
                new ColunaDoc("DOCUMENTOS", 4f),
            ], linhas));
        }

        Rntrc(b, cte);
    }

    private static void ModalFerroviario(List<BlocoDoc> b, CteDocumento cte, DetalheFerroviario f)
    {
        b.Add(new BlocoDoc.Secao("Informações Específicas do Modal Ferroviário"));
        b.Add(new BlocoDoc.Linha(
        [
            new CampoDoc("TIPO DE TRÁFEGO", f.TipoTrafego, 1.2f),
            new CampoDoc("FLUXO FERROVIÁRIO", f.Fluxo, 1.2f),
            new CampoDoc("RESPONSÁVEL PELO FATURAMENTO", f.ResponsavelFaturamento, 1.5f),
            new CampoDoc("FERROVIA EMITENTE DO CT-E", f.FerroviaEmitente, 1.4f),
            new CampoDoc("VALOR DO FRETE", Formatos.Moeda(f.ValorFrete), 1f, AlinhamentoH.Direita),
        ]));

        if (!string.IsNullOrWhiteSpace(f.ChaveCteFerroviaOrigem))
        {
            b.Add(new BlocoDoc.Linha(
            [
                new CampoDoc(
                    "CHAVE DO CT-E DA FERROVIA DE ORIGEM",
                    ChaveAcesso.DeAtributoId(f.ChaveCteFerroviaOrigem)?.Formatada ?? f.ChaveCteFerroviaOrigem,
                    1f),
            ]));
        }

        if (f.Ferrovias.Count > 0)
        {
            var linhas = new List<string[]>(f.Ferrovias.Count);
            foreach (FerroviaEnvolvida fe in f.Ferrovias)
            {
                linhas.Add(
                [
                    Formatos.Cnpj(fe.Cnpj),
                    fe.InscricaoEstadual ?? string.Empty,
                    fe.CodigoInterno ?? string.Empty,
                    fe.RazaoSocial ?? string.Empty,
                ]);
            }

            b.Add(new BlocoDoc.Secao("Informações das Ferrovias Envolvidas"));
            b.Add(new BlocoDoc.Tabela(
            [
                new ColunaDoc("CNPJ", 1.3f),
                new ColunaDoc("INSCRIÇÃO ESTADUAL", 1.1f),
                new ColunaDoc("CÓD. INTERNO", 0.9f),
                new ColunaDoc("RAZÃO SOCIAL", 3.5f),
            ], linhas));
        }

        Rntrc(b, cte);
    }

    private static void ModalDutoviario(List<BlocoDoc> b, CteDocumento cte, DetalheDutoviario d)
    {
        b.Add(new BlocoDoc.Secao("Informações Específicas do Modal Dutoviário"));
        b.Add(new BlocoDoc.Linha(
        [
            // vTar tem seis casas no schema (TDec_0906Opc): arredondar para
            // duas imprimiria um numero que nao esta no arquivo.
            new CampoDoc("VALOR DA TARIFA", Formatos.ValorUnitario(d.ValorTarifa), 1.1f, AlinhamentoH.Direita),
            new CampoDoc("INÍCIO DA PRESTAÇÃO", Formatos.Data(d.DataInicio), 1f, AlinhamentoH.Centro),
            new CampoDoc("FIM DA PRESTAÇÃO", Formatos.Data(d.DataFim), 1f, AlinhamentoH.Centro),
            new CampoDoc("CLASSIFICAÇÃO", d.Classificacao, 1f),
            new CampoDoc("TIPO DE CONTRATAÇÃO", d.TipoContratacao, 1.4f),
        ]));

        if (d.PontoEntrada is not null || d.PontoSaida is not null || d.Contrato is not null)
        {
            b.Add(new BlocoDoc.Linha(
            [
                new CampoDoc("CÓDIGO DO PONTO DE ENTRADA", d.PontoEntrada, 1f),
                new CampoDoc("CÓDIGO DO PONTO DE SAÍDA", d.PontoSaida, 1f),
                new CampoDoc("NÚMERO DO CONTRATO", d.Contrato, 1f),
            ]));
        }

        Rntrc(b, cte);
    }

    private static void ModalMultimodal(List<BlocoDoc> b, CteDocumento cte, DetalheMultimodal mm)
    {
        b.Add(new BlocoDoc.Secao("Informações Específicas do Transporte Multimodal de Cargas"));
        b.Add(new BlocoDoc.Linha(
        [
            new CampoDoc("Nº DO CERTIFICADO DO OPERADOR DE TRANSPORTE MULTIMODAL", mm.Cotm, 2f),
            new CampoDoc("INDICADOR NEGOCIÁVEL", mm.Negociavel, 1f),
        ]));

        if (mm.Seguradora is not null || mm.Apolice is not null || mm.Averbacao is not null)
        {
            b.Add(new BlocoDoc.Secao("Informações de Seguro do Multimodal"));
            b.Add(new BlocoDoc.Linha(
            [
                new CampoDoc("NOME DA SEGURADORA", mm.Seguradora, 2f),
                new CampoDoc("CNPJ DA SEGURADORA", Formatos.Cnpj(mm.CnpjSeguradora), 1.2f),
                new CampoDoc("NÚMERO DA APÓLICE", mm.Apolice, 1.2f),
                new CampoDoc("NÚMERO DA AVERBAÇÃO", mm.Averbacao, 1.2f),
            ]));
        }

        Rntrc(b, cte);
    }

    /// <summary>
    /// RNTRC fora do rodoviario. O leiaute atual so o poe em rodo, mas o
    /// leitor o procura em qualquer modal - e o que foi achado nao pode
    /// deixar de sair.
    /// </summary>
    private static void Rntrc(List<BlocoDoc> b, CteDocumento cte)
    {
        if (!string.IsNullOrWhiteSpace(cte.Rntrc))
        {
            b.Add(new BlocoDoc.Linha([new CampoDoc("RNTRC", cte.Rntrc, 1f)]));
        }
    }

    private static string Local(string? municipio, string? uf) =>
        string.IsNullOrWhiteSpace(municipio) ? uf ?? string.Empty : $"{municipio} - {uf}";

    private static string Juntar(string? codigo, string? nome)
    {
        if (string.IsNullOrWhiteSpace(codigo))
        {
            return nome ?? string.Empty;
        }

        return string.IsNullOrWhiteSpace(nome) ? codigo : $"{codigo} - {nome}";
    }

    private static string Juntar(IReadOnlyList<CampoLivre> campos) =>
        string.Join('\n', campos.Select(c => c.Linha));

    private static string MontarObservacoes(CteDocumento cte)
    {
        var partes = new List<string>();

        if (Rotulos.ExigeJustificativaContingencia(cte.TipoEmissao))
        {
            if (cte.DataHoraContingencia is { } dh)
            {
                partes.Add($"Início da contingência: {Formatos.DataHora(dh)}");
            }

            if (!string.IsNullOrWhiteSpace(cte.JustificativaContingencia))
            {
                partes.Add($"Motivo: {cte.JustificativaContingencia}");
            }
        }

        if (!string.IsNullOrWhiteSpace(cte.ObservacoesFisco))
        {
            partes.Add(cte.ObservacoesFisco!);
        }

        if (!string.IsNullOrWhiteSpace(cte.Observacoes))
        {
            partes.Add(cte.Observacoes!);
        }

        return string.Join('\n', partes);
    }
}
