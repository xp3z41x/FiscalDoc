using FiscalDoc.Core.Model.Nfe;
using FiscalDoc.Core.Values;
using FiscalDoc.Layout.Barcode;
using FiscalDoc.Layout.DisplayList;

namespace FiscalDoc.Layout.Composition;

/// <summary>Dados do cabecalho, iguais para DACTE, DAMDFE e evento.</summary>
public sealed record DadosCabecalho(
    string Sigla,
    string Descricao,
    string? Numero,
    string? Serie,
    string? ModalOuTipo,
    ChaveAcesso? Chave,
    string? RazaoSocialEmitente,
    Endereco? EnderecoEmitente,
    string? DocumentoEmitente,
    string? InscricaoEstadual,
    Protocolo? Protocolo,
    bool ExigeSemValorFiscal,
    string? DizerContingencia,
    string? TextoConsulta);

/// <summary>
/// Cabecalho comum aos documentos que passam pelo <see cref="MontadorDocumento"/>.
///
/// Reune emitente, identificacao do documento, codigo de barras da chave e
/// area do protocolo - que e o mesmo conjunto que o MOC do CT-e (2.12) e o do
/// MDF-e mandam repetir em toda folha adicional.
/// </summary>
public static class CabecalhoFiscal
{
    /// <summary>Altura minima do bloco de codigo de barras + chave.</summary>
    private const float AlturaBarras = 11.0f;

    /// <summary>
    /// O DACTE e o DAMDFE reservam 3 x 9 cm para o codigo, com barra entre
    /// 1,5 e 2,5 cm de altura e modulo de <b>no maximo</b> 0,03 cm.
    /// Repare a inversao em relacao ao DANFE, que especifica um <i>minimo</i>
    /// de modulo (0,02 cm) - por isso o valor nao e compartilhado.
    /// </summary>
    private const float ModuloMaximo = 0.3f;

    private const float ModuloMinimo = 0.2f;

    public static float Desenhar(
        Campo c,
        MontadorDocumento m,
        EstilosDanfe e,
        DadosCabecalho d,
        int pagina,
        int totalPaginas)
    {
        float y = m.Topo;

        float larguraEmit = m.Largura * 0.42f;
        float larguraId = m.Largura * 0.20f;
        float larguraChave = m.Largura - larguraEmit - larguraId;

        // 30 mm: 14 para o codigo de barras, 7,5 para a chave e 8,5 para a
        // area do protocolo - que precisa comportar o rotulo mais uma linha de
        // valor sem cortar. Com 26 mm sobravam 4,5 mm e o numero saia partido.
        const float alturaTopo = 30.0f;

        // ---- Emitente ----
        var emit = new RetanguloMm(m.Esquerda, y, larguraEmit, alturaTopo);
        c.Moldura(emit);

        RetanguloMm dentro = emit.Encolhido(1.8f);
        c.Texto(dentro.FatiaSuperior(5.5f), d.RazaoSocialEmitente, e.EmitenteNome, AlinhamentoH.Centro);

        if (d.EnderecoEmitente is { } en)
        {
            c.Texto(
                dentro.SemFatiaSuperior(6f),
                $"{en.LinhaLogradouro}\n{en.Bairro} - CEP: {Formatos.Cep(en.Cep)}\n"
                + $"{en.Municipio} - {en.Uf}"
                + (string.IsNullOrWhiteSpace(en.Fone)
                    ? string.Empty
                    : $"\nFone: {Formatos.Telefone(en.Fone)}"),
                e.EmitenteDados,
                AlinhamentoH.Centro,
                AlinhamentoV.Topo,
                quebrar: true);
        }

        // ---- Identificacao do documento ----
        var id = new RetanguloMm(emit.Direita, y, larguraId, alturaTopo);
        c.Moldura(id);

        RetanguloMm di = id.Encolhido(1.2f);
        float yi = di.Y;

        c.Texto(new RetanguloMm(di.X, yi, di.Largura, 5f), d.Sigla, e.PalavraDanfe, AlinhamentoH.Centro);
        yi += 5.2f;

        c.Texto(
            new RetanguloMm(di.X, yi, di.Largura, 6f),
            d.Descricao, e.DescricaoDanfe,
            AlinhamentoH.Centro, AlinhamentoV.Topo, quebrar: true);
        yi += 6.4f;

        c.Texto(
            new RetanguloMm(di.X, yi, di.Largura, 3.6f),
            $"N. {Formatos.NumeroNota(d.Numero)}", e.NumeroSerie, AlinhamentoH.Centro);
        yi += 3.6f;

        c.Texto(
            new RetanguloMm(di.X, yi, di.Largura, 3.6f),
            $"SÉRIE {Formatos.Serie(d.Serie)}", e.NumeroSerie, AlinhamentoH.Centro);
        yi += 3.6f;

        // MOC CT-e 2.21.2 e MOC MDF-e: numero de folha no alto de TODA folha,
        // inclusive a primeira.
        c.Texto(
            new RetanguloMm(di.X, yi, di.Largura, 3.6f),
            $"FOLHA {pagina:00}/{totalPaginas:00}", e.NumeroSerie, AlinhamentoH.Centro);

        // ---- Codigo de barras + chave ----
        float x3 = id.Direita;

        var caixaBarras = new RetanguloMm(x3, y, larguraChave, AlturaBarras + 3f);
        c.Moldura(caixaBarras);

        if (d.Chave is not null)
        {
            IReadOnlyList<int> modulos = Code128C.Codificar(d.Chave.Digitos);

            // A caixa e mais larga que o necessario; limita o codigo ao que o
            // modulo maximo de 0,03 cm permite, para nao esticar a barra alem
            // do que a norma admite.
            float larguraMaxima = Math.Min(
                caixaBarras.Largura - 3f,
                ModuloMaximo * Code128C.TotalModulos(44));

            var area = new RetanguloMm(
                caixaBarras.X + ((caixaBarras.Largura - larguraMaxima) / 2f),
                caixaBarras.Y + 1.5f,
                larguraMaxima,
                AlturaBarras);

            c.Barras(area, modulos, ModuloMinimo);
        }

        var caixaChave = new RetanguloMm(x3, caixaBarras.Base, larguraChave, 7.5f);
        c.Rotulado(
            caixaChave, "CHAVE DE ACESSO", d.Chave?.Formatada,
            AlinhamentoH.Centro, valorEmNegrito: true);

        var caixaProt = new RetanguloMm(
            x3, caixaChave.Base, larguraChave, alturaTopo - AlturaBarras - 3f - 7.5f);
        c.Moldura(caixaProt);
        DesenharProtocolo(c, e, d, caixaProt);

        y += alturaTopo;

        // ---- Faixa de identificacao ----
        var faixa = new RetanguloMm(m.Esquerda, y, m.Largura, 7.5f);

        RetanguloMm[] cols = Campo.Colunas(
            faixa, m.Largura * 0.34f, m.Largura * 0.26f, m.Largura * 0.18f, 0f);

        c.Rotulado(cols[0], "CNPJ / CPF DO EMITENTE", Formatos.CnpjOuCpf(d.DocumentoEmitente));
        c.Rotulado(cols[1], "INSCRIÇÃO ESTADUAL", d.InscricaoEstadual);
        c.Rotulado(cols[2], d.Sigla == "DAMDFE" ? "MODAL" : "MODAL / TIPO", d.ModalOuTipo);
        c.Rotulado(cols[3], "CONSULTA", d.TextoConsulta ?? "www.cte.fazenda.gov.br");

        return y + 7.5f;
    }

    /// <summary>
    /// Area do protocolo. E aqui que moram as tres mensagens obrigatorias:
    /// homologacao (MOC CT-e 2.20 e MDF-e 2.6, centralizada e em caixa alta),
    /// contingencia do MDF-e (2.5, "EMISSÃO EM CONTINGÊNCIA" em destaque) e,
    /// no caso normal, o protocolo de autorizacao.
    /// </summary>
    private static void DesenharProtocolo(
        Campo c, EstilosDanfe e, DadosCabecalho d, RetanguloMm caixa)
    {
        if (d.ExigeSemValorFiscal)
        {
            c.Texto(
                caixa.Encolhido(1f),
                "EMITIDO EM AMBIENTE DE HOMOLOGAÇÃO – SEM VALOR FISCAL",
                e.NumeroSerie,
                AlinhamentoH.Centro, AlinhamentoV.Meio, quebrar: true);
            return;
        }

        // Contingencia SEM protocolo: o dizer ocupa a caixa inteira, porque
        // nao ha protocolo a mostrar.
        if (d.DizerContingencia is not null && d.Protocolo?.Numero is null)
        {
            c.Texto(
                caixa.Encolhido(1f),
                d.DizerContingencia,
                e.NumeroSerie,
                AlinhamentoH.Centro, AlinhamentoV.Meio, quebrar: true);
            return;
        }

        // Contingencia COM protocolo: os dois aparecem, compactos.
        //
        // Um documento emitido em contingencia e transmitido depois recebe
        // autorizacao de verdade, e o numero fica no arquivo. Deixar o dizer
        // substituir a caixa apagava esse protocolo - a amostra
        // cte-400-contingencia.xml tem nProt e ele nao era impresso. Esconder
        // um dado que esta no XML e o espelho do que o MOC 3.1 proibe.
        //
        // A caixa tem cerca de 8,5 mm e nao comporta o dizer em 9 pt mais o
        // protocolo em duas linhas de 7 pt. Entao o protocolo vira UMA linha,
        // com o rotulo embutido: "PROTOCOLO 123... - 11/09/2026 16:01:38".
        // Uma tentativa anterior de dividir a caixa em 45/55 mantendo os
        // recuos originais deixava 1,4 mm para duas linhas - o numero saia
        // cortado ao meio e a data sumia, que e pior do que a falta.
        string valorProtocolo = d.Protocolo?.Numero is null
            ? string.Empty
            : $"{d.Protocolo.Numero} - {Formatos.DataHora(d.Protocolo.DataHoraRecebimento)}";

        if (d.DizerContingencia is not null)
        {
            // Duas linhas de 5,5 pt para o dizer, o resto para o protocolo.
            const float AlturaDizerMm = 4.6f;

            c.Texto(
                caixa.FatiaSuperior(AlturaDizerMm).Encolhido(1f, 0.3f, 1f, 0f),
                d.DizerContingencia,
                e.Rotulo,
                AlinhamentoH.Centro, AlinhamentoV.Topo, quebrar: true);

            if (valorProtocolo.Length > 0)
            {
                c.Texto(
                    caixa.SemFatiaSuperior(AlturaDizerMm)
                         .Encolhido(Campo.RecuoInterno, 0f, Campo.RecuoInterno, 0.3f),
                    $"PROTOCOLO {valorProtocolo}",
                    e.Rotulo,
                    AlinhamentoH.Centro, AlinhamentoV.Meio);
            }

            return;
        }

        c.Texto(
            caixa.Encolhido(Campo.RecuoInterno, 0.6f, Campo.RecuoInterno, 0f).FatiaSuperior(2.2f),
            "PROTOCOLO DE AUTORIZAÇÃO DE USO",
            e.Rotulo);

        // Sem protocolo o campo fica vazio: o MOC proibe imprimir informacao
        // que nao conste do arquivo, e isso inclui inventar um protocolo.
        string texto = d.Protocolo?.Numero is null
            ? string.Empty
            : $"{d.Protocolo.Numero}\n{Formatos.DataHora(d.Protocolo.DataHoraRecebimento)}";

        c.Texto(
            caixa.Encolhido(Campo.RecuoInterno, 2.8f, Campo.RecuoInterno, 0.5f),
            texto, e.Valor,
            AlinhamentoH.Centro, AlinhamentoV.Topo, quebrar: true);
    }
}
