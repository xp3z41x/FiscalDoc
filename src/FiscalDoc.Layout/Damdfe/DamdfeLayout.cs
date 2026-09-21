using FiscalDoc.Core.Model.Mdfe;
using FiscalDoc.Core.Values;
using FiscalDoc.Layout.Composition;
using FiscalDoc.Layout.DisplayList;

namespace FiscalDoc.Layout.Damdfe;

/// <summary>
/// DAMDFE - representacao grafica do MDF-e modelo 58.
///
/// <para>Vale o mesmo aviso do DACTE: o MOC do MDF-e 3.00b Anexo II <b>nao
/// traz coordenadas, fontes nem margens</b> - a secao 2.7.1 diz literalmente
/// so que se usa "papel comum nas orientacoes retrato ou paisagem", e os
/// modelos da secao 2.8 sao imagens. Este layout entrega os blocos do modelo
/// oficial, na ordem dele, legiveis.</para>
///
/// <para>Duas regras do MOC que <b>sao</b> normativas e estao implementadas:
/// 2.5 obriga "EMISSÃO EM CONTINGÊNCIA" em destaque no lugar do protocolo, e
/// 2.6 obriga a frase de homologacao centralizada e em caixa alta na mesma
/// area. Ambas ficam em <see cref="CabecalhoFiscal"/>.</para>
///
/// <para>Retrato por padrao: a lista de documentos vinculados e vertical e
/// longa, e e o que o motorista precisa conferir na estrada.</para>
/// </summary>
public static class DamdfeLayout
{
    private const float MargemMm = 6.0f;

    public static ConjuntoPaginas Construir(
        MdfeDocumento mdfe, IMedidorTexto medidor, EstilosDanfe? estilos = null)
    {
        ArgumentNullException.ThrowIfNull(mdfe);
        ArgumentNullException.ThrowIfNull(medidor);

        EstilosDanfe e = estilos ?? new EstilosDanfe();
        TamanhoPapel papel = mdfe.Paisagem ? TamanhoPapel.A4.Girado : TamanhoPapel.A4;

        var m = new MontadorDocumento(papel, MargemMm, e, medidor)
        {
            AlturaLinha = 7.8f,
            AlturaSecao = 4.4f,
            AlturaLinhaTabela = 4.6f,
        };

        DadosCabecalho cab = new(
            Sigla: "DAMDFE",
            Descricao: "Documento Auxiliar do Manifesto Eletrônico de Documentos Fiscais",
            Numero: mdfe.Numero,
            Serie: mdfe.Serie,
            ModalOuTipo: RotulosMdfe.Modal(mdfe.Modal),
            Chave: mdfe.Chave,
            RazaoSocialEmitente: mdfe.Emitente.RazaoSocial,
            EnderecoEmitente: mdfe.Emitente.Endereco,
            DocumentoEmitente: mdfe.Emitente.Documento,
            InscricaoEstadual: mdfe.Emitente.InscricaoEstadual,
            Protocolo: mdfe.Protocolo,
            ExigeSemValorFiscal: mdfe.ExigeSemValorFiscal,

            // MOC MDF-e 2.5: texto proprio, em destaque, no lugar do protocolo.
            DizerContingencia: mdfe.EmContingencia ? "EMISSÃO EM CONTINGÊNCIA" : null,
            TextoConsulta: "www.mdfe-portal.sefaz.rs.gov.br");

        return m.Montar(
            MontarBlocos(mdfe),
            (campo, pagina, total) =>
                CabecalhoFiscal.Desenhar(campo, m, e, cab, pagina, total));
    }

    private static List<BlocoDoc> MontarBlocos(MdfeDocumento mdfe)
    {
        var b = new List<BlocoDoc>
        {
            new BlocoDoc.Linha(
            [
                new CampoDoc("EMITENTE", RotulosMdfe.Emitente(mdfe.TipoEmitente), 2.4f),
                new CampoDoc("UF DE INÍCIO", mdfe.UfInicio, 0.7f, AlinhamentoH.Centro),
                new CampoDoc("UF DE FIM", mdfe.UfFim, 0.7f, AlinhamentoH.Centro),
                new CampoDoc("DATA E HORA DE EMISSÃO", Formatos.DataHora(mdfe.DataHoraEmissao), 1.4f),
                new CampoDoc("INÍCIO DA VIAGEM", Formatos.DataHora(mdfe.DataHoraInicioViagem), 1.4f),
            ]),

            new BlocoDoc.Linha(
            [
                new CampoDoc("MUNICÍPIOS DE CARREGAMENTO",
                    string.Join(", ", mdfe.MunicipiosCarregamento), 3.0f),
                new CampoDoc("PERCURSO (UF)", string.Join(" › ", mdfe.Percurso), 2.0f),
            ]),
        };

        // Veiculo e condutores.
        b.Add(new BlocoDoc.Secao("Veículo e Condutores"));

        VeiculoMdfe? vt = mdfe.VeiculoTracao;
        string condutores = vt is null
            ? string.Empty
            : string.Join(" · ", vt.Condutores.Select(
                c => $"{c.Nome} ({Formatos.Cpf(c.Cpf)})"));

        b.Add(new BlocoDoc.Linha(
        [
            new CampoDoc("PLACA (TRAÇÃO)", vt?.Placa, 0.9f, AlinhamentoH.Centro, Destacado: true),
            new CampoDoc("UF", vt?.Uf, 0.4f, AlinhamentoH.Centro),
            new CampoDoc("RENAVAM", vt?.Renavam, 1.0f),
            new CampoDoc("TARA (KG)", Formatos.Quantidade(vt?.Tara), 0.8f, AlinhamentoH.Direita),
            new CampoDoc("CAP. (KG)", Formatos.Quantidade(vt?.CapacidadeKg), 0.8f, AlinhamentoH.Direita),
            new CampoDoc("RNTRC", mdfe.Rntrc, 0.9f),
        ]));

        b.Add(new BlocoDoc.Linha(
        [
            new CampoDoc("CONDUTOR(ES)", condutores, 3.0f),
            new CampoDoc("REBOQUE(S)",
                string.Join(" · ", mdfe.Reboques.Select(r => r.Placa)), 1.6f),
        ]));

        // Totais.
        b.Add(new BlocoDoc.Secao("Totalizadores do Manifesto"));
        b.Add(new BlocoDoc.Linha(
        [
            new CampoDoc("QTD. CT-e", mdfe.Totais.QuantidadeCte?.ToString(), 0.8f, AlinhamentoH.Direita),
            new CampoDoc("QTD. NF-e", mdfe.Totais.QuantidadeNfe?.ToString(), 0.8f, AlinhamentoH.Direita),
            new CampoDoc("QTD. MDF-e", mdfe.Totais.QuantidadeMdfe?.ToString(), 0.8f, AlinhamentoH.Direita),
            new CampoDoc("PESO DA CARGA", $"{Formatos.Quantidade(mdfe.Totais.PesoCarga)} {mdfe.Totais.UnidadeMedida}",
                1.2f, AlinhamentoH.Direita),
            new CampoDoc("VALOR TOTAL DA CARGA", Formatos.Moeda(mdfe.Totais.ValorCarga),
                1.4f, AlinhamentoH.Direita, Destacado: true),
            new CampoDoc("PRODUTO PREDOMINANTE", mdfe.ProdutoPredominante, 1.8f),
        ]));

        if (mdfe.Lacres.Count > 0)
        {
            b.Add(new BlocoDoc.Linha(
            [
                new CampoDoc("LACRES", string.Join(", ", mdfe.Lacres), 1f),
            ]));
        }

        // Documentos vinculados: o corpo do manifesto, e o que pagina.
        b.Add(new BlocoDoc.Secao("Documentos Fiscais Vinculados ao Manifesto"));

        var linhas = new List<string[]>(mdfe.Documentos.Count);
        foreach (DocumentoDescarga d in mdfe.Documentos)
        {
            linhas.Add(
            [
                d.Municipio ?? string.Empty,
                d.Tipo ?? string.Empty,
                d.Chave is not null
                    ? ChaveAcesso.DeAtributoId(d.Chave)?.Formatada ?? d.Chave
                    : string.Empty,
            ]);
        }

        b.Add(new BlocoDoc.Tabela(
        [
            new ColunaDoc("MUNICÍPIO DE DESCARREGAMENTO", 2.0f),
            new ColunaDoc("TIPO", 0.6f, AlinhamentoH.Centro),
            new ColunaDoc("CHAVE DE ACESSO", 5.0f),
        ], linhas));

        // Observacoes.
        b.Add(new BlocoDoc.Secao("Observações"));
        b.Add(new BlocoDoc.Texto("OBSERVAÇÕES", MontarObservacoes(mdfe), 14f));

        return b;
    }

    private static string MontarObservacoes(MdfeDocumento mdfe)
    {
        var partes = new List<string>();

        if (mdfe.EmContingencia)
        {
            // MOC 2.5: a transmissao deve ocorrer em ate 168 horas da emissao.
            partes.Add("EMISSÃO EM CONTINGÊNCIA - transmissão obrigatória em até 168 horas.");

            if (mdfe.DataHoraContingencia is { } dh)
            {
                partes.Add($"Início da contingência: {Formatos.DataHora(dh)}");
            }

            if (!string.IsNullOrWhiteSpace(mdfe.JustificativaContingencia))
            {
                partes.Add($"Motivo: {mdfe.JustificativaContingencia}");
            }
        }

        if (!string.IsNullOrWhiteSpace(mdfe.Observacoes))
        {
            partes.Add(mdfe.Observacoes!);
        }

        return string.Join('\n', partes);
    }
}
