using FiscalDoc.Layout.DisplayList;

namespace FiscalDoc.Layout.Composition;

/// <summary>Um campo rotulado dentro de uma linha. O peso divide a largura.</summary>
public sealed record CampoDoc(
    string Rotulo,
    string? Valor,
    float Peso = 1f,
    AlinhamentoH Alinhamento = AlinhamentoH.Esquerda,
    bool Destacado = false);

/// <summary>Coluna de tabela. O peso divide a largura disponivel.</summary>
public sealed record ColunaDoc(
    string Titulo,
    float Peso,
    AlinhamentoH Alinhamento = AlinhamentoH.Esquerda);

/// <summary>Bloco de conteudo, na ordem em que aparece no papel.</summary>
public abstract record BlocoDoc
{
    private BlocoDoc()
    {
    }

    /// <summary>Faixa de titulo de secao.</summary>
    public sealed record Secao(string Titulo) : BlocoDoc;

    /// <summary>Uma linha de campos lado a lado.</summary>
    public sealed record Linha(IReadOnlyList<CampoDoc> Campos) : BlocoDoc;

    /// <summary>Texto corrido com rotulo, que cresce conforme o conteudo.</summary>
    public sealed record Texto(string Rotulo, string? Conteudo, float AlturaMinimaMm = 10f) : BlocoDoc;

    /// <summary>
    /// Tabela. E o unico bloco que se divide entre paginas; quando isso
    /// acontece, o cabecalho das colunas se repete na continuacao.
    /// </summary>
    public sealed record Tabela(
        IReadOnlyList<ColunaDoc> Colunas,
        IReadOnlyList<string[]> Linhas) : BlocoDoc;

    /// <summary>Espaco em branco deliberado.</summary>
    public sealed record Espaco(float AlturaMm) : BlocoDoc;
}

/// <summary>
/// Empilhador de blocos com paginacao.
///
/// Serve ao DACTE, ao DAMDFE e a representacao de evento - os tres documentos
/// para os quais <b>nao existe tabela de coordenadas normativa</b>. O MOC do
/// CT-e e o do MDF-e trazem apenas figuras, sem medida de campo, sem tamanho
/// minimo de fonte e sem regra de margem; para eventos nao ha layout nenhum em
/// MOC algum.
///
/// Diante disso, fingir precisao milimetrica seria falsa exatidao. O que este
/// montador entrega e o que o criterio acordado pede: todos os blocos
/// presentes, na ordem do modelo oficial, legiveis, sem sobreposicao e sem
/// corte. A geometria e derivada da largura util e do conteudo.
///
/// O DANFE <b>nao</b> passa por aqui: para ele existe a tabela 3.8 do Anexo II,
/// e <c>DanfeRetrato</c> a segue campo a campo.
/// </summary>
public sealed class MontadorDocumento
{
    /// <summary>Desenha o cabecalho repetido; devolve o y onde o corpo comeca.</summary>
    public delegate float DesenhaCabecalho(Campo campo, int pagina, int totalPaginas);

    private readonly TamanhoPapel _papel;
    private readonly float _esquerda;
    private readonly float _direita;
    private readonly float _topo;
    private readonly float _base;
    private readonly EstilosDanfe _estilos;
    private readonly IMedidorTexto _medidor;

    public MontadorDocumento(
        TamanhoPapel papel,
        float margemMm,
        EstilosDanfe estilos,
        IMedidorTexto medidor)
    {
        _papel = papel;
        _esquerda = margemMm;
        _direita = papel.LarguraMm - margemMm;
        _topo = margemMm;
        _base = papel.AlturaMm - margemMm;
        _estilos = estilos;
        _medidor = medidor;
    }

    public float Largura => _direita - _esquerda;

    public float Esquerda => _esquerda;

    public float Direita => _direita;

    public float Topo => _topo;

    /// <summary>Altura de uma linha de campos: rotulo em cima, valor embaixo.</summary>
    public float AlturaLinha { get; init; } = 8.0f;

    /// <summary>Altura da faixa de titulo de secao.</summary>
    public float AlturaSecao { get; init; } = 4.6f;

    /// <summary>Altura do cabecalho de uma tabela.</summary>
    public float AlturaCabecalhoTabela { get; init; } = 5.0f;

    /// <summary>Altura de uma linha de tabela.</summary>
    public float AlturaLinhaTabela { get; init; } = 4.4f;

    public ConjuntoPaginas Montar(IReadOnlyList<BlocoDoc> blocos, DesenhaCabecalho cabecalho)
    {
        // Passo 1: distribuir os blocos em paginas. Nada e desenhado ainda -
        // e por isso que "FOLHA 1/3" ja sai certo na primeira pagina.
        List<List<BlocoDoc>> plano = Planejar(blocos, cabecalho);

        // Passo 2: desenhar.
        var paginas = new List<Pagina>(plano.Count);

        for (int i = 0; i < plano.Count; i++)
        {
            var saida = new List<Primitiva>(256);
            var campo = new Campo(saida, _estilos);

            float y = cabecalho(campo, i + 1, plano.Count);

            foreach (BlocoDoc b in plano[i])
            {
                y = Desenhar(campo, b, y);
            }

            paginas.Add(new Pagina(i + 1, saida));
        }

        return new ConjuntoPaginas(
            paginas,
            _papel,
            RetanguloMm.DeCantos(_esquerda, _topo, _direita, _base));
    }

    private List<List<BlocoDoc>> Planejar(
        IReadOnlyList<BlocoDoc> blocos, DesenhaCabecalho cabecalho)
    {
        // Mede o cabecalho desenhando-o num descarte: e o mesmo codigo do
        // desenho real, entao a altura nao pode divergir.
        float topoCorpo = cabecalho(new Campo([], _estilos), 1, 1);

        var plano = new List<List<BlocoDoc>>();
        var atual = new List<BlocoDoc>();
        float y = topoCorpo;

        foreach (BlocoDoc b in blocos)
        {
            if (b is BlocoDoc.Tabela t)
            {
                // Tabela e o unico bloco divisivel. Consome o espaco que
                // sobrar, e o resto segue para a pagina seguinte.
                int inicio = 0;

                while (inicio < t.Linhas.Count || inicio == 0)
                {
                    float disponivel = _base - y;
                    int cabem = LinhasQueCabem(disponivel);

                    if (cabem <= 0 && atual.Count > 0)
                    {
                        plano.Add(atual);
                        atual = [];
                        y = topoCorpo;
                        continue;
                    }

                    int quantas = Math.Min(Math.Max(cabem, 0), t.Linhas.Count - inicio);
                    var fatia = new List<string[]>(quantas);

                    for (int k = 0; k < quantas; k++)
                    {
                        fatia.Add(t.Linhas[inicio + k]);
                    }

                    atual.Add(new BlocoDoc.Tabela(t.Colunas, fatia));
                    y += AlturaCabecalhoTabela + (quantas * AlturaLinhaTabela);
                    inicio += quantas;

                    if (inicio >= t.Linhas.Count)
                    {
                        break;
                    }

                    plano.Add(atual);
                    atual = [];
                    y = topoCorpo;
                }

                continue;
            }

            float altura = Medir(b);

            if (y + altura > _base && atual.Count > 0)
            {
                plano.Add(atual);
                atual = [];
                y = topoCorpo;
            }

            atual.Add(b);
            y += altura;
        }

        plano.Add(atual);
        return plano;
    }

    private int LinhasQueCabem(float disponivelMm)
    {
        float util = disponivelMm - AlturaCabecalhoTabela;
        return util <= 0 ? 0 : (int)(util / AlturaLinhaTabela);
    }

    private float Medir(BlocoDoc b) => b switch
    {
        BlocoDoc.Secao => AlturaSecao,
        BlocoDoc.Linha => AlturaLinha,
        BlocoDoc.Espaco e => e.AlturaMm,

        BlocoDoc.Texto t => MedirTexto(t),

        BlocoDoc.Tabela tab =>
            AlturaCabecalhoTabela + (tab.Linhas.Count * AlturaLinhaTabela),

        _ => 0f,
    };

    private float MedirTexto(BlocoDoc.Texto t)
    {
        if (string.IsNullOrWhiteSpace(t.Conteudo))
        {
            return t.AlturaMinimaMm;
        }

        float larguraUtil = Largura - (2 * Campo.RecuoInterno);
        IReadOnlyList<string> linhas = _medidor.Quebrar(
            t.Conteudo, _estilos.InfoComplementar, larguraUtil);

        float alturaLinha = _medidor.AlturaLinhaMm(_estilos.InfoComplementar);
        float necessaria = 2.6f + (linhas.Count * alturaLinha) + 1.2f;

        return Math.Max(t.AlturaMinimaMm, necessaria);
    }

    private float Desenhar(Campo c, BlocoDoc b, float y)
    {
        switch (b)
        {
            case BlocoDoc.Secao s:
                c.TituloBloco(new RetanguloMm(_esquerda, y, Largura, AlturaSecao), s.Titulo);
                return y + AlturaSecao;

            case BlocoDoc.Espaco e:
                return y + e.AlturaMm;

            case BlocoDoc.Linha l:
                DesenharLinha(c, l, y);
                return y + AlturaLinha;

            case BlocoDoc.Texto t:
            {
                float altura = Medir(t);
                var caixa = new RetanguloMm(_esquerda, y, Largura, altura);
                c.Moldura(caixa);

                c.Texto(
                    caixa.Encolhido(Campo.RecuoInterno, 0.6f, Campo.RecuoInterno, 0f)
                         .FatiaSuperior(2.2f),
                    t.Rotulo,
                    _estilos.Rotulo);

                if (!string.IsNullOrWhiteSpace(t.Conteudo))
                {
                    c.Texto(
                        caixa.Encolhido(Campo.RecuoInterno, 2.9f, Campo.RecuoInterno, 0.6f),
                        t.Conteudo,
                        _estilos.InfoComplementar,
                        AlinhamentoH.Esquerda,
                        AlinhamentoV.Topo,
                        quebrar: true);
                }

                return y + altura;
            }

            case BlocoDoc.Tabela tab:
                return DesenharTabela(c, tab, y);

            default:
                return y;
        }
    }

    private void DesenharLinha(Campo c, BlocoDoc.Linha l, float y)
    {
        float pesoTotal = 0f;
        foreach (CampoDoc f in l.Campos)
        {
            pesoTotal += f.Peso;
        }

        if (pesoTotal <= 0)
        {
            return;
        }

        float x = _esquerda;

        for (int i = 0; i < l.Campos.Count; i++)
        {
            CampoDoc f = l.Campos[i];

            // A ultima coluna fecha exatamente na borda, absorvendo o
            // arredondamento acumulado.
            float largura = i == l.Campos.Count - 1
                ? _direita - x
                : Largura * (f.Peso / pesoTotal);

            c.Rotulado(
                new RetanguloMm(x, y, largura, AlturaLinha),
                f.Rotulo,
                f.Valor,
                f.Alinhamento,
                f.Destacado);

            x += largura;
        }
    }

    private float DesenharTabela(Campo c, BlocoDoc.Tabela t, float y)
    {
        float altura = AlturaCabecalhoTabela + (t.Linhas.Count * AlturaLinhaTabela);
        var quadro = new RetanguloMm(_esquerda, y, Largura, altura);
        c.Moldura(quadro);

        float pesoTotal = 0f;
        foreach (ColunaDoc col in t.Colunas)
        {
            pesoTotal += col.Peso;
        }

        if (pesoTotal <= 0)
        {
            return y + altura;
        }

        // Cabecalho das colunas e divisorias verticais.
        float x = _esquerda;
        var limites = new float[t.Colunas.Count + 1];

        for (int i = 0; i < t.Colunas.Count; i++)
        {
            limites[i] = x;
            float largura = i == t.Colunas.Count - 1
                ? _direita - x
                : Largura * (t.Colunas[i].Peso / pesoTotal);

            c.Texto(
                new RetanguloMm(x, y, largura, AlturaCabecalhoTabela)
                    .Encolhido(0.5f, 0.3f, 0.5f, 0.3f),
                t.Colunas[i].Titulo,
                _estilos.ColunaProduto,
                AlinhamentoH.Centro,
                AlinhamentoV.Meio,
                quebrar: true);

            if (i > 0)
            {
                c.Linha(x, y, x, quadro.Base);
            }

            x += largura;
        }

        limites[^1] = _direita;

        float yCab = y + AlturaCabecalhoTabela;
        c.Linha(_esquerda, yCab, _direita, yCab);

        // Celulas.
        for (int r = 0; r < t.Linhas.Count; r++)
        {
            float yLinha = yCab + (r * AlturaLinhaTabela);
            string[] valores = t.Linhas[r];

            for (int i = 0; i < t.Colunas.Count && i < valores.Length; i++)
            {
                c.Texto(
                    new RetanguloMm(
                        limites[i], yLinha, limites[i + 1] - limites[i], AlturaLinhaTabela)
                        .Encolhido(Campo.RecuoInterno, 0.4f, Campo.RecuoInterno, 0f),
                    valores[i],
                    _estilos.ValorProduto,
                    t.Colunas[i].Alinhamento,
                    AlinhamentoV.Topo);
            }

            if (r > 0)
            {
                c.Linha(_esquerda, yLinha, _direita, yLinha, 0.05f);
            }
        }

        return y + altura;
    }
}
