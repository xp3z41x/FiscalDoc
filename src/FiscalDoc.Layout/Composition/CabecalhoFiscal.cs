using System.Text;
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
    string? TextoConsulta,
    MatrizQr? QrCode = null);

/// <summary>
/// Cabecalho comum aos documentos que passam pelo <see cref="MontadorDocumento"/>.
///
/// Reune emitente, identificacao do documento, codigo de barras da chave e
/// area do protocolo - que e o mesmo conjunto que o MOC do CT-e (2.12) e o do
/// MDF-e mandam repetir em toda folha adicional.
///
/// <para>Com QR Code, o simbolo ganha uma coluna propria a direita, da altura
/// do topo e da faixa de identificacao juntos - a posicao do modelo oficial do
/// DACTE, que o repete em toda folha, e a dos oito modelos do DAMDFE (MOC
/// MDF-e 3.00b, Anexo II, 2.7), todos com o QR no canto superior direito. Sem
/// QR Code a geometria e a de sempre, milimetro por milimetro.</para>
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

    /// <summary>
    /// Matriz do QR Code do cabecalho, a partir do conteudo que veio no arquivo
    /// (qrCodCTe, qrCodMDFe) - nunca recalculado. O manual do DACTE o poe no
    /// campo 3 do cabecalho (MOC CT-e 4.00, Anexo II, 2.19.1), e o do DAMDFE o
    /// exige com no minimo 25 x 25 mm (MOC MDF-e 3.00b, Anexo II, 2.3 e 2.6.2).
    ///
    /// <para>Um texto maior do que o maior simbolo comporta faria
    /// <see cref="QrCode.Codificar"/> recusar. Diferente do cupom da NFC-e,
    /// aqui o QR e um campo entre dezenas: perde-se o simbolo, e nao o
    /// documento inteiro.</para>
    /// </summary>
    internal static MatrizQr? CodificarQr(string? conteudo)
    {
        if (string.IsNullOrWhiteSpace(conteudo)
            || Encoding.UTF8.GetByteCount(conteudo) > QrCode.CapacidadeDadosBytes(40))
        {
            return null;
        }

        return QrCode.Codificar(conteudo);
    }

    public static float Desenhar(
        Campo c,
        MontadorDocumento m,
        EstilosDanfe e,
        DadosCabecalho d,
        int pagina,
        int totalPaginas)
    {
        float y = m.Topo;

        // 30 mm: 14 para o codigo de barras, 7,5 para a chave e 8,5 para a
        // area do protocolo - que precisa comportar o rotulo mais uma linha de
        // valor sem cortar. Com 26 mm sobravam 4,5 mm e o numero saia partido.
        const float alturaTopo = 30.0f;
        const float alturaFaixa = 7.5f;

        // O QR Code ocupa um quadrado da altura do cabecalho inteiro, e o resto
        // se reparte na largura que sobra.
        float larguraQr = d.QrCode is null ? 0f : alturaTopo + alturaFaixa;
        float largura = m.Largura - larguraQr;

        // O codigo e montado antes da divisao das colunas porque e ele quem
        // diz quanto a da chave precisa: com letra no CNPJ sao ate 385 modulos,
        // contra 297 da chave so de digitos (NT Conjunta 2025.001, item 6).
        IReadOnlyList<int>? barras = d.Chave is null ? null : Code128.Codificar(d.Chave.Caracteres);
        int modulos = barras?.Sum() ?? Code128.TotalModulos(44);

        (float larguraEmit, float larguraId) = d.QrCode is null
            ? DividirSemQr(largura, modulos)
            : DividirComQr(largura, modulos);

        float larguraChave = largura - larguraEmit - larguraId;

        // ---- Emitente ----
        var emit = new RetanguloMm(m.Esquerda, y, larguraEmit, alturaTopo);
        c.Moldura(emit);

        RetanguloMm dentro = emit.Encolhido(1.8f);

        // A razao social quebra linha em vez de perder o fim: com o QR Code a
        // coluna do emitente estreita, e no retrato um nome que cabia inteiro
        // passaria a sair cortado. Numa linha so, a posicao e a de sempre.
        int linhasNome = LinhasDoNome(m.Medidor, e, d.RazaoSocialEmitente, dentro.Largura);
        float alturaNome = 5.5f + ((linhasNome - 1) * m.Medidor.AlturaLinhaMm(e.EmitenteNome));

        c.Texto(
            dentro.FatiaSuperior(alturaNome),
            d.RazaoSocialEmitente,
            e.EmitenteNome,
            AlinhamentoH.Centro,
            AlinhamentoV.Topo,
            quebrar: linhasNome > 1);

        if (d.EnderecoEmitente is { } en)
        {
            c.Texto(
                dentro.SemFatiaSuperior(alturaNome + 0.5f),
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

        // Duas linhas cabem nos 6 mm de sempre. Numa coluna estreita - o
        // retrato com QR Code - a descricao do DACTE pede tres, e com a altura
        // fixa a terceira passava por baixo do numero do documento. A caixa
        // cresce pelo que o texto mede; ha folga para isso ate o fim da coluna.
        int linhasDescricao = m.Medidor.Quebrar(d.Descricao, e.DescricaoDanfe, di.Largura).Count;
        float alturaDescricao = Math.Max(
            6f, (linhasDescricao * m.Medidor.AlturaLinhaMm(e.DescricaoDanfe)) + 0.4f);

        c.Texto(
            new RetanguloMm(di.X, yi, di.Largura, alturaDescricao),
            d.Descricao, e.DescricaoDanfe,
            AlinhamentoH.Centro, AlinhamentoV.Topo, quebrar: true);
        yi += alturaDescricao + 0.4f;

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

        if (barras is not null)
        {
            // A caixa e mais larga que o necessario; limita o codigo ao que o
            // modulo maximo de 0,03 cm permite, para nao esticar a barra alem
            // do que a norma admite.
            float larguraMaxima = Math.Min(caixaBarras.Largura - 3f, ModuloMaximo * modulos);

            var area = new RetanguloMm(
                caixaBarras.X + ((caixaBarras.Largura - larguraMaxima) / 2f),
                caixaBarras.Y + 1.5f,
                larguraMaxima,
                AlturaBarras);

            c.Barras(area, barras, ModuloMinimo);
        }

        var caixaChave = new RetanguloMm(x3, caixaBarras.Base, larguraChave, 7.5f);
        c.Rotulado(
            caixaChave, "CHAVE DE ACESSO", d.Chave?.Formatada,
            AlinhamentoH.Centro, valorEmNegrito: true);

        var caixaProt = new RetanguloMm(
            x3, caixaChave.Base, larguraChave, alturaTopo - AlturaBarras - 3f - 7.5f);
        c.Moldura(caixaProt);
        DesenharProtocolo(c, e, d, caixaProt);

        // ---- QR Code ----
        if (d.QrCode is { } qr)
        {
            var coluna = new RetanguloMm(m.Direita - larguraQr, y, larguraQr, alturaTopo + alturaFaixa);
            c.Moldura(coluna);

            float lado = Math.Min(QrCode.LadoCaixaImpressaMm, Math.Min(coluna.Largura, coluna.Altura) - 1f);
            c.Qr(
                new RetanguloMm(
                    coluna.X + ((coluna.Largura - lado) / 2f),
                    coluna.Y + ((coluna.Altura - lado) / 2f),
                    lado,
                    lado),
                qr);
        }

        y += alturaTopo;

        // ---- Faixa de identificacao ----
        var faixa = new RetanguloMm(m.Esquerda, y, largura, alturaFaixa);

        // Com QR Code a faixa perde 37,5 mm, e as proporcoes de sempre cortavam
        // "MODAL / TIPO" e o endereco de consulta no retrato. Aqui cada coluna
        // recebe pelo que o conteudo pede.
        RetanguloMm[] cols = d.QrCode is null
            ? Campo.Colunas(faixa, largura * 0.34f, largura * 0.26f, largura * 0.18f, 0f)
            : Campo.Colunas(faixa, largura * 0.20f, largura * 0.16f, largura * 0.34f, 0f);

        c.Rotulado(cols[0], "CNPJ / CPF DO EMITENTE", Formatos.CnpjOuCpf(d.DocumentoEmitente));
        c.Rotulado(cols[1], "INSCRIÇÃO ESTADUAL", d.InscricaoEstadual);
        c.Rotulado(cols[2], d.Sigla == "DAMDFE" ? "MODAL" : "MODAL / TIPO", d.ModalOuTipo);
        c.Rotulado(cols[3], "CONSULTA", d.TextoConsulta ?? "www.cte.fazenda.gov.br");

        return y + alturaFaixa;
    }

    /// <summary>
    /// Divisao da largura sem QR Code: 42 % para o emitente, 20 % para a
    /// identificacao e o resto para a chave. A chave com letra no CNPJ pode
    /// pedir mais que esse resto no retrato - no pior caso, 385 modulos no
    /// minimo de 0,02 cm, mais o respiro, sao 80 mm -, e o que falta sai do
    /// emitente. Com a chave so de digitos a divisao e a de sempre, milimetro
    /// por milimetro.
    /// </summary>
    private static (float Emitente, float Identificacao) DividirSemQr(float largura, int modulos)
    {
        float emitente = largura * 0.42f;
        float id = largura * 0.20f;
        float falta = LarguraMinimaChave(modulos) - (largura - emitente - id);

        return (falta > 0f ? emitente - falta : emitente, id);
    }

    /// <summary>
    /// Divisao da largura quando o QR Code toma a sua coluna. A proporcao de
    /// sempre espremeria o codigo de barras abaixo do modulo minimo no
    /// retrato; por isso a coluna da chave recebe primeiro o que o codigo
    /// precisa - entre o modulo minimo e o maximo, mais o respiro de 3 mm -,
    /// e o emitente fica com o resto.
    /// </summary>
    private static (float Emitente, float Identificacao) DividirComQr(float largura, int modulos)
    {
        float minima = LarguraMinimaChave(modulos);
        float maxima = (ModuloMaximo * modulos) + 3f;

        float chave = Math.Clamp(largura * 0.40f, minima, maxima);
        float id = Math.Clamp(largura * 0.20f, 30f, 55f);

        return (largura - chave - id, id);
    }

    /// <summary>O codigo inteiro no modulo minimo, mais 1,5 mm de respiro de cada lado.</summary>
    private static float LarguraMinimaChave(int modulos) => (ModuloMinimo * modulos) + 3f;

    /// <summary>
    /// Quantas linhas a razao social ocupa, ate tres: o xNome tem no maximo 60
    /// caracteres, e tres linhas e o que a caixa comporta junto com as quatro
    /// do endereco.
    /// </summary>
    private static int LinhasDoNome(IMedidorTexto medidor, EstilosDanfe e, string? nome, float largura)
    {
        if (string.IsNullOrWhiteSpace(nome))
        {
            return 1;
        }

        return Math.Clamp(medidor.Quebrar(nome, e.EmitenteNome, largura).Count, 1, 3);
    }

    /// <summary>
    /// Area do protocolo. E aqui que moram as tres mensagens obrigatorias:
    /// homologacao (MOC CT-e 2.20 e MDF-e 2.5, centralizada e em caixa alta),
    /// contingencia do MDF-e (2.4, "EMISSÃO EM CONTINGÊNCIA" em destaque) e,
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
