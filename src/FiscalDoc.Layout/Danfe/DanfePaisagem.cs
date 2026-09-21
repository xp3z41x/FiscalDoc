using FiscalDoc.Core.Model.Nfe;
using FiscalDoc.Core.Values;
using FiscalDoc.Layout.Barcode;
using FiscalDoc.Layout.Composition;
using FiscalDoc.Layout.DisplayList;

namespace FiscalDoc.Layout.Danfe;

/// <summary>
/// DANFE A4 paisagem (tpImp = 2), conforme MOC 7.0 Anexo II secao 3.8.2.
///
/// <para>E um layout proprio, nao o retrato girado. O MOC os publica como
/// modelos separados (Anexo III.02 e III.04) e as diferencas sao estruturais:
/// <list type="bullet">
/// <item>o canhoto vira uma tira <b>vertical na borda esquerda</b>, em vez de
/// uma faixa horizontal no topo;</item>
/// <item>as faixas de titulo de bloco viram <b>tarjas verticais de 5,1 mm</b> a
/// esquerda de cada bloco, em vez de barras horizontais de 4,2 mm;</item>
/// <item>a altura de linha cai de 8,5 mm para <b>6,4 mm</b>;</item>
/// <item>a area de campos comeca em 29,2 mm e tem 265,4 mm de largura.</item>
/// </list>
/// Tentar unificar os dois produziria mais condicionais do que codigo.</para>
///
/// <para>Duas regras de comportamento tambem mudam, e por isso nao aparecem
/// aqui: o 3.3.1 so permite mover o canhoto para a borda inferior em
/// <b>retrato</b>, e o 3.10.3 so permite encolher o quadro de produtos para
/// absorver margem de impressora em <b>retrato</b>.</para>
/// </summary>
public static class DanfePaisagem
{
    // ---- Geometria (MOC 3.8.2) --------------------------------------------

    /// <summary>Tira do canhoto, girada, na borda esquerda.</summary>
    private const float CanhotoEsquerda = 1.3f;

    private const float CanhotoDireita = 24.1f;

    /// <summary>Tarja vertical de titulo de bloco: 0,51 cm.</summary>
    private const float TituloX = 24.1f;

    private const float TituloLargura = 5.1f;

    /// <summary>Largura util do infAdProd - a mesma em que ele e medido.</summary>
    private const float LarguraInfoAdicional = Largura - (2 * Campo.RecuoInterno);

    /// <summary>Area de campos: 2,92 cm ate 29,46 cm.</summary>
    private const float Esquerda = 29.2f;

    private const float Direita = 294.6f;

    private const float Largura = Direita - Esquerda;

    private const float Topo = 4.7f;

    private const float Base = 205.3f;

    /// <summary>Altura de linha em paisagem: 0,64 cm, contra 0,85 no retrato.</summary>
    private const float AlturaLinha = 6.4f;

    private const float AlturaEmitente = 30.0f;

    private const float AlturaQuadroBarras = 12.0f;

    private const float AlturaConsulta = 11.6f;

    private const float LarguraEmitente = 118.0f;

    private const float LarguraQuadroDanfe = 30.0f;

    private const float AlturaDadosAdicionais = 24.0f;

    private const float AlturaCabecalhoProdutos = 5.4f;

    private const float PadVertical = 0.35f;

    /// <summary>
    /// As larguras de campo do retrato reaproveitadas em escala. O MOC 2.16 do
    /// CT-e descreve exatamente esta operacao para o DACTE - "alargamento
    /// proporcional das larguras e reducao das alturas" - e o mesmo raciocinio
    /// vale aqui, onde a area util passa de 205,7 mm para 265,4 mm.
    /// </summary>
    private const float EscalaLargura = Largura / DanfeMetricas.Largura;

    private static float Esc(float larguraRetrato) => larguraRetrato * EscalaLargura;

    public static ConjuntoPaginas Construir(
        NfeDocumento nfe,
        IMedidorTexto medidor,
        EstilosDanfe? estilos = null)
    {
        ArgumentNullException.ThrowIfNull(nfe);
        ArgumentNullException.ThrowIfNull(medidor);

        EstilosDanfe e = estilos ?? new EstilosDanfe();
        TamanhoPapel papel = TamanhoPapel.A4.Girado;

        List<ItemMedido> itens = MedirItens(nfe, medidor, e);
        List<List<ItemMedido>> porPagina = Paginar(nfe, itens);

        int total = porPagina.Count;
        var paginas = new List<Pagina>(total);

        for (int i = 0; i < total; i++)
        {
            bool primeira = i == 0;
            bool ultima = i == total - 1;

            var saida = new List<Primitiva>(512);
            var c = new Campo(saida, e);

            if (primeira)
            {
                DesenharCanhoto(c, nfe, e);
            }

            float y = DesenharCabecalho(c, nfe, e, medidor, Topo, i + 1, total);

            if (primeira)
            {
                y = DesenharDestinatario(c, nfe, e, y);
                y = DesenharFatura(c, nfe, e, y);
                y = DesenharImpostoETransporte(c, nfe, e, y);
            }

            float alturaRodape = ultima ? AlturaRodape(nfe) : 0f;
            float baseProdutos = Base - alturaRodape;

            if (porPagina[i].Count > 0)
            {
                DesenharProdutos(c, porPagina[i], e, y, baseProdutos);
            }

            if (ultima)
            {
                DesenharRodape(c, nfe, e, baseProdutos);
            }

            paginas.Add(new Pagina(i + 1, saida));
        }

        return new ConjuntoPaginas(
            paginas,
            papel,
            RetanguloMm.DeCantos(CanhotoEsquerda, Topo, Direita, Base));
    }

    private sealed record ItemMedido(
        ItemNfe Item,
        IReadOnlyList<string> LinhasDescricao,
        IReadOnlyList<string> LinhasInfoAdicional,
        float AlturaMm);

    private static List<ItemMedido> MedirItens(
        NfeDocumento nfe, IMedidorTexto medidor, EstilosDanfe e)
    {
        float alturaLinhaTexto = medidor.AlturaLinhaMm(e.ValorProduto);
        float larguraDescricao = Esc(DanfeMetricas.ColunasProduto[1].Largura) - (2 * Campo.RecuoInterno);
        float larguraTotal = Largura - (2 * Campo.RecuoInterno);

        var medidos = new List<ItemMedido>(nfe.Itens.Count);

        foreach (ItemNfe item in nfe.Itens)
        {
            IReadOnlyList<string> descricao = medidor.Quebrar(
                item.Descricao ?? string.Empty, e.ValorProduto, larguraDescricao);

            IReadOnlyList<string> info = item.TemInformacaoAdicional
                ? medidor.Quebrar(item.InformacaoAdicional!, e.ValorProduto, larguraTotal)
                : [];

            int linhas = Math.Max(1, descricao.Count) + info.Count;

            medidos.Add(new ItemMedido(
                item, descricao, info, (linhas * alturaLinhaTexto) + (2 * PadVertical)));
        }

        return medidos;
    }

    private static List<List<ItemMedido>> Paginar(NfeDocumento nfe, List<ItemMedido> itens)
    {
        float topoPrimeira = Topo + AlturaEmitente + (2 * AlturaLinha)
            + (3 * AlturaLinha)          // destinatario
            + AlturaLinha                // fatura / duplicatas
            + (2 * AlturaLinha)          // calculo do imposto
            + (nfe.IbsCbs is null ? 0f : AlturaLinha)   // IBS/CBS, se houver
            + (3 * AlturaLinha)          // transportador
            + AlturaCabecalhoProdutos;

        float topoSeguinte = Topo + AlturaEmitente + (2 * AlturaLinha) + AlturaCabecalhoProdutos;

        var paginas = new List<List<ItemMedido>>();
        var atual = new List<ItemMedido>();

        float y = topoPrimeira;

        foreach (ItemMedido m in itens)
        {
            if (y + m.AlturaMm > Base && atual.Count > 0)
            {
                paginas.Add(atual);
                atual = [];
                y = topoSeguinte;
            }

            atual.Add(m);
            y += m.AlturaMm;
        }

        paginas.Add(atual);

        // O rodape fica na ultima pagina e precisa caber la. Como os itens
        // foram empilhados ate a borda, pode nao caber: nesse caso empurra-se
        // o fim da lista para uma folha nova, um item por vez.
        //
        // Antes o rodape ganhava uma folha SO PARA ELE, sem nem o quadro de
        // produtos - o DANFE paisagem de 27 itens do corpus saia em 2 folhas
        // com a segunda praticamente em branco. Mesmo criterio do retrato.
        float rodape = AlturaRodape(nfe);

        while (paginas[^1].Count > 1)
        {
            float topoUltima = paginas.Count == 1 ? topoPrimeira : topoSeguinte;

            if (topoUltima + Altura(paginas[^1]) + rodape <= Base)
            {
                break;
            }

            List<ItemMedido> ultima = paginas[^1];
            ItemMedido empurrado = ultima[^1];
            ultima.RemoveAt(ultima.Count - 1);
            paginas.Add([empurrado]);
        }

        float usadoNaUltima =
            (paginas.Count == 1 ? topoPrimeira : topoSeguinte) + Altura(paginas[^1]);

        if (usadoNaUltima + rodape > Base)
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

    /// <summary>
    /// Fatura e duplicatas (MOC 3.3.2).
    ///
    /// <para>Este bloco simplesmente <b>nao existia</b> no paisagem: o modelo
    /// trazia <c>Fatura</c> e <c>Duplicatas</c> lidas do arquivo e nada as
    /// imprimia. Dois dos arquivos reais do corpus sao tpImp 2 com tres
    /// duplicatas cada - uma venda a prazo saia sem numero, sem vencimento e
    /// sem valor. O MOC so permite suprimir o quadro quando nao ha
    /// duplicata.</para>
    /// </summary>
    private static float DesenharFatura(Campo c, NfeDocumento nfe, EstilosDanfe e, float y)
    {
        var partes = new List<string>();

        if (nfe.Fatura is { } f && !string.IsNullOrWhiteSpace(f.Numero))
        {
            partes.Add($"Fat. {f.Numero}  {Formatos.Moeda(f.ValorLiquido ?? f.ValorOriginal)}");
        }

        foreach (Duplicata d in nfe.Duplicatas)
        {
            partes.Add($"{d.Numero}  {Formatos.Data(d.Vencimento)}  {Formatos.Moeda(d.Valor)}");
        }

        // Em paisagem o titulo de bloco e uma aba vertical de 5,1 mm na borda
        // esquerda, nao uma faixa horizontal (MOC 3.8.2) - mesmo idioma dos
        // blocos vizinhos.
        // A tarja vertical de um bloco de UMA linha tem so ~5,2 mm de
        // comprimento util, e TituloVertical nao quebra: "Fatura / Duplicatas"
        // mede 20 mm e sairia como "FATU". Bloco curto, rotulo curto.
        Titulo(c, y, AlturaLinha, "Fat.");

        RetanguloMm faixa = Faixa(y);
        c.Moldura(faixa);

        if (partes.Count > 0)
        {
            c.Texto(
                faixa.Encolhido(Campo.RecuoInterno, 1f, Campo.RecuoInterno, 0.5f),
                string.Join("      ", partes),
                e.ValorProduto,
                AlinhamentoH.Esquerda,
                AlinhamentoV.Meio);
        }

        return y + AlturaLinha;
    }

    private static float AlturaRodape(NfeDocumento nfe) =>
        (nfe.Issqn is not null ? AlturaLinha : 0f) + AlturaDadosAdicionais;

    // =====================================================================

    /// <summary>
    /// Canhoto girado 90 graus na borda esquerda (MOC 3.8.2). Em paisagem ele
    /// nunca vai para a borda inferior - o 3.3.1 so permite isso em retrato.
    /// </summary>
    private static void DesenharCanhoto(Campo c, NfeDocumento nfe, EstilosDanfe e)
    {
        var tira = RetanguloMm.DeCantos(CanhotoEsquerda, Topo, CanhotoDireita, Base);

        // Caixa de numero e serie, no alto da tira.
        var numero = new RetanguloMm(tira.X, tira.Y, tira.Largura, 45.3f);
        c.Moldura(numero);
        c.TituloVertical(numero, $"NF-e  N. {Formatos.NumeroNota(nfe.Ide.Numero)}  "
            + $"SÉRIE {Formatos.Serie(nfe.Ide.Serie)}");

        // Recibo, ocupando o resto da tira.
        var recibo = RetanguloMm.DeCantos(tira.X, numero.Base, tira.Direita, tira.Base);
        c.Moldura(recibo);

        float metade = recibo.Largura / 2f;

        var assinatura = new RetanguloMm(recibo.X, recibo.Y, metade, recibo.Altura * 0.62f);
        c.TituloVertical(
            assinatura,
            $"RECEBEMOS DE {nfe.Emitente.RazaoSocial?.ToUpperInvariant()} "
            + "OS PRODUTOS CONSTANTES DA NOTA FISCAL AO LADO");

        var dataReceb = new RetanguloMm(
            recibo.X + metade, recibo.Y, metade, recibo.Altura * 0.62f);
        c.TituloVertical(dataReceb, "DATA DE RECEBIMENTO");

        var identificacao = new RetanguloMm(
            recibo.X, assinatura.Base, recibo.Largura, recibo.Base - assinatura.Base);
        c.Moldura(identificacao);
        c.TituloVertical(identificacao, "IDENTIFICAÇÃO E ASSINATURA DO RECEBEDOR");
    }

    private static float DesenharCabecalho(
        Campo c,
        NfeDocumento nfe,
        EstilosDanfe e,
        IMedidorTexto medidor,
        float y,
        int pagina,
        int total)
    {
        var emit = new RetanguloMm(Esquerda, y, LarguraEmitente, AlturaEmitente);
        c.Moldura(emit);

        RetanguloMm dentro = emit.Encolhido(2f);

        // Mesmo criterio do retrato: centralizado e sem quebra, um nome
        // comprido transborda para os dois lados e o recorte come o comeco e o
        // fim. O MOC 3.7 fixa 10 pt como minimo de conteudo, entao a saida e
        // quebrar, nao reduzir.
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
        c.Texto(
            dentro.SemFatiaSuperior(6.5f),
            $"{en.LinhaLogradouro}\n{en.Bairro} - CEP: {Formatos.Cep(en.Cep)}\n"
            + $"{en.Municipio} - {en.Uf}"
            + (string.IsNullOrWhiteSpace(en.Fone) ? string.Empty : $"\nFone: {Formatos.Telefone(en.Fone)}"),
            e.EmitenteDados,
            AlinhamentoH.Centro,
            AlinhamentoV.Topo,
            quebrar: true);

        var quadro = new RetanguloMm(emit.Direita, y, LarguraQuadroDanfe, AlturaEmitente);
        c.Moldura(quadro);
        DesenharQuadroDanfe(c, nfe, e, quadro, pagina, total);

        float x3 = quadro.Direita;
        float larg3 = Direita - x3;

        var caixaBarras = new RetanguloMm(x3, y, larg3, AlturaQuadroBarras);
        c.Moldura(caixaBarras);

        if (nfe.Chave is not null)
        {
            IReadOnlyList<int> modulos = Code128C.Codificar(nfe.Chave.Digitos);
            c.Barras(
                caixaBarras.Encolhido(1.5f, 1.2f, 1.5f, 1.2f),
                modulos,
                DanfeMetricas.ModuloMinimoBarras);
        }

        var caixaChave = new RetanguloMm(x3, caixaBarras.Base, larg3, AlturaLinha);
        c.Rotulado(caixaChave, "CHAVE DE ACESSO", nfe.Chave?.Formatada,
            AlinhamentoH.Centro, valorEmNegrito: true);

        var caixaConsulta = new RetanguloMm(x3, caixaChave.Base, larg3, AlturaConsulta);
        c.Moldura(caixaConsulta);
        c.Texto(
            caixaConsulta.Encolhido(1.5f),
            TextosMoc.Consulta(nfe.Ide.TipoEmissao),
            e.DescricaoDanfe,
            AlinhamentoH.Centro,
            AlinhamentoV.Meio,
            quebrar: true);

        float yLinha2 = Math.Max(emit.Base, caixaConsulta.Base);

        var natureza = new RetanguloMm(Esquerda, yLinha2, Esc(78.7f), AlturaLinha);
        c.Rotulado(natureza, "NATUREZA DA OPERAÇÃO", nfe.Ide.NaturezaOperacao);

        var protocolo = new RetanguloMm(
            natureza.Direita, yLinha2, Direita - natureza.Direita, AlturaLinha);
        c.Rotulado(
            protocolo,
            TextosMoc.RotuloProtocolo(nfe.Ide.TipoEmissao),
            TextoProtocolo(nfe),
            AlinhamentoH.Centro);

        float yIe = yLinha2 + AlturaLinha;
        RetanguloMm[] ie = Campo.Colunas(
            new RetanguloMm(Esquerda, yIe, Largura, AlturaLinha),
            Esc(68.6f), Esc(68.6f), 0f);

        c.Rotulado(ie[0], "INSCRIÇÃO ESTADUAL", nfe.Emitente.InscricaoEstadual);
        c.Rotulado(ie[1], "INSC. ESTADUAL DO SUBST. TRIBUTÁRIO", nfe.Emitente.InscricaoEstadualSt);
        c.Rotulado(ie[2], "CNPJ", Formatos.CnpjOuCpf(nfe.Emitente.Documento));

        return yIe + AlturaLinha;
    }

    private static void DesenharQuadroDanfe(
        Campo c, NfeDocumento nfe, EstilosDanfe e, RetanguloMm quadro, int pagina, int total)
    {
        RetanguloMm d = quadro.Encolhido(1f);
        float y = d.Y + 0.5f;

        c.Texto(new RetanguloMm(d.X, y, d.Largura, 5f), "DANFE", e.PalavraDanfe, AlinhamentoH.Centro);
        y += 5.5f;

        c.Texto(
            new RetanguloMm(d.X, y, d.Largura, 5f),
            "Documento Auxiliar da Nota Fiscal Eletrônica",
            e.DescricaoDanfe, AlinhamentoH.Centro, AlinhamentoV.Topo, quebrar: true);
        y += 5.5f;

        c.Texto(new RetanguloMm(d.X, y, d.Largura - 5f, 3f), "0 - ENTRADA", e.DescricaoDanfe);
        c.Texto(new RetanguloMm(d.X, y + 3f, d.Largura - 5f, 3f), "1 - SAÍDA", e.DescricaoDanfe);

        var caixaTipo = new RetanguloMm(d.Direita - 4.5f, y + 0.5f, 4f, 5f);
        c.Moldura(caixaTipo);
        c.Texto(caixaTipo, ((int)nfe.Ide.TipoOperacao).ToString(
            System.Globalization.CultureInfo.InvariantCulture),
            e.NumeroSerie, AlinhamentoH.Centro, AlinhamentoV.Meio);

        y += 7f;

        c.Texto(new RetanguloMm(d.X, y, d.Largura, 3.6f),
            $"N. {Formatos.NumeroNota(nfe.Ide.Numero)}", e.NumeroSerie, AlinhamentoH.Centro);
        y += 3.6f;

        c.Texto(new RetanguloMm(d.X, y, d.Largura, 3.6f),
            $"SÉRIE {Formatos.Serie(nfe.Ide.Serie)}", e.NumeroSerie, AlinhamentoH.Centro);
        y += 3.6f;

        // MOC 3.10.2: em toda folha, inclusive a primeira.
        c.Texto(new RetanguloMm(d.X, y, d.Largura, 3.6f),
            $"FOLHA {pagina:00}/{total:00}", e.NumeroSerie, AlinhamentoH.Centro);
    }

    private static string TextoProtocolo(NfeDocumento nfe)
    {
        if (nfe.Protocolo?.Numero is null)
        {
            return string.Empty;
        }

        string dh = Formatos.DataHora(nfe.Protocolo.DataHoraRecebimento);
        return string.IsNullOrEmpty(dh) ? nfe.Protocolo.Numero : $"{nfe.Protocolo.Numero} - {dh}";
    }

    /// <summary>
    /// Tarja vertical de titulo a esquerda do bloco (MOC 3.8.2).
    ///
    /// Os rotulos aqui sao mais curtos que os do retrato porque a tarja tem a
    /// altura do <b>bloco</b>: o de calculo do imposto tem duas linhas, ou seja
    /// 12,8 mm, e "CALCULO DO IMPOSTO" a 5 pt nao cabe nesse espaco. Abreviar e
    /// preferivel a cortar a palavra ou a diminuir a fonte abaixo do minimo de
    /// 5 pt que o MOC 3.7.1 exige.
    /// </summary>
    private static void Titulo(Campo c, float y, float altura, string texto)
    {
        var tarja = new RetanguloMm(TituloX, y, TituloLargura, altura);
        c.Moldura(tarja);
        c.TituloVertical(tarja, texto);
    }

    private static float DesenharDestinatario(Campo c, NfeDocumento nfe, EstilosDanfe e, float y)
    {
        Titulo(c, y, 3 * AlturaLinha, "Destinatário");

        Destinatario d = nfe.Destinatario;
        Endereco en = d.Endereco;

        RetanguloMm[] l1 = Campo.Colunas(Faixa(y), Esc(123.2f), Esc(53.3f), 0f);
        c.Rotulado(l1[0], "NOME / RAZÃO SOCIAL", d.RazaoSocial);
        c.Rotulado(l1[1], "CNPJ / CPF", Formatos.CnpjOuCpf(d.Documento));
        c.Rotulado(l1[2], "DATA DA EMISSÃO", Formatos.Data(nfe.Ide.DataHoraEmissao));
        y += AlturaLinha;

        RetanguloMm[] l2 = Campo.Colunas(Faixa(y), Esc(101.6f), Esc(48.3f), Esc(26.7f), 0f);
        c.Rotulado(l2[0], "ENDEREÇO", en.LinhaLogradouro);
        c.Rotulado(l2[1], "BAIRRO / DISTRITO", en.Bairro);
        c.Rotulado(l2[2], "CEP", Formatos.Cep(en.Cep));
        c.Rotulado(l2[3], "DATA DA SAÍDA / ENTRADA", Formatos.Data(nfe.Ide.DataHoraSaidaEntrada));
        y += AlturaLinha;

        RetanguloMm[] l3 = Campo.Colunas(
            Faixa(y), Esc(71.1f), Esc(40.6f), Esc(11.4f), Esc(53.3f), 0f);
        c.Rotulado(l3[0], "MUNICÍPIO", en.Municipio);
        c.Rotulado(l3[1], "FONE / FAX", Formatos.Telefone(en.Fone));
        c.Rotulado(l3[2], "UF", en.Uf, AlinhamentoH.Centro);
        c.Rotulado(l3[3], "INSCRIÇÃO ESTADUAL", d.InscricaoEstadual);
        c.Rotulado(l3[4], "HORA DE SAÍDA / ENTRADA", Formatos.Hora(nfe.Ide.DataHoraSaidaEntrada));

        return y + AlturaLinha;
    }

    private static float DesenharImpostoETransporte(
        Campo c, NfeDocumento nfe, EstilosDanfe e, float y)
    {
        Titulo(c, y, 2 * AlturaLinha, "Imposto");

        TotaisIcms t = nfe.Totais;

        RetanguloMm[] i1 = Campo.Colunas(
            Faixa(y), Esc(40.6f), Esc(40.6f), Esc(40.6f), Esc(40.6f), 0f);
        c.Rotulado(i1[0], "BASE DE CÁLCULO DO ICMS", Formatos.Moeda(t.BaseCalculoIcms), AlinhamentoH.Direita);
        c.Rotulado(i1[1], "VALOR DO ICMS", Formatos.Moeda(t.ValorIcms), AlinhamentoH.Direita);
        c.Rotulado(i1[2], "BASE DE CÁLCULO DO ICMS ST", Formatos.Moeda(t.BaseCalculoIcmsSt), AlinhamentoH.Direita);
        c.Rotulado(i1[3], "VALOR DO ICMS ST", Formatos.Moeda(t.ValorIcmsSt), AlinhamentoH.Direita);
        c.Rotulado(i1[4], "VALOR TOTAL DOS PRODUTOS", Formatos.Moeda(t.ValorTotalProdutos), AlinhamentoH.Direita);
        y += AlturaLinha;

        RetanguloMm[] i2 = Campo.Colunas(
            Faixa(y), Esc(33f), Esc(33f), Esc(33f), Esc(33f), Esc(33f), 0f);
        c.Rotulado(i2[0], "VALOR DO FRETE", Formatos.Moeda(t.ValorFrete), AlinhamentoH.Direita);
        c.Rotulado(i2[1], "VALOR DO SEGURO", Formatos.Moeda(t.ValorSeguro), AlinhamentoH.Direita);
        c.Rotulado(i2[2], "DESCONTO", Formatos.Moeda(t.ValorDesconto), AlinhamentoH.Direita);
        c.Rotulado(i2[3], "OUTRAS DESPESAS", Formatos.Moeda(t.OutrasDespesas), AlinhamentoH.Direita);
        c.Rotulado(i2[4], "VALOR DO IPI", Formatos.Moeda(t.ValorIpi), AlinhamentoH.Direita);
        c.Rotulado(i2[5], "VALOR TOTAL DA NOTA", Formatos.Moeda(t.ValorTotalNota),
            AlinhamentoH.Direita, valorEmNegrito: true);
        y += AlturaLinha;

        // IBS/CBS. Ver a nota extensa em DanfeRetrato.DesenharIbsCbs: o MOC
        // nao tem quadro para estes valores, e o bloco e acrescimo. Aqui ele
        // ganha tarja vertical, como todo bloco em paisagem.
        if (nfe.IbsCbs is { } ibs)
        {
            Titulo(c, y, AlturaLinha, "Reforma");

            var campos = new List<(string Rotulo, string Valor, bool Destaque, float Peso)>
            {
                ("BASE DE CÁLCULO IBS/CBS", Formatos.Moeda(ibs.BaseCalculo), false, 1.5f),
                ("IBS ESTADUAL", Formatos.Moeda(ibs.ValorIbsUf), false, 1.0f),
                ("IBS MUNICIPAL", Formatos.Moeda(ibs.ValorIbsMun), false, 1.0f),
                ("(+) IBS R$", Formatos.Moeda(ibs.ValorIbs), true, 1.0f),
                ("(+) CBS R$", Formatos.Moeda(ibs.ValorCbs), true, 1.0f),
            };

            if (ibs.TemImpostoSeletivo)
            {
                campos.Add(("(+) IS R$", Formatos.Moeda(ibs.ValorIs), true, 1.0f));
            }

            if (ibs.TotalDivergeDoValorDaNota(nfe.Totais.ValorTotalNota))
            {
                campos.Add((
                    "TOTAL COM IBS/CBS/IS",
                    Formatos.Moeda(ibs.ValorTotalNotaComTributos),
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
                larguras[i] = Largura * (campos[i].Peso / pesoTotal);
            }

            RetanguloMm[] r = Campo.Colunas(Faixa(y), larguras);

            for (int i = 0; i < campos.Count; i++)
            {
                (string rotulo, string valor, bool destaque, _) = campos[i];
                c.Rotulado(r[i], rotulo, valor, AlinhamentoH.Direita, destaque);
            }

            y += AlturaLinha;
        }

        Titulo(c, y, 3 * AlturaLinha, "Transportador");

        Transportador? tr = nfe.Transportador;
        Veiculo? v = nfe.Veiculo;

        RetanguloMm[] t1 = Campo.Colunas(
            Faixa(y), Esc(90.2f), Esc(27.9f), Esc(17.8f), Esc(22.9f), Esc(7.6f), 0f);
        c.Rotulado(t1[0], "NOME / RAZÃO SOCIAL", tr?.RazaoSocial);
        c.Rotulado(t1[1], "FRETE POR CONTA", Rotulos.Frete(nfe.Frete));
        c.Rotulado(t1[2], "CÓDIGO ANTT", v?.Rntc);
        c.Rotulado(t1[3], "PLACA DO VEÍCULO", v?.Placa);
        c.Rotulado(t1[4], "UF", v?.Uf, AlinhamentoH.Centro);
        c.Rotulado(t1[5], "CNPJ / CPF", Formatos.CnpjOuCpf(tr?.Documento));
        y += AlturaLinha;

        RetanguloMm[] t2 = Campo.Colunas(Faixa(y), Esc(90.2f), Esc(68.6f), Esc(7.6f), 0f);
        c.Rotulado(t2[0], "ENDEREÇO", tr?.EnderecoCompleto);
        c.Rotulado(t2[1], "MUNICÍPIO", tr?.Municipio);
        c.Rotulado(t2[2], "UF", tr?.Uf, AlinhamentoH.Centro);
        c.Rotulado(t2[3], "INSCRIÇÃO ESTADUAL", tr?.InscricaoEstadual);
        y += AlturaLinha;

        // A faixa comporta um volume. Com varios grupos <vol>, imprime-se o
        // PRIMEIRO inteiro - nao a soma. Somar quantidade e peso seria
        // imprimir um numero ausente do arquivo (MOC 3.1) ao lado da especie
        // e da marca de um volume so. Mesmo criterio do retrato.
        Volume? vol = nfe.Volumes.Count > 0 ? nfe.Volumes[0] : null;
        decimal? qtd = vol?.Quantidade;
        decimal? pesoB = vol?.PesoBruto;
        decimal? pesoL = vol?.PesoLiquido;

        RetanguloMm[] t3 = Campo.Colunas(
            Faixa(y), Esc(29.2f), Esc(30.5f), Esc(30.5f), Esc(48.3f), Esc(34.3f), 0f);
        c.Rotulado(t3[0], "QUANTIDADE", Formatos.Quantidade(qtd), AlinhamentoH.Direita);
        c.Rotulado(t3[1], "ESPÉCIE", vol?.Especie);
        c.Rotulado(t3[2], "MARCA", vol?.Marca);
        c.Rotulado(t3[3], "NUMERAÇÃO", vol?.Numeracao);
        c.Rotulado(t3[4], "PESO BRUTO", Formatos.Quantidade(pesoB), AlinhamentoH.Direita);
        c.Rotulado(t3[5], "PESO LÍQUIDO", Formatos.Quantidade(pesoL), AlinhamentoH.Direita);

        return y + AlturaLinha;
    }

    private static void DesenharProdutos(
        Campo c, List<ItemMedido> itens, EstilosDanfe e, float y, float baseMaxima)
    {
        float altura = baseMaxima - y;
        if (altura < DanfeMetricas.AlturaMinimaProdutos)
        {
            return;
        }

        Titulo(c, y, altura, "Produtos / Serviços");

        var quadro = new RetanguloMm(Esquerda, y, Largura, altura);
        c.Moldura(quadro);

        float x = Esquerda;

        foreach ((string titulo, float larguraRetrato, _) in DanfeMetricas.ColunasProduto)
        {
            float largura = Esc(larguraRetrato);
            var celula = new RetanguloMm(x, y, largura, AlturaCabecalhoProdutos);

            c.Texto(celula.Encolhido(0.4f, 0.3f, 0.4f, 0.3f), titulo, e.ColunaProduto,
                AlinhamentoH.Centro, AlinhamentoV.Meio, quebrar: true);

            if (x > Esquerda)
            {
                c.Linha(x, y, x, quadro.Base);
            }

            x += largura;
        }

        float yCab = y + AlturaCabecalhoProdutos;
        c.Linha(Esquerda, yCab, Direita, yCab);

        float yItem = yCab;

        foreach (ItemMedido m in itens)
        {
            if (yItem + m.AlturaMm > quadro.Base)
            {
                break;
            }

            DesenharItem(c, m, e, yItem);
            yItem += m.AlturaMm;

            if (yItem < quadro.Base)
            {
                c.Linha(Esquerda, yItem, Direita, yItem, 0.05f);
            }
        }
    }

    private static void DesenharItem(Campo c, ItemMedido m, EstilosDanfe e, float y)
    {
        ItemNfe it = m.Item;
        IcmsItem icms = it.Icms;

        string[] valores =
        [
            it.Codigo ?? string.Empty,
            string.Empty,
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
        float x = Esquerda;

        for (int i = 0; i < DanfeMetricas.ColunasProduto.Length; i++)
        {
            (_, float larguraRetrato, bool numerica) = DanfeMetricas.ColunasProduto[i];
            float largura = Esc(larguraRetrato);
            var celula = new RetanguloMm(x, y + PadVertical, largura, alturaTexto);

            c.Texto(
                celula.Encolhido(Campo.RecuoInterno, 0f, Campo.RecuoInterno, 0f),
                i == 1 ? string.Join('\n', m.LinhasDescricao) : valores[i],
                e.ValorProduto,
                numerica ? AlinhamentoH.Direita : AlinhamentoH.Esquerda,
                AlinhamentoV.Topo,
                quebrar: i == 1);

            x += largura;
        }

        // MOC 3.1.7: o infAdProd sai imediatamente abaixo do seu item.
        //
        // O paisagem MEDIA o infAdProd e somava a altura dele em cada linha,
        // mas nunca o desenhava: o documento reservava espaco em branco onde
        // a informacao adicional do produto deveria estar. Mesmo tratamento do
        // retrato, com a largura de desenho igual a de medicao.
        if (m.LinhasInfoAdicional.Count > 0)
        {
            int linhasDoItem = Math.Max(1, m.LinhasDescricao.Count) + m.LinhasInfoAdicional.Count;
            float alturaPorLinha = alturaTexto / linhasDoItem;
            float yInfo = y + PadVertical + (alturaTexto - (m.LinhasInfoAdicional.Count * alturaPorLinha));

            c.Texto(
                new RetanguloMm(
                    Esquerda + Campo.RecuoInterno,
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

    private static void DesenharRodape(Campo c, NfeDocumento nfe, EstilosDanfe e, float y)
    {
        if (nfe.Issqn is { } issqn)
        {
            Titulo(c, y, AlturaLinha, "ISSQN");

            RetanguloMm[] l = Campo.Colunas(
                Faixa(y), Esc(50.8f), Esc(50.8f), Esc(50.8f), 0f);
            c.Rotulado(l[0], "INSCRIÇÃO MUNICIPAL", nfe.Emitente.InscricaoMunicipal);
            c.Rotulado(l[1], "VALOR TOTAL DOS SERVIÇOS", Formatos.Moeda(issqn.ValorTotalServicos), AlinhamentoH.Direita);
            c.Rotulado(l[2], "BASE DE CÁLCULO DO ISSQN", Formatos.Moeda(issqn.BaseCalculo), AlinhamentoH.Direita);
            c.Rotulado(l[3], "VALOR DO ISSQN", Formatos.Moeda(issqn.ValorIssqn), AlinhamentoH.Direita);

            y += AlturaLinha;
        }

        Titulo(c, y, AlturaDadosAdicionais, "Dados Adicionais");

        var info = new RetanguloMm(Esquerda, y, Esc(129.5f), AlturaDadosAdicionais);
        c.Moldura(info);

        var fisco = new RetanguloMm(info.Direita, y, Direita - info.Direita, AlturaDadosAdicionais);
        c.Moldura(fisco);

        c.Texto(info.Encolhido(Campo.RecuoInterno, 0.6f, Campo.RecuoInterno, 0f).FatiaSuperior(2.2f),
            "INFORMAÇÕES COMPLEMENTARES", e.Rotulo);
        c.Texto(fisco.Encolhido(Campo.RecuoInterno, 0.6f, Campo.RecuoInterno, 0f).FatiaSuperior(2.2f),
            "RESERVADO AO FISCO", e.Rotulo);

        string texto = TextosMoc.MontarInformacoesComplementares(nfe);

        if (texto.Length > 0)
        {
            c.Texto(
                info.Encolhido(Campo.RecuoInterno, 3f, Campo.RecuoInterno, 0.5f),
                texto, e.InfoComplementar,
                AlinhamentoH.Esquerda, AlinhamentoV.Topo, quebrar: true);
        }
    }

    private static RetanguloMm Faixa(float y) => new(Esquerda, y, Largura, AlturaLinha);
}
