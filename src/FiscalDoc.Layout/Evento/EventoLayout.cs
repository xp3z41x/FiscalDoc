using FiscalDoc.Core.Model;
using FiscalDoc.Core.Model.Evento;
using FiscalDoc.Core.Values;
using FiscalDoc.Layout.Composition;
using FiscalDoc.Layout.DisplayList;

namespace FiscalDoc.Layout.Evento;

/// <summary>
/// Representacao grafica de evento de NF-e, CT-e ou MDF-e.
///
/// <para><b>Este layout e desenho proprio, e precisa ser dito com todas as
/// letras.</b> Nao existe representacao grafica obrigatoria para evento em
/// MOC nenhum dos tres documentos - eles tratam eventos como servico de XML.
/// A Carta de Correcao tem sim um texto legal de redacao fixa, o
/// <c>xCondUso</c>, mas essa e obrigacao do <i>arquivo</i>, nao de um
/// impresso; imprimir CC-e e convencao de mercado, nao exigencia do MOC.</para>
///
/// <para>O desenho e sobrio e de proposito: identifica o tipo de evento, a
/// chave do documento a que se refere, quem o registrou, o protocolo, e o
/// corpo. Nada de moldura de DANFE, que sugeriria uma formalidade que o
/// documento nao tem.</para>
///
/// <para>O <c>xCondUso</c> aparece em duas variantes validas no mundo real,
/// com e sem acentos. As duas sao impressas como vieram - o texto e legal e
/// nao cabe ao visualizador "corrigir".</para>
/// </summary>
public static class EventoLayout
{
    private const float MargemMm = 18.0f;

    public static ConjuntoPaginas Construir(
        EventoDocumento ev, IMedidorTexto medidor, EstilosDanfe? estilos = null)
    {
        ArgumentNullException.ThrowIfNull(ev);
        ArgumentNullException.ThrowIfNull(medidor);

        EstilosDanfe e = estilos ?? new EstilosDanfe();

        var m = new MontadorDocumento(TamanhoPapel.A4, MargemMm, e, medidor)
        {
            AlturaLinha = 8.4f,
            AlturaSecao = 4.8f,
        };

        return m.Montar(
            MontarBlocos(ev, m),
            (campo, pagina, total) => DesenharCabecalho(campo, m, e, ev, pagina, total));
    }

    private static float DesenharCabecalho(
        Campo c, MontadorDocumento m, EstilosDanfe e, EventoDocumento ev,
        int pagina, int totalPaginas)
    {
        float y = m.Topo;

        // Titulo: o tipo do evento, que e a informacao que identifica a folha.
        c.Texto(
            new RetanguloMm(m.Esquerda, y, m.Largura, 7f),
            ev.DescricaoEvento?.ToUpperInvariant() ?? "EVENTO",
            e.PalavraDanfe,
            AlinhamentoH.Centro);
        y += 8f;

        c.Texto(
            new RetanguloMm(m.Esquerda, y, m.Largura, 4.5f),
            DescreverOrigem(ev.FamiliaOrigem),
            e.DescricaoDanfe,
            AlinhamentoH.Centro);
        y += 6f;

        if (ev.ExigeSemValorFiscal)
        {
            c.Texto(
                new RetanguloMm(m.Esquerda, y, m.Largura, 5f),
                "EMITIDO EM AMBIENTE DE HOMOLOGAÇÃO – SEM VALOR FISCAL",
                e.NumeroSerie,
                AlinhamentoH.Centro);
            y += 6.5f;
        }

        c.Linha(m.Esquerda, y, m.Direita, y, 0.3f);
        y += 3f;

        if (totalPaginas > 1)
        {
            c.Texto(
                new RetanguloMm(m.Esquerda, m.Topo, m.Largura, 4f),
                $"FOLHA {pagina:00}/{totalPaginas:00}",
                e.Rotulo,
                AlinhamentoH.Direita);
        }

        return y;
    }

    private static string DescreverOrigem(FamiliaDocumento f) => f switch
    {
        FamiliaDocumento.Cte => "Evento de Conhecimento de Transporte Eletrônico (CT-e)",
        FamiliaDocumento.Mdfe => "Evento de Manifesto Eletrônico de Documentos Fiscais (MDF-e)",
        _ => "Evento de Nota Fiscal Eletrônica (NF-e)",
    };

    private static List<BlocoDoc> MontarBlocos(EventoDocumento ev, MontadorDocumento m)
    {
        var b = new List<BlocoDoc>
        {
            new BlocoDoc.Secao("Documento Referenciado"),

            new BlocoDoc.Linha(
            [
                new CampoDoc("CHAVE DE ACESSO", ev.ChaveReferenciada?.Formatada,
                    1f, AlinhamentoH.Centro, Destacado: true),
            ]),

            new BlocoDoc.Secao("Identificação do Evento"),

            new BlocoDoc.Linha(
            [
                new CampoDoc("TIPO DO EVENTO", ev.CodigoEvento, 1.0f),
                new CampoDoc("DESCRIÇÃO", ev.DescricaoEvento, 2.2f),
                new CampoDoc("SEQUÊNCIA", ev.SequenciaEvento?.ToString(), 0.7f, AlinhamentoH.Centro),
                new CampoDoc("DATA E HORA DO EVENTO", Formatos.DataHora(ev.DataHoraEvento), 1.6f),
            ]),

            new BlocoDoc.Linha(
            [
                new CampoDoc("CNPJ / CPF DO AUTOR", Formatos.CnpjOuCpf(ev.DocumentoAutor), 1.4f),
                new CampoDoc("ÓRGÃO", ev.Orgao, 0.6f, AlinhamentoH.Centro),
                new CampoDoc("AMBIENTE",
                    ev.Ambiente == Core.Model.Nfe.Ambiente.Producao ? "Produção" : "Homologação", 1.0f),
                new CampoDoc("VERSÃO", ev.VersaoEvento, 0.7f, AlinhamentoH.Centro),
            ]),
        };

        // Corpo do evento.
        if (!string.IsNullOrWhiteSpace(ev.TextoCorrecao))
        {
            b.Add(new BlocoDoc.Secao("Correção"));
            b.Add(new BlocoDoc.Texto("TEXTO DA CORREÇÃO", ev.TextoCorrecao, 24f));
        }

        if (!string.IsNullOrWhiteSpace(ev.Justificativa))
        {
            b.Add(new BlocoDoc.Secao("Justificativa"));
            b.Add(new BlocoDoc.Texto("JUSTIFICATIVA", ev.Justificativa, 18f));
        }

        if (!string.IsNullOrWhiteSpace(ev.ProtocoloReferenciado))
        {
            b.Add(new BlocoDoc.Linha(
            [
                new CampoDoc("PROTOCOLO DO DOCUMENTO REFERENCIADO", ev.ProtocoloReferenciado, 1f),
            ]));
        }

        // Campos especificos do tipo de evento, em pares rotulo/valor. Um tipo
        // novo, criado por Nota Tecnica, aparece aqui sem alteracao de codigo.
        if (ev.Detalhes.Count > 0)
        {
            b.Add(new BlocoDoc.Secao("Detalhes do Evento"));

            var linhas = new List<string[]>(ev.Detalhes.Count);
            foreach ((string rotulo, string valor) in ev.Detalhes)
            {
                linhas.Add([rotulo, valor]);
            }

            b.Add(new BlocoDoc.Tabela(
            [
                new ColunaDoc("CAMPO", 1.2f),
                new ColunaDoc("CONTEÚDO", 3.0f),
            ], linhas));
        }

        // Registro na SEFAZ.
        b.Add(new BlocoDoc.Secao("Registro do Evento"));

        if (ev.Retorno is { } r)
        {
            b.Add(new BlocoDoc.Linha(
            [
                new CampoDoc("PROTOCOLO", r.Protocolo, 1.4f, AlinhamentoH.Esquerda, Destacado: true),
                new CampoDoc("DATA E HORA DO REGISTRO", Formatos.DataHora(r.DataHoraRegistro), 1.4f),
                new CampoDoc("STATUS", r.CodigoStatus, 0.6f, AlinhamentoH.Centro),
                new CampoDoc("MOTIVO", r.Motivo, 2.2f),
            ]));
        }
        else
        {
            // Evento sem retorno e evento nao registrado. Dizer isso e melhor
            // do que deixar um quadro vazio que parece falha de leitura.
            b.Add(new BlocoDoc.Linha(
            [
                new CampoDoc("SITUAÇÃO",
                    "Este arquivo não contém o retorno da SEFAZ — o evento não consta como registrado.",
                    1f),
            ]));
        }

        // O texto legal da Carta de Correcao, na integra e como veio.
        if (!string.IsNullOrWhiteSpace(ev.CondicoesUso))
        {
            b.Add(new BlocoDoc.Secao("Condições de Uso"));
            b.Add(new BlocoDoc.Texto("TEXTO LEGAL", ev.CondicoesUso, 26f));
        }

        return b;
    }
}
