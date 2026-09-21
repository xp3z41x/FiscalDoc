using FiscalDoc.Core.Model.Nfe;
using FiscalDoc.Core.Values;
using FiscalDoc.Layout.Barcode;
using FiscalDoc.Layout.Composition;
using FiscalDoc.Layout.DisplayList;

namespace FiscalDoc.Layout.Danfe;

/// <summary>
/// Monta o DANFE A4 retrato a partir do modelo, produzindo a display list.
///
/// A paginacao acontece <b>antes</b> de qualquer primitiva ser emitida: os
/// itens sao medidos, distribuidos em paginas, e so entao desenhados. E por
/// isso que "FOLHA nn/nn" sai certo na primeira pagina - o total ja e
/// conhecido. Ver plano 2.1.
/// </summary>
public static class DanfeRetrato
{
    public static ConjuntoPaginas Construir(
        NfeDocumento nfe,
        IMedidorTexto medidor,
        EstilosDanfe? estilos = null)
    {
        ArgumentNullException.ThrowIfNull(nfe);
        ArgumentNullException.ThrowIfNull(medidor);

        EstilosDanfe e = estilos ?? new EstilosDanfe();

        // --- 1. Medir cada item ------------------------------------------------
        List<ItemMedido> itens = MedirItens(nfe, medidor, e);

        // --- 2. Distribuir em paginas -----------------------------------------
        List<List<ItemMedido>> porPagina = Paginar(nfe, itens, medidor, e, out bool rodapeEmPaginaPropria);

        // --- 3. Desenhar -------------------------------------------------------
        int total = porPagina.Count;
        var paginas = new List<Pagina>(total);

        for (int i = 0; i < total; i++)
        {
            bool primeira = i == 0;
            bool ultima = i == total - 1;

            var saida = new List<Primitiva>(512);
            var campo = new Campo(saida, e);

            float y = DanfeMetricas.Topo;

            if (primeira)
            {
                y = DesenharCanhoto(campo, nfe, e, y);
            }

            y = DesenharCabecalho(campo, nfe, e, medidor, y, i + 1, total);

            if (primeira)
            {
                y = DesenharDestinatario(campo, nfe, e, y);
                y = DesenharFatura(campo, nfe, e, y);
                y = DesenharCalculoImposto(campo, nfe, e, y);
                y = DesenharIbsCbs(campo, nfe, e, y);
                y = DesenharTransportador(campo, nfe, e, y);
            }

            float alturaRodape = ultima ? AlturaRodape(nfe, medidor, e) : 0f;
            float baseProdutos = DanfeMetricas.Base - alturaRodape;

            bool semItensNestaPagina = porPagina[i].Count == 0 && rodapeEmPaginaPropria && ultima;

            if (!semItensNestaPagina)
            {
                DesenharProdutos(campo, porPagina[i], e, y, baseProdutos);
            }

            if (ultima)
            {
                DesenharRodape(campo, nfe, medidor, e, baseProdutos);
            }

            paginas.Add(new Pagina(i + 1, saida));
        }

        return new ConjuntoPaginas(
            paginas,
            TamanhoPapel.A4,
            RetanguloMm.DeCantos(
                DanfeMetricas.Esquerda, DanfeMetricas.Topo,
                DanfeMetricas.Direita, DanfeMetricas.Base));
    }

    // =====================================================================
    // Medicao e paginacao
    // =====================================================================

    /// <summary>Item com as linhas ja quebradas e a altura ocupada resolvida.</summary>
    private sealed record ItemMedido(
        ItemNfe Item,
        IReadOnlyList<string> LinhasDescricao,
        IReadOnlyList<string> LinhasCodigo,
        IReadOnlyList<string> LinhasInfoAdicional,
        float AlturaMm);

    private static List<ItemMedido> MedirItens(
        NfeDocumento nfe, IMedidorTexto medidor, EstilosDanfe e)
    {
        float alturaLinhaTexto = medidor.AlturaLinhaMm(e.ValorProduto);
        float larguraDescricao = DanfeMetricas.ColunasProduto[1].Largura - (2 * Campo.RecuoInterno);
        float larguraCodigo = DanfeMetricas.ColunasProduto[0].Largura - (2 * Campo.RecuoInterno);
        float larguraTotal = LarguraInfoAdicional;

        var medidos = new List<ItemMedido>(nfe.Itens.Count);

        foreach (ItemNfe item in nfe.Itens)
        {
            IReadOnlyList<string> descricao = medidor.Quebrar(
                item.Descricao ?? string.Empty, e.ValorProduto, larguraDescricao);

            IReadOnlyList<string> info = item.TemInformacaoAdicional
                ? medidor.Quebrar(item.InformacaoAdicional!, e.ValorProduto, larguraTotal)
                : [];

            // O CODIGO tambem quebra, e tambem manda na altura.
            //
            // cProd vai ate 60 caracteres numa coluna de 14,8 mm uteis: um
            // codigo de 15 caracteres - que o corpus real tem, em todos os 41
            // itens de um arquivo - ocupa duas linhas. Medir so a descricao
            // reservava uma linha e o recorte comia a segunda, deixando o
            // codigo com um hifen solto no fim, como se continuasse em lugar
            // nenhum.
            IReadOnlyList<string> codigo = medidor.Quebrar(
                item.Codigo ?? string.Empty, e.ValorProduto, larguraCodigo);

            // A altura do item e a da coluna mais alta, mais as linhas de
            // infAdProd, que o MOC 3.1.7 manda imprimir logo abaixo do item.
            int linhas = Math.Max(1, Math.Max(descricao.Count, codigo.Count)) + info.Count;

            medidos.Add(new ItemMedido(
                item,
                descricao,
                codigo,
                info,
                (linhas * alturaLinhaTexto) + (2 * PadVertical)));
        }

        return medidos;
    }

    private const float PadVertical = 0.35f;

    /// <summary>
    /// Largura util do infAdProd. Uma constante so: medir numa largura e
    /// desenhar noutra quebra o texto duas vezes com resultados diferentes, e
    /// a segunda quebra nao tem altura reservada.
    /// </summary>
    private const float LarguraInfoAdicional =
        DanfeMetricas.Largura - (2 * Campo.RecuoInterno);

    private static List<List<ItemMedido>> Paginar(
        NfeDocumento nfe,
        List<ItemMedido> itens,
        IMedidorTexto medidor,
        EstilosDanfe e,
        out bool rodapeEmPaginaPropria)
    {
        float topoProdutosPrimeira = AlturaCabecalhoCompleto() + AlturaBlocosIniciais(nfe);
        float topoProdutosSeguinte = AlturaCabecalhoContinuacao();

        float rodape = AlturaRodape(nfe, medidor, e);

        float topoPrimeira =
            topoProdutosPrimeira + DanfeMetricas.AlturaTitulo + DanfeMetricas.AlturaCabecalhoProdutos;

        float topoSeguinte =
            topoProdutosSeguinte + DanfeMetricas.AlturaTitulo + DanfeMetricas.AlturaCabecalhoProdutos;

        var paginas = new List<List<ItemMedido>>();
        var atual = new List<ItemMedido>();

        float y = topoPrimeira;

        foreach (ItemMedido m in itens)
        {
            if (y + m.AlturaMm > DanfeMetricas.Base && atual.Count > 0)
            {
                paginas.Add(atual);
                atual = [];
                y = topoSeguinte;
            }

            atual.Add(m);
            y += m.AlturaMm;
        }

        paginas.Add(atual);

        // O rodape (ISSQN e dados adicionais) fica na ULTIMA pagina, e precisa
        // caber la. Os itens foram empilhados ate a borda, entao pode nao
        // caber: nesse caso empurra-se o fim da lista para uma folha nova, um
        // item por vez, ate sobrar espaco.
        //
        // Antes o rodape simplesmente ganhava uma folha SO PARA ELE, sem nem o
        // quadro de produtos: o DANFE de 74 itens do corpus saia em 5 folhas
        // com a quinta trazendo cabecalho, rodape e cerca de 200 mm de branco
        // no meio. A folha a mais continua sendo necessaria - o que muda e que
        // agora ela carrega documento.
        while (paginas[^1].Count > 1)
        {
            float topoUltima = paginas.Count == 1 ? topoPrimeira : topoSeguinte;

            if (topoUltima + Altura(paginas[^1]) + rodape <= DanfeMetricas.Base)
            {
                break;
            }

            List<ItemMedido> ultima = paginas[^1];
            ItemMedido empurrado = ultima[^1];
            ultima.RemoveAt(ultima.Count - 1);
            paginas.Add([empurrado]);
        }

        // Ultimo recurso: nem uma folha de continuacao com um item so comporta
        // o rodape - infCpl gigantesco. Ai a folha propria e mesmo a saida, e
        // e preferivel uma folha a mais do que dado fiscal espremido.
        float usadoNaUltima =
            (paginas.Count == 1 ? topoPrimeira : topoSeguinte) + Altura(paginas[^1]);

        rodapeEmPaginaPropria = usadoNaUltima + rodape > DanfeMetricas.Base;

        if (rodapeEmPaginaPropria)
        {
            paginas.Add([]);
        }

        return paginas;
    }

    private static float Altura(List<ItemMedido> pagina)
    {
        float total = 0f;

        foreach (ItemMedido m in pagina)
        {
            total += m.AlturaMm;
        }

        return total;
    }

    /// <summary>Altura do canhoto mais o cabecalho, na primeira pagina.</summary>
    private static float AlturaCabecalhoCompleto() =>
        DanfeMetricas.Topo
        + DanfeMetricas.AlturaCanhoto + DanfeMetricas.VaoCanhoto
        + DanfeMetricas.AlturaEmitente + DanfeMetricas.AlturaLinha + DanfeMetricas.AlturaLinha;

    /// <summary>
    /// Cabecalho repetido nas paginas seguintes. O MOC 3.5 lista exatamente o
    /// que precisa se repetir: emitente, DANFE, numero/serie/folha, codigos de
    /// barras, natureza da operacao, chave e IE/IEST/CNPJ. Ou seja, o mesmo
    /// cabecalho - so o canhoto nao volta.
    /// </summary>
    private static float AlturaCabecalhoContinuacao() =>
        DanfeMetricas.Topo
        + DanfeMetricas.AlturaEmitente + DanfeMetricas.AlturaLinha + DanfeMetricas.AlturaLinha;

    private static float AlturaBlocosIniciais(NfeDocumento nfe)
    {
        float h = 0f;

        // Destinatario: titulo + 3 linhas.
        h += DanfeMetricas.AlturaTitulo + (3 * DanfeMetricas.AlturaLinha);

        // Fatura/duplicatas: titulo + 1 faixa. O MOC 3.3.2 permite suprimir,
        // mas so quando nao ha nenhuma - o que nao e o caso comum.
        h += DanfeMetricas.AlturaTitulo + DanfeMetricas.AlturaLinha;

        // Calculo do imposto: titulo + 2 linhas.
        h += DanfeMetricas.AlturaTitulo + (2 * DanfeMetricas.AlturaLinha);

        // IBS/CBS: so ocupa espaco quando o documento traz o grupo. Notas
        // anteriores a reforma nao pagam por um bloco que nao teriam.
        if (nfe.IbsCbs is not null)
        {
            h += DanfeMetricas.AlturaBlocoIbsCbs;
        }

        // Transportador: titulo + 3 linhas.
        h += DanfeMetricas.AlturaTitulo + (3 * DanfeMetricas.AlturaLinha);

        return h;
    }

    /// <summary>
    /// Altura do rodape, com os dados adicionais <b>medidos</b>.
    ///
    /// <para>O metodo recebia o medidor e os estilos e ignorava os dois: a
    /// altura era a constante <c>AlturaDadosAdicionais</c> (30,7 mm, ~11
    /// linhas a 6 pt) independentemente do texto. Como <c>infCpl</c> vai a
    /// 5000 caracteres no leiaute, tudo alem da decima primeira linha era
    /// recortado - sem reticencias, sem continuacao, sem aviso. E o
    /// "ultimo recurso" da paginacao, que existe para o caso do infCpl
    /// gigantesco, era inalcancavel porque nada media o infCpl.</para>
    ///
    /// <para>Agora o quadro cresce com o conteudo, e como <see cref="Paginar"/>
    /// reserva o rodape na ultima pagina e empurra itens, o espaco aparece
    /// sozinho. O teto e o corpo de uma pagina de continuacao: alem disso nao
    /// ha para onde crescer.</para>
    /// </summary>
    private static float AlturaRodape(NfeDocumento nfe, IMedidorTexto medidor, EstilosDanfe e)
    {
        float h = 0f;

        if (nfe.Issqn is not null)
        {
            h += DanfeMetricas.AlturaTitulo + DanfeMetricas.AlturaLinha;
        }

        h += DanfeMetricas.AlturaTitulo + AlturaDadosAdicionais(nfe, medidor, e);

        return h;
    }

    /// <summary>Altura do quadro de dados adicionais, medida.</summary>
    private static float AlturaDadosAdicionais(
        NfeDocumento nfe, IMedidorTexto medidor, EstilosDanfe e)
    {
        string texto = TextosMoc.MontarInformacoesComplementares(nfe);

        if (texto.Length == 0)
        {
            return DanfeMetricas.AlturaDadosAdicionais;
        }

        float larguraUtil = DanfeMetricas.LarguraInfoComplementares - (2 * Campo.RecuoInterno);
        int linhas = medidor.Quebrar(texto, e.InfoComplementar, larguraUtil).Count;

        // 3 mm de rotulo em cima, 0,5 mm de respiro embaixo.
        float necessaria = 3f + (linhas * medidor.AlturaLinhaMm(e.InfoComplementar)) + 0.5f;

        // Nunca menor que a caixa do MOC, nunca maior que o corpo de uma
        // pagina de continuacao.
        float teto = DanfeMetricas.Base - AlturaCabecalhoContinuacao() - DanfeMetricas.AlturaTitulo;

        return Math.Clamp(necessaria, DanfeMetricas.AlturaDadosAdicionais, teto);
    }

    // =====================================================================
    // Blocos
    // =====================================================================

    private static float DesenharCanhoto(Campo c, NfeDocumento nfe, EstilosDanfe e, float y)
    {
        float larguraEsq = DanfeMetricas.Largura - DanfeMetricas.LarguraNumeroCanhoto;

        var recebemos = new RetanguloMm(
            DanfeMetricas.Esquerda, y, larguraEsq, DanfeMetricas.AlturaLinha);

        c.Moldura(recebemos);
        c.Texto(
            recebemos.Encolhido(Campo.RecuoInterno, 0.8f, Campo.RecuoInterno, 0.4f),
            $"RECEBEMOS DE {nfe.Emitente.RazaoSocial?.ToUpperInvariant()} OS PRODUTOS / SERVIÇOS "
            + "CONSTANTES DA NOTA FISCAL INDICADA AO LADO",
            e.Rotulo,
            AlinhamentoH.Esquerda,
            AlinhamentoV.Topo,
            quebrar: true);

        // Caixa de numero e serie, a direita, ocupando as duas linhas.
        var numero = new RetanguloMm(
            DanfeMetricas.Esquerda + larguraEsq, y,
            DanfeMetricas.LarguraNumeroCanhoto, DanfeMetricas.AlturaCanhoto);

        c.Moldura(numero);
        c.Texto(
            numero.Encolhido(Campo.RecuoInterno, 1.2f, Campo.RecuoInterno, 0f).FatiaSuperior(4f),
            "NF-e",
            e.NumeroSerie,
            AlinhamentoH.Centro);
        c.Texto(
            new RetanguloMm(numero.X, numero.Y + 6.0f, numero.Largura, 4f),
            $"N. {Formatos.NumeroNota(nfe.Ide.Numero)}",
            e.DescricaoDanfe,
            AlinhamentoH.Centro);
        c.Texto(
            new RetanguloMm(numero.X, numero.Y + 10.0f, numero.Largura, 4f),
            $"SÉRIE {Formatos.Serie(nfe.Ide.Serie)}",
            e.DescricaoDanfe,
            AlinhamentoH.Centro);

        float y2 = y + DanfeMetricas.AlturaLinha;

        var dataReceb = new RetanguloMm(
            DanfeMetricas.Esquerda, y2,
            DanfeMetricas.LarguraDataRecebimento, DanfeMetricas.AlturaLinha);
        c.Rotulado(dataReceb, "DATA DE RECEBIMENTO", null);

        var assinatura = new RetanguloMm(
            dataReceb.Direita, y2,
            larguraEsq - DanfeMetricas.LarguraDataRecebimento, DanfeMetricas.AlturaLinha);
        c.Rotulado(assinatura, "IDENTIFICAÇÃO E ASSINATURA DO RECEBEDOR", null);

        // Linha de destaque: e onde o papel e rasgado na entrega.
        float yLinha = y + DanfeMetricas.AlturaCanhoto + (DanfeMetricas.VaoCanhoto / 2f);
        c.LinhaTracejada(DanfeMetricas.Esquerda, yLinha, DanfeMetricas.Direita);

        return y + DanfeMetricas.AlturaCanhoto + DanfeMetricas.VaoCanhoto;
    }

    private static float DesenharCabecalho(
        Campo c,
        NfeDocumento nfe,
        EstilosDanfe e,
        IMedidorTexto medidor,
        float y,
        int pagina,
        int totalPaginas)
    {
        float yTopo = y;

        // ---- Coluna 1: identificacao do emitente ----
        var emit = new RetanguloMm(
            DanfeMetricas.Esquerda, y, DanfeMetricas.LarguraEmitente, DanfeMetricas.AlturaEmitente);
        c.Moldura(emit);

        RetanguloMm dentro = emit.Encolhido(2f);

        // A razao social e centralizada e nao quebrava: um nome comprido
        // transbordava para os DOIS lados e o recorte comia o comeco e o fim -
        // 3 dos 10 arquivos reais saiam com o nome do emitente mutilado no
        // campo mais visivel do documento.
        //
        // A saida nao e reduzir a fonte: o MOC 3.7 fixa 10 pt como MINIMO para
        // conteudo. Entao quebra-se em duas linhas, e o endereco desce o
        // tanto que a segunda linha ocupou. O quadro tem 39,2 mm; o endereco
        // usa quatro linhas curtas, entao ha folga para isso.
        float alturaNome = medidor.AlturaLinhaMm(e.EmitenteNome);
        bool cabeNumaLinha =
            medidor.LarguraMm(nfe.Emitente.RazaoSocial ?? string.Empty, e.EmitenteNome)
            <= dentro.Largura;

        float bandaNome = cabeNumaLinha ? 6f : Math.Max(6f, (2 * alturaNome) + 0.6f);

        c.Texto(
            dentro.FatiaSuperior(bandaNome),
            nfe.Emitente.RazaoSocial,
            e.EmitenteNome,
            AlinhamentoH.Centro,
            AlinhamentoV.Topo,
            quebrar: !cabeNumaLinha);

        Endereco en = nfe.Emitente.Endereco;
        string linhas =
            $"{en.LinhaLogradouro}\n"
            + $"{en.Bairro} - CEP: {Formatos.Cep(en.Cep)}\n"
            + $"{en.Municipio} - {en.Uf}\n"
            + (string.IsNullOrWhiteSpace(en.Fone) ? string.Empty : $"Fone: {Formatos.Telefone(en.Fone)}");

        c.Texto(
            dentro.SemFatiaSuperior(bandaNome + 1f),
            linhas,
            e.EmitenteDados,
            AlinhamentoH.Centro,
            AlinhamentoV.Topo,
            quebrar: true);

        // ---- Coluna 2: quadro DANFE ----
        var quadro = new RetanguloMm(
            emit.Direita, y, DanfeMetricas.LarguraQuadroDanfe, DanfeMetricas.AlturaEmitente);
        c.Moldura(quadro);
        DesenharQuadroDanfe(c, nfe, e, quadro, pagina, totalPaginas);

        // ---- Coluna 3: barras, chave, consulta, protocolo ----
        float x3 = quadro.Direita;
        float larg3 = DanfeMetricas.LarguraColunaChave;

        var caixaBarras = new RetanguloMm(x3, y, larg3, DanfeMetricas.AlturaQuadroBarras);
        c.Moldura(caixaBarras);
        DesenharCodigoBarrasChave(c, nfe, caixaBarras);

        var caixaChave = new RetanguloMm(
            x3, caixaBarras.Base, larg3, DanfeMetricas.AlturaLinha);
        c.Rotulado(
            caixaChave,
            "CHAVE DE ACESSO",
            nfe.Chave?.Formatada,
            AlinhamentoH.Centro,
            valorEmNegrito: true);

        var caixaConsulta = new RetanguloMm(
            x3, caixaChave.Base, larg3, DanfeMetricas.AlturaConsulta);
        c.Moldura(caixaConsulta);
        c.Texto(
            caixaConsulta.Encolhido(1.5f),
            TextosMoc.Consulta(nfe.Ide.TipoEmissao),
            e.DescricaoDanfe,
            AlinhamentoH.Centro,
            AlinhamentoV.Meio,
            quebrar: true);

        var caixaProtocolo = new RetanguloMm(
            x3, caixaConsulta.Base, larg3, DanfeMetricas.AlturaLinha);
        c.Rotulado(
            caixaProtocolo,
            TextosMoc.RotuloProtocolo(nfe.Ide.TipoEmissao),
            TextoProtocolo(nfe),
            AlinhamentoH.Centro);

        // ---- Natureza da operacao (abaixo do emitente, ao lado da coluna 3) ----
        float yNat = yTopo + DanfeMetricas.AlturaEmitente;
        var natureza = new RetanguloMm(
            DanfeMetricas.Esquerda, yNat, x3 - DanfeMetricas.Esquerda, DanfeMetricas.AlturaLinha);
        c.Rotulado(natureza, "NATUREZA DA OPERAÇÃO", nfe.Ide.NaturezaOperacao);

        // ---- IE / IE ST / CNPJ ----
        float yIe = caixaProtocolo.Base;
        RetanguloMm[] ie = Campo.Colunas(
            new RetanguloMm(DanfeMetricas.Esquerda, yIe, DanfeMetricas.Largura, DanfeMetricas.AlturaLinha),
            68.6f, 68.6f, 68.5f);

        c.Rotulado(ie[0], "INSCRIÇÃO ESTADUAL", nfe.Emitente.InscricaoEstadual);
        c.Rotulado(ie[1], "INSC. ESTADUAL DO SUBST. TRIBUTÁRIO", nfe.Emitente.InscricaoEstadualSt);
        c.Rotulado(ie[2], "CNPJ", Formatos.CnpjOuCpf(nfe.Emitente.Documento));

        return yIe + DanfeMetricas.AlturaLinha;
    }

    private static void DesenharQuadroDanfe(
        Campo c, NfeDocumento nfe, EstilosDanfe e, RetanguloMm quadro, int pagina, int total)
    {
        RetanguloMm d = quadro.Encolhido(1f);
        float y = d.Y + 0.5f;

        c.Texto(new RetanguloMm(d.X, y, d.Largura, 5f), "DANFE", e.PalavraDanfe, AlinhamentoH.Centro);
        y += 5.5f;

        c.Texto(
            new RetanguloMm(d.X, y, d.Largura, 7f),
            "Documento Auxiliar da Nota Fiscal Eletrônica",
            e.DescricaoDanfe,
            AlinhamentoH.Centro,
            AlinhamentoV.Topo,
            quebrar: true);
        y += 7.5f;

        // Tipo de operacao: o MOC manda destacar 0-ENTRADA / 1-SAIDA e indicar
        // qual dos dois e o caso.
        int tipo = (int)nfe.Ide.TipoOperacao;

        c.Texto(new RetanguloMm(d.X, y, d.Largura - 5f, 3f), "0 - ENTRADA", e.DescricaoDanfe);
        c.Texto(new RetanguloMm(d.X, y + 3f, d.Largura - 5f, 3f), "1 - SAÍDA", e.DescricaoDanfe);

        var caixaTipo = new RetanguloMm(d.Direita - 4.5f, y + 0.5f, 4f, 5f);
        c.Moldura(caixaTipo);
        c.Texto(
            caixaTipo,
            tipo.ToString(System.Globalization.CultureInfo.InvariantCulture),
            e.NumeroSerie,
            AlinhamentoH.Centro,
            AlinhamentoV.Meio);

        y += 7.5f;

        c.Texto(
            new RetanguloMm(d.X, y, d.Largura, 4f),
            $"N. {Formatos.NumeroNota(nfe.Ide.Numero)}",
            e.NumeroSerie,
            AlinhamentoH.Centro);
        y += 4f;

        c.Texto(
            new RetanguloMm(d.X, y, d.Largura, 4f),
            $"SÉRIE {Formatos.Serie(nfe.Ide.Serie)}",
            e.NumeroSerie,
            AlinhamentoH.Centro);
        y += 4f;

        // MOC 3.10.2: "FOLHA nn/nn" em TODA folha, inclusive a primeira, e
        // inclusive quando ha uma folha so.
        c.Texto(
            new RetanguloMm(d.X, y, d.Largura, 4f),
            $"FOLHA {pagina:00}/{total:00}",
            e.NumeroSerie,
            AlinhamentoH.Centro);
    }

    private static void DesenharCodigoBarrasChave(Campo c, NfeDocumento nfe, RetanguloMm caixa)
    {
        if (nfe.Chave is null)
        {
            return;
        }

        IReadOnlyList<int> modulos = Code128C.Codificar(nfe.Chave.Digitos);

        float altura = Math.Min(DanfeMetricas.AlturaBarras, caixa.Altura - 3f);
        var area = new RetanguloMm(
            caixa.X + 1.5f,
            caixa.Y + ((caixa.Altura - altura) / 2f),
            caixa.Largura - 3f,
            altura);

        c.Barras(area, modulos, DanfeMetricas.ModuloMinimoBarras);
    }

    private static string TextoProtocolo(NfeDocumento nfe)
    {
        // MOC 3.1: e proibido imprimir informacao que nao conste do arquivo.
        // Sem protocolo, o campo fica vazio - nunca preenchido por conta propria.
        if (nfe.Protocolo?.Numero is null)
        {
            return string.Empty;
        }

        string dh = Formatos.DataHora(nfe.Protocolo.DataHoraRecebimento);
        return string.IsNullOrEmpty(dh)
            ? nfe.Protocolo.Numero
            : $"{nfe.Protocolo.Numero} - {dh}";
    }

    private static float DesenharDestinatario(Campo c, NfeDocumento nfe, EstilosDanfe est, float y)
    {
        c.TituloBloco(
            new RetanguloMm(DanfeMetricas.Esquerda, y, DanfeMetricas.Largura, DanfeMetricas.AlturaTitulo),
            "Destinatário / Remetente");
        y += DanfeMetricas.AlturaTitulo;

        Destinatario d = nfe.Destinatario;
        Endereco en = d.Endereco;

        RetanguloMm[] l1 = Campo.Colunas(
            Faixa(y), DanfeMetricas.LarguraDestRazaoSocial, DanfeMetricas.LarguraDestCnpj, 0f);
        c.Rotulado(l1[0], "NOME / RAZÃO SOCIAL", d.RazaoSocial);
        c.Rotulado(l1[1], "CNPJ / CPF", Formatos.CnpjOuCpf(d.Documento));
        c.Rotulado(l1[2], "DATA DA EMISSÃO", Formatos.Data(nfe.Ide.DataHoraEmissao));
        y += DanfeMetricas.AlturaLinha;

        RetanguloMm[] l2 = Campo.Colunas(
            Faixa(y),
            DanfeMetricas.LarguraDestEndereco, DanfeMetricas.LarguraDestBairro,
            DanfeMetricas.LarguraDestCep, 0f);
        c.Rotulado(l2[0], "ENDEREÇO", en.LinhaLogradouro);
        c.Rotulado(l2[1], "BAIRRO / DISTRITO", en.Bairro);
        c.Rotulado(l2[2], "CEP", Formatos.Cep(en.Cep));
        c.Rotulado(l2[3], "DATA DA SAÍDA / ENTRADA", Formatos.Data(nfe.Ide.DataHoraSaidaEntrada));
        y += DanfeMetricas.AlturaLinha;

        RetanguloMm[] l3 = Campo.Colunas(
            Faixa(y),
            DanfeMetricas.LarguraDestMunicipio, DanfeMetricas.LarguraDestFone,
            DanfeMetricas.LarguraDestUf, DanfeMetricas.LarguraDestCnpj, 0f);
        c.Rotulado(l3[0], "MUNICÍPIO", en.Municipio);
        c.Rotulado(l3[1], "FONE / FAX", Formatos.Telefone(en.Fone));
        c.Rotulado(l3[2], "UF", en.Uf, AlinhamentoH.Centro);
        c.Rotulado(l3[3], "INSCRIÇÃO ESTADUAL", d.InscricaoEstadual);
        c.Rotulado(l3[4], "HORA DE SAÍDA / ENTRADA", Formatos.Hora(nfe.Ide.DataHoraSaidaEntrada));

        return y + DanfeMetricas.AlturaLinha;
    }

    private static float DesenharFatura(Campo c, NfeDocumento nfe, EstilosDanfe e, float y)
    {
        c.TituloBloco(
            new RetanguloMm(DanfeMetricas.Esquerda, y, DanfeMetricas.Largura, DanfeMetricas.AlturaTitulo),
            "Fatura / Duplicatas");
        y += DanfeMetricas.AlturaTitulo;

        RetanguloMm faixa = Faixa(y);
        c.Moldura(faixa);

        // As duplicatas sao impressas lado a lado dentro da faixa unica, que e
        // o que o MOC chama de "admite dados de duplicatas do grupo Y07".
        var partes = new List<string>();

        if (nfe.Fatura is { } f && !string.IsNullOrWhiteSpace(f.Numero))
        {
            partes.Add($"Fat. {f.Numero}  {Formatos.Moeda(f.ValorLiquido ?? f.ValorOriginal)}");
        }

        foreach (Duplicata d in nfe.Duplicatas)
        {
            partes.Add($"{d.Numero}  {Formatos.Data(d.Vencimento)}  {Formatos.Moeda(d.Valor)}");
        }

        if (partes.Count > 0)
        {
            c.Texto(
                faixa.Encolhido(Campo.RecuoInterno, 1f, Campo.RecuoInterno, 0.5f),
                string.Join("      ", partes),
                e.ValorProduto,
                AlinhamentoH.Esquerda,
                AlinhamentoV.Meio);
        }

        return y + DanfeMetricas.AlturaLinha;
    }

    private static float DesenharCalculoImposto(Campo c, NfeDocumento nfe, EstilosDanfe e, float y)
    {
        c.TituloBloco(
            new RetanguloMm(DanfeMetricas.Esquerda, y, DanfeMetricas.Largura, DanfeMetricas.AlturaTitulo),
            "Cálculo do Imposto");
        y += DanfeMetricas.AlturaTitulo;

        TotaisIcms t = nfe.Totais;
        float w1 = DanfeMetricas.LarguraImpostoLinha1;

        RetanguloMm[] l1 = Campo.Colunas(Faixa(y), w1, w1, w1, w1, 0f);
        c.Rotulado(l1[0], "BASE DE CÁLCULO DO ICMS", Formatos.Moeda(t.BaseCalculoIcms), AlinhamentoH.Direita);
        c.Rotulado(l1[1], "VALOR DO ICMS", Formatos.Moeda(t.ValorIcms), AlinhamentoH.Direita);
        c.Rotulado(l1[2], "BASE DE CÁLCULO DO ICMS ST", Formatos.Moeda(t.BaseCalculoIcmsSt), AlinhamentoH.Direita);
        c.Rotulado(l1[3], "VALOR DO ICMS ST", Formatos.Moeda(t.ValorIcmsSt), AlinhamentoH.Direita);
        c.Rotulado(l1[4], "VALOR TOTAL DOS PRODUTOS", Formatos.Moeda(t.ValorTotalProdutos), AlinhamentoH.Direita);
        y += DanfeMetricas.AlturaLinha;

        float w2 = DanfeMetricas.LarguraImpostoLinha2;
        RetanguloMm[] l2 = Campo.Colunas(Faixa(y), w2, w2, w2, w2, w2, 0f);
        c.Rotulado(l2[0], "VALOR DO FRETE", Formatos.Moeda(t.ValorFrete), AlinhamentoH.Direita);
        c.Rotulado(l2[1], "VALOR DO SEGURO", Formatos.Moeda(t.ValorSeguro), AlinhamentoH.Direita);
        c.Rotulado(l2[2], "DESCONTO", Formatos.Moeda(t.ValorDesconto), AlinhamentoH.Direita);
        c.Rotulado(l2[3], "OUTRAS DESPESAS", Formatos.Moeda(t.OutrasDespesas), AlinhamentoH.Direita);
        c.Rotulado(l2[4], "VALOR DO IPI", Formatos.Moeda(t.ValorIpi), AlinhamentoH.Direita);
        c.Rotulado(l2[5], "VALOR TOTAL DA NOTA", Formatos.Moeda(t.ValorTotalNota), AlinhamentoH.Direita, valorEmNegrito: true);

        return y + DanfeMetricas.AlturaLinha;
    }

    /// <summary>
    /// Bloco de IBS e CBS, os tributos da Reforma Tributária.
    ///
    /// <para><b>O MOC 7.00 Anexo II é de 2020 e não tem quadro para estes
    /// valores</b> — o layout do DANFE foi publicado cinco anos antes de os
    /// grupos existirem no XML. Este bloco é, portanto, acréscimo, posicionado
    /// logo após CÁLCULO DO IMPOSTO por ser o lugar natural: é onde o leitor
    /// procura tributo totalizado, e reusa o mesmo gabarito de 4,06 cm por
    /// campo, de modo que as molduras dos dois blocos se alinham.</para>
    ///
    /// <para>Só os valores do arquivo são impressos. O MOC 3.1 é categórico —
    /// "não poderão ser impressas informações que não constem do arquivo da
    /// NF-e" — então não há soma IBS+CBS calculada aqui, ainda que fosse
    /// conveniente: esse total não existe no XML.</para>
    ///
    /// <para><b>Os rótulos não foram inventados.</b> A NT 2026.003, que
    /// especifica o DANFE Simplificado Tipo 2, criou a "Divisão III-A –
    /// Informações dos novos impostos IBS/CBS" com a redação
    /// <c>(+) CBS R$</c>, <c>(+) IBS R$</c> e <c>(+) IS R$</c>. É a única
    /// redação que um fisco brasileiro publicou para imprimir estes três
    /// tributos num documento auxiliar de NF-e, então é a adotada aqui — o
    /// "(+)" ainda carrega a semântica certa, porque IBS, CBS e IS são
    /// cobrados "por fora" e somam ao valor da nota.</para>
    ///
    /// <para>O bloco desaparece por inteiro quando o documento não traz o
    /// grupo, e nesse caso os milímetros voltam para o quadro de produtos.
    /// Imposto Seletivo e total com tributos aparecem só quando existem.</para>
    /// </summary>
    private static float DesenharIbsCbs(Campo c, NfeDocumento nfe, EstilosDanfe e, float y)
    {
        if (nfe.IbsCbs is not { } t)
        {
            return y;
        }

        c.TituloBloco(
            new RetanguloMm(DanfeMetricas.Esquerda, y, DanfeMetricas.Largura, DanfeMetricas.AlturaTitulo),
            "Tributos da Reforma Tributária (LC 214/2025)");
        y += DanfeMetricas.AlturaTitulo;

        // A linha é montada conforme o que o documento tem: IS só existe em
        // operação sujeita a ele, e vNFTot só interessa quando de fato diverge
        // de vNF - repetir o mesmo número em dois campos seria ruído.
        //
        // O peso reparte a largura: os dois campos de rótulo longo recebem
        // mais espaço, porque a 5,5 pt um rótulo maior que a coluna é cortado,
        // e rótulo cortado num documento fiscal é pior que rótulo abreviado.
        var campos = new List<(string Rotulo, string Valor, bool Destaque, float Peso)>
        {
            ("BASE DE CÁLCULO IBS/CBS", Formatos.Moeda(t.BaseCalculo), false, 1.5f),
            ("IBS ESTADUAL", Formatos.Moeda(t.ValorIbsUf), false, 1.0f),
            ("IBS MUNICIPAL", Formatos.Moeda(t.ValorIbsMun), false, 1.0f),
            ("(+) IBS R$", Formatos.Moeda(t.ValorIbs), true, 1.0f),
            ("(+) CBS R$", Formatos.Moeda(t.ValorCbs), true, 1.0f),
        };

        if (t.TemImpostoSeletivo)
        {
            campos.Add(("(+) IS R$", Formatos.Moeda(t.ValorIs), true, 1.0f));
        }

        if (t.TotalDivergeDoValorDaNota(nfe.Totais.ValorTotalNota))
        {
            campos.Add((
                "TOTAL COM IBS/CBS/IS",
                Formatos.Moeda(t.ValorTotalNotaComTributos),
                true,
                1.4f));
        }

        float pesoTotal = 0f;
        foreach ((_, _, _, float peso) in campos)
        {
            pesoTotal += peso;
        }

        var larguras = new float[campos.Count];
        for (int i = 0; i < campos.Count; i++)
        {
            larguras[i] = DanfeMetricas.Largura * (campos[i].Peso / pesoTotal);
        }

        RetanguloMm[] l = Campo.Colunas(Faixa(y), larguras);

        for (int i = 0; i < campos.Count; i++)
        {
            (string rotulo, string valor, bool destaque, _) = campos[i];
            c.Rotulado(l[i], rotulo, valor, AlinhamentoH.Direita, destaque);
        }

        return y + DanfeMetricas.AlturaLinha;
    }

    private static float DesenharTransportador(Campo c, NfeDocumento nfe, EstilosDanfe e, float y)
    {
        c.TituloBloco(
            new RetanguloMm(DanfeMetricas.Esquerda, y, DanfeMetricas.Largura, DanfeMetricas.AlturaTitulo),
            "Transportador / Volumes Transportados");
        y += DanfeMetricas.AlturaTitulo;

        Transportador? tr = nfe.Transportador;
        Veiculo? v = nfe.Veiculo;

        RetanguloMm[] l1 = Campo.Colunas(
            Faixa(y),
            DanfeMetricas.LarguraTranspRazaoSocial, DanfeMetricas.LarguraTranspFretePorConta,
            DanfeMetricas.LarguraTranspAntt, DanfeMetricas.LarguraTranspPlaca,
            DanfeMetricas.LarguraTranspUf, 0f);

        c.Rotulado(l1[0], "NOME / RAZÃO SOCIAL", tr?.RazaoSocial);
        c.Rotulado(l1[1], "FRETE POR CONTA", Rotulos.Frete(nfe.Frete));
        c.Rotulado(l1[2], "CÓDIGO ANTT", v?.Rntc);
        c.Rotulado(l1[3], "PLACA DO VEÍCULO", v?.Placa);
        c.Rotulado(l1[4], "UF", v?.Uf, AlinhamentoH.Centro);
        c.Rotulado(l1[5], "CNPJ / CPF", Formatos.CnpjOuCpf(tr?.Documento));
        y += DanfeMetricas.AlturaLinha;

        RetanguloMm[] l2 = Campo.Colunas(
            Faixa(y),
            DanfeMetricas.LarguraTranspEndereco, DanfeMetricas.LarguraTranspMunicipio,
            DanfeMetricas.LarguraTranspUf, 0f);
        c.Rotulado(l2[0], "ENDEREÇO", tr?.EnderecoCompleto);
        c.Rotulado(l2[1], "MUNICÍPIO", tr?.Municipio);
        c.Rotulado(l2[2], "UF", tr?.Uf, AlinhamentoH.Centro);
        c.Rotulado(l2[3], "INSCRIÇÃO ESTADUAL", tr?.InscricaoEstadual);
        y += DanfeMetricas.AlturaLinha;

        // A faixa do MOC comporta um volume. Quando o XML traz varios grupos
        // <vol>, imprime-se o PRIMEIRO inteiro - nao a soma.
        //
        // Somar quantidade e peso seria imprimir um numero que nao esta no
        // arquivo (MOC 3.1), e produzia uma linha incoerente: o total somado
        // ao lado da especie, da marca e da numeracao de um volume so. A
        // NFC-e ja recusa somar acrescimos pelo mesmo motivo; aqui era o
        // unico lugar do aplicativo que ainda calculava.
        Volume? vol = nfe.Volumes.Count > 0 ? nfe.Volumes[0] : null;
        decimal? qtdTotal = vol?.Quantidade;
        decimal? pesoBTotal = vol?.PesoBruto;
        decimal? pesoLTotal = vol?.PesoLiquido;

        RetanguloMm[] l3 = Campo.Colunas(
            Faixa(y),
            DanfeMetricas.LarguraVolQuantidade, DanfeMetricas.LarguraVolEspecie,
            DanfeMetricas.LarguraVolMarca, DanfeMetricas.LarguraVolNumeracao,
            DanfeMetricas.LarguraVolPesoBruto, 0f);

        c.Rotulado(l3[0], "QUANTIDADE", Formatos.Quantidade(qtdTotal), AlinhamentoH.Direita);
        c.Rotulado(l3[1], "ESPÉCIE", vol?.Especie);
        c.Rotulado(l3[2], "MARCA", vol?.Marca);
        c.Rotulado(l3[3], "NUMERAÇÃO", vol?.Numeracao);
        c.Rotulado(l3[4], "PESO BRUTO", Formatos.Quantidade(pesoBTotal), AlinhamentoH.Direita);
        c.Rotulado(l3[5], "PESO LÍQUIDO", Formatos.Quantidade(pesoLTotal), AlinhamentoH.Direita);

        return y + DanfeMetricas.AlturaLinha;
    }

    private static void DesenharProdutos(
        Campo c, List<ItemMedido> itens, EstilosDanfe e, float y, float baseMaxima)
    {
        c.TituloBloco(
            new RetanguloMm(DanfeMetricas.Esquerda, y, DanfeMetricas.Largura, DanfeMetricas.AlturaTitulo),
            "Dados dos Produtos / Serviços");
        y += DanfeMetricas.AlturaTitulo;

        float altura = baseMaxima - y;
        if (altura < DanfeMetricas.AlturaMinimaProdutos)
        {
            return;
        }

        var quadro = new RetanguloMm(DanfeMetricas.Esquerda, y, DanfeMetricas.Largura, altura);
        c.Moldura(quadro);

        // Cabecalho das colunas.
        float yCab = y;
        float x = DanfeMetricas.Esquerda;

        foreach ((string titulo, float largura, _) in DanfeMetricas.ColunasProduto)
        {
            var celula = new RetanguloMm(x, yCab, largura, DanfeMetricas.AlturaCabecalhoProdutos);

            c.Texto(
                celula.Encolhido(0.4f, 0.3f, 0.4f, 0.3f),
                titulo,
                e.ColunaProduto,
                AlinhamentoH.Centro,
                AlinhamentoV.Meio,
                quebrar: true);

            if (x > DanfeMetricas.Esquerda)
            {
                c.Linha(x, yCab, x, quadro.Base);
            }

            x += largura;
        }

        float yLinhaCab = yCab + DanfeMetricas.AlturaCabecalhoProdutos;
        c.Linha(DanfeMetricas.Esquerda, yLinhaCab, DanfeMetricas.Direita, yLinhaCab);

        // Itens.
        float yItem = yLinhaCab;

        foreach (ItemMedido m in itens)
        {
            if (yItem + m.AlturaMm > quadro.Base)
            {
                break;
            }

            DesenharItem(c, m, e, yItem);
            yItem += m.AlturaMm;

            // MOC 3.1.7: item que ocupa mais de uma linha exige destaque
            // divisorio. A linha fina entre itens cumpre isso e ainda ajuda a
            // leitura de qualquer tabela longa.
            if (yItem < quadro.Base)
            {
                c.Linha(DanfeMetricas.Esquerda, yItem, DanfeMetricas.Direita, yItem, 0.05f);
            }
        }
    }

    private static void DesenharItem(Campo c, ItemMedido m, EstilosDanfe e, float y)
    {
        ItemNfe it = m.Item;
        IcmsItem icms = it.Icms;

        string[] valores =
        [
            string.Join('\n', m.LinhasCodigo),
            string.Empty, // descricao sai a parte, porque pode ter varias linhas
            it.Ncm ?? string.Empty,
            icms.CstOuCsosnFormatado ?? string.Empty,
            it.Cfop ?? string.Empty,
            it.Unidade ?? string.Empty,
            Formatos.Quantidade(it.Quantidade),
            Formatos.ValorUnitario(it.ValorUnitario),
            Formatos.Moeda(it.ValorTotal),
            Formatos.Moeda(icms.BaseCalculo),
            Formatos.Moeda(icms.Valor),
            Formatos.Moeda(it.Ipi.Valor),
            Formatos.Percentual(icms.Aliquota),
            Formatos.Percentual(it.Ipi.Aliquota),
        ];

        float alturaTexto = m.AlturaMm - (2 * PadVertical);
        float x = DanfeMetricas.Esquerda;

        for (int i = 0; i < DanfeMetricas.ColunasProduto.Length; i++)
        {
            (_, float largura, bool numerica) = DanfeMetricas.ColunasProduto[i];
            var celula = new RetanguloMm(x, y + PadVertical, largura, alturaTexto);

            if (i == 1)
            {
                c.Texto(
                    celula.Encolhido(Campo.RecuoInterno, 0f, Campo.RecuoInterno, 0f),
                    string.Join('\n', m.LinhasDescricao),
                    e.ValorProduto,
                    AlinhamentoH.Esquerda,
                    AlinhamentoV.Topo,
                    quebrar: true);
            }
            else
            {
                // So a coluna 0 (CODIGO) quebra aqui, e ela ja vem com as
                // quebras decididas por MedirItens - junta-se com quebra
                // explicita, como a descricao, para o renderizador nao decidir
                // de novo com outro algoritmo.
                //
                // NCM, CST, CFOP e UNID NAO quebram: elas nao sao medidas, e
                // uma segunda linha nelas nao teria altura reservada. Sao
                // identificadores curtos; se um dia nao couberem, o lugar de
                // resolver e a largura da coluna, no DanfeMetricas.
                //
                // A coluna CODIGO tem 14,8 mm uteis a 6 pt, e cProd vai ate 60
                // caracteres: um codigo de 15 - que o corpus real tem, em
                // todos os 41 itens de um arquivo - perdia o ultimo caractere
                // no recorte. cProd e uma das colunas que o MOC 3.1.7 proibe
                // suprimir, entao truncar e pior que ocupar duas linhas. As
                // numericas ficam de fora porque quebrar um valor no meio
                // produz um numero que nao existe.
                c.Texto(
                    celula.Encolhido(Campo.RecuoInterno, 0f, Campo.RecuoInterno, 0f),
                    valores[i],
                    e.ValorProduto,
                    numerica ? AlinhamentoH.Direita : AlinhamentoH.Esquerda,
                    AlinhamentoV.Topo,
                    quebrar: i == 0);
            }

            x += largura;
        }

        // MOC 3.1.7: o infAdProd sai imediatamente abaixo do seu item.
        //
        // A caixa de desenho tem a MESMA largura em que o texto foi medido.
        // Media-se a 204,5 mm e desenhava-se a 185,7 mm: qualquer linha entre
        // essas duas larguras era quebrada de novo aqui, virava uma linha a
        // mais do que MedirItens reservou, e - como a caixa era alta o
        // bastante para nao recortar - sobrescrevia a descricao do item
        // seguinte.
        if (m.LinhasInfoAdicional.Count > 0)
        {
            int linhasDoItem = Math.Max(1, Math.Max(m.LinhasDescricao.Count, m.LinhasCodigo.Count))
                + m.LinhasInfoAdicional.Count;

            float alturaPorLinha = alturaTexto / linhasDoItem;
            float yInfo = y + PadVertical + (alturaTexto - (m.LinhasInfoAdicional.Count * alturaPorLinha));

            c.Texto(
                new RetanguloMm(
                    DanfeMetricas.Esquerda + Campo.RecuoInterno,
                    yInfo,
                    LarguraInfoAdicional,
                    m.LinhasInfoAdicional.Count * alturaPorLinha),
                string.Join('\n', m.LinhasInfoAdicional),
                e.ValorProduto,
                AlinhamentoH.Esquerda,
                AlinhamentoV.Topo,
                quebrar: true);
        }
    }

    private static void DesenharRodape(
        Campo c, NfeDocumento nfe, IMedidorTexto medidor, EstilosDanfe e, float baseProdutos)
    {
        float y = baseProdutos;

        if (nfe.Issqn is { } issqn)
        {
            c.TituloBloco(
                new RetanguloMm(DanfeMetricas.Esquerda, y, DanfeMetricas.Largura, DanfeMetricas.AlturaTitulo),
                "Cálculo do ISSQN");
            y += DanfeMetricas.AlturaTitulo;

            float w = DanfeMetricas.LarguraIssqn;
            RetanguloMm[] l = Campo.Colunas(Faixa(y), w, w, w, 0f);

            c.Rotulado(l[0], "INSCRIÇÃO MUNICIPAL", nfe.Emitente.InscricaoMunicipal);
            c.Rotulado(l[1], "VALOR TOTAL DOS SERVIÇOS", Formatos.Moeda(issqn.ValorTotalServicos), AlinhamentoH.Direita);
            c.Rotulado(l[2], "BASE DE CÁLCULO DO ISSQN", Formatos.Moeda(issqn.BaseCalculo), AlinhamentoH.Direita);
            c.Rotulado(l[3], "VALOR DO ISSQN", Formatos.Moeda(issqn.ValorIssqn), AlinhamentoH.Direita);

            y += DanfeMetricas.AlturaLinha;
        }

        c.TituloBloco(
            new RetanguloMm(DanfeMetricas.Esquerda, y, DanfeMetricas.Largura, DanfeMetricas.AlturaTitulo),
            "Dados Adicionais");
        y += DanfeMetricas.AlturaTitulo;

        // A MESMA altura que AlturaRodape reservou - medir numa e desenhar
        // noutra e como o infAdProd se perdia.
        float alturaDados = AlturaDadosAdicionais(nfe, medidor, e);

        var info = new RetanguloMm(
            DanfeMetricas.Esquerda, y,
            DanfeMetricas.LarguraInfoComplementares, alturaDados);
        c.Moldura(info);

        var fisco = new RetanguloMm(
            info.Direita, y,
            DanfeMetricas.Direita - info.Direita, alturaDados);
        c.Moldura(fisco);

        c.Texto(
            info.Encolhido(Campo.RecuoInterno, 0.6f, Campo.RecuoInterno, 0f).FatiaSuperior(2.2f),
            "INFORMAÇÕES COMPLEMENTARES",
            e.Rotulo);

        c.Texto(
            fisco.Encolhido(Campo.RecuoInterno, 0.6f, Campo.RecuoInterno, 0f).FatiaSuperior(2.2f),
            "RESERVADO AO FISCO",
            e.Rotulo);

        string texto = TextosMoc.MontarInformacoesComplementares(nfe);

        if (texto.Length > 0)
        {
            c.Texto(
                info.Encolhido(Campo.RecuoInterno, 3f, Campo.RecuoInterno, 0.5f),
                texto,
                e.InfoComplementar,
                AlinhamentoH.Esquerda,
                AlinhamentoV.Topo,
                quebrar: true);
        }
    }

    private static RetanguloMm Faixa(float y) =>
        new(DanfeMetricas.Esquerda, y, DanfeMetricas.Largura, DanfeMetricas.AlturaLinha);
}
