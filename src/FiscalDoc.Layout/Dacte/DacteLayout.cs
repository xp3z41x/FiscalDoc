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
        TextoConsulta: "www.cte.fazenda.gov.br/portal");

    private static List<BlocoDoc> MontarBlocos(CteDocumento cte)
    {
        var b = new List<BlocoDoc>
        {
            new BlocoDoc.Linha(
            [
                new CampoDoc("NATUREZA DA OPERAÇÃO", cte.NaturezaOperacao, 2.2f),
                new CampoDoc("CFOP", cte.Cfop, 0.7f),
                new CampoDoc("TIPO DO SERVIÇO", RotulosCte.Servico(cte.Servico), 1.4f),
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

        // Participantes. Expedidor e recebedor so aparecem quando existem no
        // arquivo - o MOC 2.7 permite omitir os quadros ausentes.
        Participante(b, "Remetente", cte.Remetente);
        Participante(b, "Expedidor", cte.Expedidor);
        Participante(b, "Recebedor", cte.Recebedor);
        Participante(b, "Destinatário", cte.Destinatario);

        if (cte.Tomador is not null && cte.CodigoTomador == 4)
        {
            Participante(b, "Tomador do Serviço", cte.Tomador);
        }

        // Produto e carga.
        b.Add(new BlocoDoc.Secao("Informações da Carga"));
        b.Add(new BlocoDoc.Linha(
        [
            new CampoDoc("PRODUTO PREDOMINANTE", cte.ProdutoPredominante, 2.6f),
            new CampoDoc("VALOR TOTAL DA CARGA", Formatos.Moeda(cte.ValorCarga), 1.2f, AlinhamentoH.Direita),
            new CampoDoc("RNTRC", cte.Rntrc, 1.0f),
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

        // Componentes do valor da prestacao.
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

        // Imposto.
        b.Add(new BlocoDoc.Secao("Informações Relativas ao Imposto"));
        b.Add(new BlocoDoc.Linha(
        [
            new CampoDoc("SITUAÇÃO TRIBUTÁRIA", cte.Icms.Cst ?? cte.Icms.Motivo, 1.4f),
            new CampoDoc("BASE DE CÁLCULO", Formatos.Moeda(cte.Icms.BaseCalculo), 1.1f, AlinhamentoH.Direita),
            new CampoDoc("ALÍQUOTA", Formatos.Percentual(cte.Icms.Aliquota), 0.8f, AlinhamentoH.Direita),
            new CampoDoc("VALOR DO ICMS", Formatos.Moeda(cte.Icms.Valor), 1.1f, AlinhamentoH.Direita),
            new CampoDoc("% RED. BC", Formatos.Percentual(cte.Icms.PercentualReducaoBc), 0.8f, AlinhamentoH.Direita),
            new CampoDoc("TOTAL DE TRIBUTOS", Formatos.Moeda(cte.ValorTotalTributos), 1.1f, AlinhamentoH.Direita),
        ]));

        // Documentos originarios.
        b.Add(new BlocoDoc.Secao("Documentos Originários"));

        if (cte.DocumentosOriginarios.Count > 0)
        {
            var linhas = new List<string[]>(cte.DocumentosOriginarios.Count);
            foreach (DocumentoOriginario d in cte.DocumentosOriginarios)
            {
                linhas.Add(
                [
                    d.Tipo ?? string.Empty,
                    d.Chave is not null
                        ? ChaveAcesso.DeAtributoId(d.Chave)?.Formatada ?? d.Chave
                        : $"{d.Serie ?? string.Empty} / {d.Numero ?? string.Empty}",
                    Formatos.Moeda(d.Valor),
                ]);
            }

            b.Add(new BlocoDoc.Tabela(
            [
                new ColunaDoc("TIPO", 0.8f),
                new ColunaDoc("CHAVE DE ACESSO / NÚMERO", 5f),
                new ColunaDoc("VALOR", 1f, AlinhamentoH.Direita),
            ], linhas));
        }

        // Observacoes.
        b.Add(new BlocoDoc.Secao("Observações"));
        b.Add(new BlocoDoc.Texto("OBSERVAÇÕES GERAIS", MontarObservacoes(cte), 16f));

        return b;
    }

    private static void Participante(List<BlocoDoc> b, string papel, ParticipanteCte? p)
    {
        if (p is null)
        {
            return;
        }

        Endereco en = p.Endereco;

        b.Add(new BlocoDoc.Secao(papel));
        b.Add(new BlocoDoc.Linha(
        [
            new CampoDoc("NOME / RAZÃO SOCIAL", p.RazaoSocial, 2.4f),
            new CampoDoc("CNPJ / CPF", Formatos.CnpjOuCpf(p.Documento), 1.2f),
            new CampoDoc("INSCRIÇÃO ESTADUAL", p.InscricaoEstadual, 1.1f),
            new CampoDoc("FONE", Formatos.Telefone(p.Fone), 0.9f),
        ]));
        b.Add(new BlocoDoc.Linha(
        [
            new CampoDoc("ENDEREÇO", en.LinhaLogradouro, 2.4f),
            new CampoDoc("BAIRRO", en.Bairro, 1.0f),
            new CampoDoc("MUNICÍPIO", en.Municipio, 1.2f),
            new CampoDoc("UF", en.Uf, 0.4f, AlinhamentoH.Centro),
            new CampoDoc("CEP", Formatos.Cep(en.Cep), 0.6f),
        ]));
    }

    private static string Local(string? municipio, string? uf) =>
        string.IsNullOrWhiteSpace(municipio) ? uf ?? string.Empty : $"{municipio} - {uf}";

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
