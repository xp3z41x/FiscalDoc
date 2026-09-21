using FiscalDoc.Layout.DisplayList;

namespace FiscalDoc.Layout;

/// <summary>
/// Pagina de calibracao: o instrumento que prova que um milimetro no codigo e
/// um milimetro no papel.
///
/// Nao e um recurso do produto - e a verificacao da Fase 2 do plano. Sem ela,
/// "fidelidade milimetrica" seria uma afirmacao sem teste; com ela, qualquer
/// pessoa confere com uma regua de escola em dez segundos.
///
/// Tambem diagnostica o problema que nenhuma API resolve: se o driver estiver
/// com "ajustar a pagina" ligado, a regua de 100 mm sai com outro tamanho e o
/// desvio fica visivel e mensuravel.
/// </summary>
public static class PaginaCalibracao
{
    private static readonly EstiloTexto Rotulo = new(EstiloTexto.FamiliaPadrao, 8f);
    private static readonly EstiloTexto Titulo = new(EstiloTexto.FamiliaPadrao, 12f, Negrito: true);
    private static readonly EstiloTexto Nota = new(EstiloTexto.FamiliaPadrao, 9f);

    /// <summary>Margem da moldura externa, em mm.</summary>
    private const float MargemMoldura = 10f;

    public static ConjuntoPaginas Construir(TamanhoPapel papel)
    {
        var p = new List<Primitiva>();

        RetanguloMm moldura = papel.Folha.Encolhido(MargemMoldura);
        p.Add(new Primitiva.Contorno(moldura, 0.2f));

        p.Add(new Primitiva.Texto(
            "FiscalDoc - pagina de calibracao",
            new RetanguloMm(moldura.X, moldura.Y + 6f, moldura.Largura, 6f),
            Titulo,
            AlinhamentoH.Centro));

        p.Add(new Primitiva.Texto(
            $"Papel declarado: {papel.LarguraMm:0.#} x {papel.AlturaMm:0.#} mm. "
            + $"Moldura a {MargemMoldura:0.#} mm de cada borda.",
            new RetanguloMm(moldura.X, moldura.Y + 13f, moldura.Largura, 5f),
            Nota,
            AlinhamentoH.Centro));

        ReguaHorizontal(p, x0: 20f, y: 60f, comprimento: 100f);
        ReguaVertical(p, x: 20f, y0: 90f, comprimento: 100f);
        QuadradoDeControle(p, x: 60f, y: 95f, lado: 50f);
        EscadaDeEspessuras(p, x: 130f, y: 95f);
        EscadaDeFontes(p, x: 20f, y: 205f);

        p.Add(new Primitiva.Texto(
            "Confira com uma regua: a barra de 100 mm deve medir 100 mm, e o quadrado deve ter "
            + "50 mm de lado. Se medir menos, o driver da impressora esta reduzindo a pagina "
            + "(procure por \"ajustar a pagina\" / \"fit to page\" e desligue).",
            new RetanguloMm(20f, papel.AlturaMm - 40f, papel.LarguraMm - 40f, 20f),
            Nota,
            AlinhamentoH.Esquerda,
            AlinhamentoV.Topo,
            Quebrar: true));

        return ConjuntoPaginas.UmaPagina(p, papel, papel.Folha.Encolhido(MargemMoldura));
    }

    /// <summary>Regua de 100 mm com marca a cada 10 mm e meia-marca a cada 5 mm.</summary>
    private static void ReguaHorizontal(List<Primitiva> p, float x0, float y, float comprimento)
    {
        p.Add(new Primitiva.Linha(new PontoMm(x0, y), new PontoMm(x0 + comprimento, y), 0.3f));

        for (int mm = 0; mm <= (int)comprimento; mm++)
        {
            float altura = mm % 10 == 0 ? 5f : mm % 5 == 0 ? 3f : 1.5f;
            float x = x0 + mm;

            p.Add(new Primitiva.Linha(new PontoMm(x, y), new PontoMm(x, y - altura), 0.2f));

            if (mm % 10 == 0)
            {
                p.Add(new Primitiva.Texto(
                    mm.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    new RetanguloMm(x - 5f, y + 1f, 10f, 4f),
                    Rotulo,
                    AlinhamentoH.Centro));
            }
        }

        p.Add(new Primitiva.Texto(
            "100 mm na horizontal",
            new RetanguloMm(x0 + comprimento + 4f, y - 3f, 60f, 5f),
            Nota));
    }

    private static void ReguaVertical(List<Primitiva> p, float x, float y0, float comprimento)
    {
        p.Add(new Primitiva.Linha(new PontoMm(x, y0), new PontoMm(x, y0 + comprimento), 0.3f));

        for (int mm = 0; mm <= (int)comprimento; mm++)
        {
            float largura = mm % 10 == 0 ? 5f : mm % 5 == 0 ? 3f : 1.5f;
            float y = y0 + mm;

            p.Add(new Primitiva.Linha(new PontoMm(x, y), new PontoMm(x - largura, y), 0.2f));
        }

        p.Add(new Primitiva.Texto(
            "100 mm na vertical",
            new RetanguloMm(x + 2f, y0 + comprimento + 2f, 60f, 5f),
            Nota));
    }

    /// <summary>
    /// Quadrado perfeito: se a saida sair com DPI horizontal e vertical
    /// diferentes, ele vira retangulo e o erro fica obvio a olho nu.
    /// </summary>
    private static void QuadradoDeControle(List<Primitiva> p, float x, float y, float lado)
    {
        p.Add(new Primitiva.Contorno(new RetanguloMm(x, y, lado, lado), 0.3f));

        p.Add(new Primitiva.Linha(
            new PontoMm(x, y), new PontoMm(x + lado, y + lado), 0.15f));

        p.Add(new Primitiva.Texto(
            $"{lado:0} x {lado:0} mm",
            new RetanguloMm(x, y + (lado / 2f) - 2.5f, lado, 5f),
            Nota,
            AlinhamentoH.Centro));
    }

    /// <summary>
    /// Linhas de espessura decrescente. Mostra ate onde a impressora ainda
    /// resolve tracos finos - a moldura do DANFE depende disso.
    /// </summary>
    private static void EscadaDeEspessuras(List<Primitiva> p, float x, float y)
    {
        float[] espessuras = [0.5f, 0.35f, 0.25f, 0.2f, 0.15f, 0.1f, 0.05f];
        float linha = y;

        p.Add(new Primitiva.Texto(
            "Espessuras de traco",
            new RetanguloMm(x, linha - 6f, 60f, 5f),
            Nota));

        foreach (float e in espessuras)
        {
            p.Add(new Primitiva.Linha(new PontoMm(x, linha), new PontoMm(x + 40f, linha), e));

            p.Add(new Primitiva.Texto(
                $"{e:0.00} mm",
                new RetanguloMm(x + 42f, linha - 2f, 20f, 4f),
                Rotulo));

            linha += 7f;
        }
    }

    /// <summary>
    /// Os tamanhos minimos que o MOC Anexo II 3.7 exige, para conferir
    /// legibilidade na impressora de destino antes de confiar neles no DANFE.
    /// </summary>
    private static void EscadaDeFontes(List<Primitiva> p, float x, float y)
    {
        (float Pt, string Onde)[] tamanhos =
        [
            (5f, "3.7.1 descritivo de bloco / 3.7.2 coluna de produtos"),
            (6f, "3.7.3 descritivo de campo / 3.7.7 conteudo de produtos"),
            (8f, "3.7.4 texto do DANFE / 3.7.6 dados do emitente"),
            (10f, "3.7.4 serie e numero / 3.7.9 conteudo dos demais campos"),
            (12f, "3.7.4 a palavra DANFE / 3.7.6 razao social"),
        ];

        p.Add(new Primitiva.Texto(
            "Tamanhos minimos do MOC Anexo II, secao 3.7",
            new RetanguloMm(x, y - 6f, 120f, 5f),
            Nota.EmNegrito()));

        float linha = y;

        foreach ((float pt, string onde) in tamanhos)
        {
            var estilo = new EstiloTexto(EstiloTexto.FamiliaPadrao, pt);

            p.Add(new Primitiva.Texto(
                $"{pt:0} pt  ABCDEFG 0123456789 - {onde}",
                new RetanguloMm(x, linha, 170f, pt * 0.6f),
                estilo));

            linha += Math.Max(5f, pt * 0.55f);
        }
    }
}
