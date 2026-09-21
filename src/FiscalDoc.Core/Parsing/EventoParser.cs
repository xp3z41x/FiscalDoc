using System.Xml.Linq;
using FiscalDoc.Core.Model;
using FiscalDoc.Core.Model.Evento;
using FiscalDoc.Core.Model.Nfe;
using FiscalDoc.Core.Values;

namespace FiscalDoc.Core.Parsing;

/// <summary>
/// Eventos de NF-e, CT-e e MDF-e.
///
/// Os tres tem a mesma forma: um &lt;evento&gt; com &lt;infEvento&gt;, um
/// &lt;detEvento&gt; cujo conteudo varia por tipo, e um &lt;retEvento&gt; com a
/// resposta da SEFAZ. Muda o nome dos elementos externos e o namespace, e e so.
/// Por isso um parser unico atende os tres - e a familia vem do sniffer.
///
/// O conteudo de detEvento e generico de proposito: cada tipo de evento tem
/// seus campos, e novos tipos surgem por Nota Tecnica. Em vez de enumerar
/// todos, os campos desconhecidos viram pares rotulo/valor, e o layout os
/// imprime. Um evento novo aparece legivel no papel sem alteracao de codigo.
/// </summary>
internal static class EventoParser
{
    internal static ResultadoLeitura Ler(XElement raiz, FamiliaDocumento familia)
    {
        XElement? evento = AcharEvento(raiz);
        XElement? inf = evento.El("infEvento");

        if (inf is null)
        {
            return new ResultadoLeitura.NaoFiscal(raiz.Name.LocalName);
        }

        XElement? det = inf.El("detEvento");
        XElement? ret = AcharRetorno(raiz).El("infEvento");

        FamiliaDocumento origem = familia switch
        {
            FamiliaDocumento.EventoCte => FamiliaDocumento.Cte,
            FamiliaDocumento.EventoMdfe => FamiliaDocumento.Mdfe,
            _ => FamiliaDocumento.Nfe,
        };

        string? codigo = inf.Str("tpEvento");

        var doc = new EventoDocumento(
            FamiliaOrigem: origem,
            ChaveReferenciada: ChaveAcesso.DeAtributoId(
                inf.Str("chNFe") ?? inf.Str("chCTe") ?? inf.Str("chMDFe")),
            Orgao: inf.Str("cOrgao"),
            Ambiente: XEl.LerAmbiente(inf.Int("tpAmb")),
            CnpjAutor: inf.Str("CNPJ"),
            CpfAutor: inf.Str("CPF"),
            CodigoEvento: codigo,
            DescricaoEvento: AcharDescricao(det) ?? TiposEvento.Descrever(codigo),
            SequenciaEvento: inf.Int("nSeqEvento"),
            DataHoraEvento: inf.DataHora("dhEvento"),
            // O nome do atributo muda por familia: a NF-e grava "versao" e o
            // CT-e e o MDF-e gravam "versaoEvento" - confirmado nos schemas
            // (eventoCTeTiposBasico_v4.00, eventoMDFeTiposBasico_v3.00,
            // leiauteEventoCancNFe_v1.00). Tentar so "versao" deixava a caixa
            // VERSAO em branco em todo evento de transporte.
            VersaoEvento: inf.Str("verEvento")
                ?? det.Attr("versao")
                ?? det.Attr("versaoEvento"),
            Detalhes: LerDetalhes(det),
            TextoCorrecao: det.Str("xCorrecao"),
            CondicoesUso: det.Str("xCondUso"),
            Justificativa: det.Str("xJust"),
            ProtocoloReferenciado: det.Str("nProt"),
            Retorno: LerRetorno(ret));

        return new ResultadoLeitura.Ok(doc);
    }

    /// <summary>
    /// descEvento fica direto em detEvento nos eventos de NF-e, e dentro do
    /// grupo do tipo (evCECTe, evEncMDFe...) nos de CT-e e MDF-e.
    /// </summary>
    private static string? AcharDescricao(XElement? det)
    {
        string? direto = det.Str("descEvento");
        if (direto is not null)
        {
            return direto;
        }

        foreach (XElement grupo in det?.Elements() ?? [])
        {
            string? aninhado = grupo.Str("descEvento");
            if (aninhado is not null)
            {
                return aninhado;
            }
        }

        return null;
    }

    private static XElement? AcharEvento(XElement raiz)
    {
        // procEventoNFe/evento, procEventoCTe/eventoCTe, procEventoMDFe/eventoMDFe,
        // ou o proprio elemento avulso.
        foreach (string nome in new[] { "evento", "eventoCTe", "eventoMDFe" })
        {
            XElement? e = raiz.El(nome);
            if (e is not null)
            {
                return e;
            }
        }

        return raiz.El("infEvento") is not null ? raiz : null;
    }

    private static XElement? AcharRetorno(XElement raiz)
    {
        foreach (string nome in new[] { "retEvento", "retEventoCTe", "retEventoMDFe" })
        {
            XElement? e = raiz.El(nome);
            if (e is not null)
            {
                return e;
            }
        }

        return null;
    }

    /// <summary>
    /// Campos de detEvento que o layout nao conhece por nome, convertidos em
    /// pares rotulo/valor. Os que ja tem lugar proprio no desenho ficam de
    /// fora, para nao aparecerem duas vezes.
    /// </summary>
    private static List<(string, string)> LerDetalhes(XElement? det)
    {
        var lista = new List<(string, string)>();

        if (det is null)
        {
            return lista;
        }

        foreach (XElement e in det.Elements())
        {
            if (TemLugarProprio(e.Name.LocalName))
            {
                continue;
            }

            // Grupo aninhado: desce um nivel, que e o suficiente para todos os
            // eventos publicados ate agora.
            if (e.HasElements)
            {
                foreach (XElement filho in e.Elements())
                {
                    if (TemLugarProprio(filho.Name.LocalName))
                    {
                        continue;
                    }

                    string? v = filho.Texto();
                    if (v is not null)
                    {
                        lista.Add((Rotular(filho.Name.LocalName), v));
                    }
                }

                continue;
            }

            string? valor = e.Texto();
            if (valor is not null)
            {
                lista.Add((Rotular(e.Name.LocalName), valor));
            }
        }

        return lista;
    }

    /// <summary>
    /// Campos que o desenho ja imprime em lugar proprio. Repeti-los na tabela
    /// de detalhes seria ruido.
    /// </summary>
    private static bool TemLugarProprio(string nome) =>
        nome is "descEvento" or "xCorrecao" or "xCondUso" or "xJust" or "nProt";

    /// <summary>
    /// Nome de campo do leiaute em rotulo legivel. Campo nao mapeado sai com o
    /// proprio nome, que ainda e informacao util - melhor do que sumir.
    /// </summary>
    private static string Rotular(string nome) => nome switch
    {
        "nProt" => "Protocolo",
        "xNome" => "Nome",
        "CPF" => "CPF",
        "CNPJ" => "CNPJ",
        "dhEntrega" => "Data/hora da entrega",
        "nDoc" => "Documento",
        "latGPS" => "Latitude",
        "longGPS" => "Longitude",
        "hashEntrega" => "Hash da entrega",
        "dtEnc" => "Data de encerramento",
        "cUF" => "UF",
        "cMun" => "Município",
        "xJust" => "Justificativa",
        "tpAutor" => "Tipo de autor",
        "verAplic" => "Versão do aplicativo",
        "chNFe" => "Chave da NF-e",
        "chCTe" => "Chave do CT-e",
        "nSeqEvento" => "Sequência",
        _ => nome,
    };

    private static RetornoEvento? LerRetorno(XElement? ret) => ret is null
        ? null
        : new RetornoEvento(
            CodigoStatus: ret.Str("cStat"),
            Motivo: ret.Str("xMotivo"),
            Protocolo: ret.Str("nProt"),
            DataHoraRegistro: ret.DataHora("dhRegEvento"),
            Orgao: ret.Str("cOrgao"));
}
