using FiscalDoc.Layout.Barcode;
using FiscalDoc.Layout.DisplayList;

namespace FiscalDoc.Layout.Composition;

/// <summary>
/// Monta os elementos repetitivos do DANFE: caixa com rotulo pequeno em cima e
/// valor embaixo, faixa de titulo de bloco, linha divisoria.
///
/// Existe para que <c>DanfeRetrato</c> fale em campos e blocos, e nao em
/// retangulos e textos soltos. Sem isso, o layout viraria centenas de chamadas
/// quase iguais e o erro de um pixel em uma delas passaria despercebido.
/// </summary>
public sealed class Campo
{
    /// <summary>
    /// Espessura da moldura dos quadros. Fina o bastante para nao engordar o
    /// desenho a 600 dpi e grossa o bastante para sobreviver a 300 dpi.
    /// </summary>
    public const float EspessuraMoldura = 0.15f;

    /// <summary>Recuo interno do texto em relacao a moldura.</summary>
    public const float RecuoInterno = 0.6f;

    private readonly List<Primitiva> _saida;
    private readonly EstilosDanfe _estilos;

    public Campo(List<Primitiva> saida, EstilosDanfe estilos)
    {
        _saida = saida;
        _estilos = estilos;
    }

    /// <summary>So a moldura, sem conteudo.</summary>
    public void Moldura(RetanguloMm caixa) =>
        _saida.Add(new Primitiva.Contorno(caixa, EspessuraMoldura));

    /// <summary>
    /// Campo comum: rotulo em cima (6 pt, MOC 3.7.3) e valor embaixo.
    /// </summary>
    public void Rotulado(
        RetanguloMm caixa,
        string rotulo,
        string? valor,
        AlinhamentoH alinhamentoValor = AlinhamentoH.Esquerda,
        bool valorEmNegrito = false,
        bool comMoldura = true)
    {
        if (comMoldura)
        {
            Moldura(caixa);
        }

        RetanguloMm interno = caixa.Encolhido(RecuoInterno, RecuoInterno * 0.7f, RecuoInterno, RecuoInterno * 0.5f);
        if (interno.EstaVazio)
        {
            return;
        }

        float alturaRotulo = _estilos.AlturaRotulo;

        _saida.Add(new Primitiva.Texto(
            rotulo,
            interno.FatiaSuperior(alturaRotulo),
            _estilos.Rotulo,
            AlinhamentoH.Esquerda,
            AlinhamentoV.Topo));

        if (string.IsNullOrWhiteSpace(valor))
        {
            return;
        }

        RetanguloMm caixaValor = interno.SemFatiaSuperior(alturaRotulo);
        if (caixaValor.EstaVazio)
        {
            return;
        }

        _saida.Add(new Primitiva.Texto(
            valor,
            caixaValor,
            valorEmNegrito ? _estilos.Valor.EmNegrito() : _estilos.Valor,
            alinhamentoValor,
            AlinhamentoV.Topo));
    }

    /// <summary>
    /// Faixa de titulo de bloco: texto em negrito e caixa alta, 5 pt
    /// (MOC 3.7.1). No retrato e uma faixa horizontal; no paisagem vira uma
    /// tarja vertical de 5,1 mm - ver <see cref="TituloVertical"/>.
    /// </summary>
    public void TituloBloco(RetanguloMm caixa, string texto)
    {
        _saida.Add(new Primitiva.Texto(
            texto.ToUpperInvariant(),
            caixa.Encolhido(RecuoInterno, 0f, RecuoInterno, 0f),
            _estilos.TituloBloco,
            AlinhamentoH.Esquerda,
            AlinhamentoV.Meio));
    }

    /// <summary>Titulo de bloco girado, para o DANFE em paisagem.</summary>
    public void TituloVertical(RetanguloMm caixa, string texto)
    {
        // Gira em torno do centro da caixa e desenha um retangulo com largura e
        // altura trocadas, de modo que o texto corra de baixo para cima.
        PontoMm centro = caixa.Centro;
        var deitado = new RetanguloMm(
            centro.X - (caixa.Altura / 2f),
            centro.Y - (caixa.Largura / 2f),
            caixa.Altura,
            caixa.Largura);

        _saida.Add(new Primitiva.Rotacionado(
            -90f,
            centro,
            [
                new Primitiva.Texto(
                    texto.ToUpperInvariant(),
                    deitado,
                    _estilos.TituloBloco,

                    // Alinhado ao inicio da tarja, nao centralizado: um titulo
                    // mais longo que a tarja perde a ponta, em vez de perder as
                    // duas extremidades e exibir so o miolo.
                    AlinhamentoH.Esquerda,
                    AlinhamentoV.Meio),
            ]));
    }

    /// <summary>Texto solto, sem moldura nem rotulo.</summary>
    public void Texto(
        RetanguloMm caixa,
        string? conteudo,
        EstiloTexto estilo,
        AlinhamentoH h = AlinhamentoH.Esquerda,
        AlinhamentoV v = AlinhamentoV.Topo,
        bool quebrar = false)
    {
        if (string.IsNullOrWhiteSpace(conteudo))
        {
            return;
        }

        _saida.Add(new Primitiva.Texto(conteudo, caixa, estilo, h, v, quebrar));
    }

    /// <summary>Codigo de barras ja codificado em larguras de modulo.</summary>
    public void Barras(RetanguloMm caixa, IReadOnlyList<int> modulos, float moduloMinimoMm) =>
        _saida.Add(new Primitiva.CodigoBarras(caixa, modulos, moduloMinimoMm));

    /// <summary>
    /// Simbolo QR ja codificado. A caixa inclui a margem clara, que vem em
    /// modulos junto com a matriz.
    /// </summary>
    public void Qr(RetanguloMm caixa, MatrizQr matriz)
    {
        ArgumentNullException.ThrowIfNull(matriz);
        _saida.Add(new Primitiva.CodigoQr(caixa, matriz, matriz.MargemModulos));
    }

    public void Linha(float x1, float y1, float x2, float y2, float espessura = EspessuraMoldura) =>
        _saida.Add(new Primitiva.Linha(new PontoMm(x1, y1), new PontoMm(x2, y2), espessura));

    /// <summary>
    /// Linha tracejada. Usada entre o canhoto e o corpo do DANFE, que e onde o
    /// papel e destacado na entrega.
    /// </summary>
    public void LinhaTracejada(float x1, float y, float x2, float traco = 2f, float vao = 1.5f)
    {
        for (float x = x1; x < x2; x += traco + vao)
        {
            _saida.Add(new Primitiva.Linha(
                new PontoMm(x, y),
                new PontoMm(Math.Min(x + traco, x2), y),
                EspessuraMoldura));
        }
    }

    /// <summary>
    /// Divide uma faixa horizontal em colunas de larguras dadas, devolvendo os
    /// retangulos. A ultima coluna e esticada para fechar exatamente na borda
    /// direita, absorvendo o arredondamento acumulado - assim a moldura do
    /// DANFE nunca sai com um fio de sobra.
    /// </summary>
    public static RetanguloMm[] Colunas(RetanguloMm faixa, params float[] larguras)
    {
        var caixas = new RetanguloMm[larguras.Length];
        float x = faixa.X;

        for (int i = 0; i < larguras.Length; i++)
        {
            float largura = i == larguras.Length - 1 ? faixa.Direita - x : larguras[i];
            caixas[i] = new RetanguloMm(x, faixa.Y, largura, faixa.Altura);
            x += largura;
        }

        return caixas;
    }
}

/// <summary>
/// Tamanhos de fonte do DANFE, transcritos do MOC Anexo II secao 3.7.
///
/// Os valores nascem exatamente como a norma manda. O 3.7.9 e o caso
/// espinhoso: manda 10 pt para o "conteudo dos demais campos", que e bem maior
/// do que qualquer DANFE comercial usa, e a 10 pt varios valores nao cabem nas
/// caixas que a propria tabela 3.8 define. Por isso o tamanho e um <b>dado</b>
/// nesta classe, e nao um literal espalhado pelo layout: ajustar e trocar uma
/// constante, nao reescrever o desenho. Ver plano 8/R2.
/// </summary>
public sealed record EstilosDanfe
{
    /// <summary>3.7.1 e 3.7.2: descritivo de bloco e de coluna, 5 pt.</summary>
    public EstiloTexto TituloBloco { get; init; } =
        new(EstiloTexto.FamiliaPadrao, 5f, Negrito: true);

    /// <summary>3.7.2: descritivo das colunas de produtos, 5 pt.</summary>
    public EstiloTexto ColunaProduto { get; init; } = new(EstiloTexto.FamiliaPadrao, 5f);

    /// <summary>3.7.3: descritivo dos demais campos, 6 pt.</summary>
    public EstiloTexto Rotulo { get; init; } = new(EstiloTexto.FamiliaPadrao, 5.5f);

    /// <summary>
    /// 3.7.9: conteudo dos demais campos. A norma diz 10 pt; 7 pt e o valor
    /// praticado, e o unico em que os campos da tabela 3.8 ainda comportam o
    /// dado. Ver a nota da classe.
    /// </summary>
    public EstiloTexto Valor { get; init; } = new(EstiloTexto.FamiliaPadrao, 7f);

    /// <summary>3.7.7: conteudo do quadro de produtos, 6 pt.</summary>
    public EstiloTexto ValorProduto { get; init; } = new(EstiloTexto.FamiliaPadrao, 6f);

    /// <summary>3.7.8: conteudo de informacoes complementares, 6 pt.</summary>
    public EstiloTexto InfoComplementar { get; init; } = new(EstiloTexto.FamiliaPadrao, 6f);

    /// <summary>3.7.6: razao social do emitente, 12 pt negrito.</summary>
    public EstiloTexto EmitenteNome { get; init; } =
        new(EstiloTexto.FamiliaPadrao, 10f, Negrito: true);

    /// <summary>3.7.6: demais dados do emitente, 8 pt negrito.</summary>
    public EstiloTexto EmitenteDados { get; init; } = new(EstiloTexto.FamiliaPadrao, 7f);

    /// <summary>3.7.4: a palavra DANFE, 12 pt negrito.</summary>
    public EstiloTexto PalavraDanfe { get; init; } =
        new(EstiloTexto.FamiliaPadrao, 12f, Negrito: true);

    /// <summary>3.7.4: serie, numero, folha e tipo de operacao, 10 pt negrito.</summary>
    public EstiloTexto NumeroSerie { get; init; } =
        new(EstiloTexto.FamiliaPadrao, 9f, Negrito: true);

    /// <summary>3.7.4: "DOCUMENTO AUXILIAR..." e ENTRADA/SAIDA, 8 pt.</summary>
    public EstiloTexto DescricaoDanfe { get; init; } = new(EstiloTexto.FamiliaPadrao, 6f);

    /// <summary>3.7.5: conteudo da chave de acesso, em negrito.</summary>
    public EstiloTexto ChaveAcesso { get; init; } =
        new(EstiloTexto.FamiliaPadrao, 7.5f, Negrito: true);

    /// <summary>Altura reservada ao rotulo dentro de um campo.</summary>
    public float AlturaRotulo { get; init; } = 1.9f;
}
