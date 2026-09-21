namespace FiscalDoc.Layout.DisplayList;

/// <summary>
/// Ponto em milimetros absolutos da folha, com origem no canto superior
/// esquerdo do papel fisico.
/// </summary>
public readonly record struct PontoMm(float X, float Y)
{
    public PontoMm Deslocado(float dx, float dy) => new(X + dx, Y + dy);
}

/// <summary>Retangulo em milimetros absolutos da folha.</summary>
public readonly record struct RetanguloMm(float X, float Y, float Largura, float Altura)
{
    public float Direita => X + Largura;

    public float Base => Y + Altura;

    public PontoMm CantoSuperiorEsquerdo => new(X, Y);

    public PontoMm Centro => new(X + (Largura / 2f), Y + (Altura / 2f));

    /// <summary>Retangulo definido por dois cantos, em vez de canto + tamanho.</summary>
    public static RetanguloMm DeCantos(float esquerda, float topo, float direita, float baixo) =>
        new(esquerda, topo, direita - esquerda, baixo - topo);

    /// <summary>Encolhe por dentro (margem interna uniforme).</summary>
    public RetanguloMm Encolhido(float margem) =>
        new(X + margem, Y + margem, Largura - (2 * margem), Altura - (2 * margem));

    /// <summary>Encolhe por dentro com margens independentes.</summary>
    public RetanguloMm Encolhido(float esquerda, float topo, float direita, float baixo) =>
        new(X + esquerda, Y + topo, Largura - esquerda - direita, Altura - topo - baixo);

    public RetanguloMm Deslocado(float dx, float dy) => new(X + dx, Y + dy, Largura, Altura);

    /// <summary>Fatia horizontal a partir do topo, sem alterar este retangulo.</summary>
    public RetanguloMm FatiaSuperior(float altura) => new(X, Y, Largura, altura);

    /// <summary>O que sobra depois de remover uma fatia do topo.</summary>
    public RetanguloMm SemFatiaSuperior(float altura) =>
        new(X, Y + altura, Largura, Altura - altura);

    public bool EstaVazio => Largura <= 0 || Altura <= 0;
}

public enum AlinhamentoH
{
    Esquerda,
    Centro,
    Direita,
}

public enum AlinhamentoV
{
    Topo,
    Meio,
    Base,
}

/// <summary>
/// Estilo de texto. O tamanho fica em <b>pontos tipograficos</b>, porque e a
/// unidade em que o MOC especifica os minimos (Anexo II 3.7): 5 pt para
/// descritivo de bloco, 6 pt para descritivo de campo, 10 pt para conteudo, e
/// por ai. Guardar em pontos deixa a conferencia contra a norma imediata.
///
/// A conversao para milimetros acontece uma unica vez, no renderizador:
/// 1 pt = 1/72 pol = 0,352778 mm.
/// </summary>
public sealed record EstiloTexto(
    string Familia,
    float TamanhoPt,
    bool Negrito = false,
    bool Italico = false)
{
    /// <summary>
    /// O MOC 3.7 e categorico: "Todos os caracteres deverao estar impressos na
    /// fonte Times New Roman ou na fonte Courier New". Nao e Arial.
    /// </summary>
    public const string FamiliaPadrao = "Times New Roman";

    public const string FamiliaMonoespacada = "Courier New";

    /// <summary>Tamanho do em em milimetros.</summary>
    public float TamanhoMm => TamanhoPt * 25.4f / 72f;

    public EstiloTexto ComTamanho(float pt) => this with { TamanhoPt = pt };

    public EstiloTexto EmNegrito() => this with { Negrito = true };
}
