using FiscalDoc.Layout.Barcode;

namespace FiscalDoc.Layout.DisplayList;

/// <summary>
/// Uma instrucao de desenho em milimetros absolutos da folha.
///
/// Este e o contrato entre o layout e o renderizador, e a razao pela qual
/// preview e impressao sao identicos: existe uma unica lista, reproduzida por
/// uma unica funcao, em duas superficies diferentes. Nao e uma disciplina que
/// alguem precise manter - e propriedade estrutural. Ver plano 2.1.
///
/// Nenhuma primitiva conhece pixel, DPI, impressora ou tela.
/// </summary>
public abstract record Primitiva
{
    private Primitiva()
    {
    }

    /// <summary>Segmento de reta.</summary>
    public sealed record Linha(PontoMm De, PontoMm Ate, float EspessuraMm) : Primitiva;

    /// <summary>Retangulo de contorno.</summary>
    public sealed record Contorno(RetanguloMm Caixa, float EspessuraMm) : Primitiva;

    /// <summary>Retangulo preenchido. Usado em faixa de titulo e em tarja.</summary>
    public sealed record Preenchimento(RetanguloMm Caixa, Tinta Cor) : Primitiva;

    /// <summary>
    /// Texto dentro de uma caixa. O texto que nao couber e recortado pela
    /// caixa - jamais transborda para cima do campo vizinho.
    /// </summary>
    public sealed record Texto(
        string Conteudo,
        RetanguloMm Caixa,
        EstiloTexto Estilo,
        AlinhamentoH Horizontal = AlinhamentoH.Esquerda,
        AlinhamentoV Vertical = AlinhamentoV.Topo,
        bool Quebrar = false,
        Tinta Cor = Tinta.Preto) : Primitiva;

    /// <summary>
    /// Codigo de barras ja codificado, como larguras em <b>modulos</b>.
    ///
    /// Modulos, e nao milimetros, de proposito: a largura fisica do modulo e
    /// quantizada para um numero inteiro de pontos da impressora no momento do
    /// desenho. Escolher um valor bonito em milimetros e deixar cada barra
    /// arredondar sozinha produz barra irregular, que e o que faz leitor
    /// recusar. Ver plano 2.7.
    /// </summary>
    public sealed record CodigoBarras(
        RetanguloMm Caixa,
        IReadOnlyList<int> Modulos,
        float LarguraModuloMmMinima) : Primitiva;

    /// <summary>
    /// Simbolo QR ja codificado, como matriz de modulos.
    ///
    /// Modulos pelo mesmo motivo do <see cref="CodigoBarras"/>: a largura
    /// fisica do modulo e quantizada para um numero inteiro de pontos da
    /// impressora no momento do desenho. A margem clara vem em modulos junto,
    /// e nao em milimetros, porque a norma e o Manual do DANFE NFC-e a definem
    /// em relacao ao proprio simbolo. Ver <c>MatrizQr.MargemModulos</c>.
    ///
    /// <para>A <see cref="Caixa"/> inclui a margem clara: e o quadrado inteiro
    /// reservado ao codigo, nao so a area escura.</para>
    /// </summary>
    public sealed record CodigoQr(
        RetanguloMm Caixa,
        MatrizQr Matriz,
        int MargemModulos) : Primitiva;

    /// <summary>
    /// Sub-lista girada em torno de um ponto. Usada no canhoto e nas faixas de
    /// titulo verticais do DANFE em paisagem (MOC Anexo II 3.8.2).
    /// </summary>
    public sealed record Rotacionado(
        float Graus,
        PontoMm Centro,
        IReadOnlyList<Primitiva> Filhos) : Primitiva;
}

/// <summary>
/// Paleta minima. Um DANFE e preto no branco; cinza existe para a faixa de
/// titulo de bloco e vermelho para o estado de erro na tela - que nunca e
/// impresso.
/// </summary>
public enum Tinta
{
    Preto = 0,
    CinzaClaro,
    CinzaMedio,
    Branco,
    Vermelho,
}
