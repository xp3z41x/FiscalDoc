using FiscalDoc.Layout.DisplayList;

namespace FiscalDoc.Layout;

/// <summary>
/// Medicao de texto em milimetros.
///
/// Este e o unico ponto em que o layout precisaria de um motor de texto, e
/// por isso e uma interface: o Layout compila para net10.0 puro e nao pode
/// enxergar WPF. A implementacao vive em FiscalDoc.Render.Wpf e mede pelo
/// DirectWrite em modo Ideal, que e <b>invariante de escala</b> - a mesma
/// linha mede o mesmo milimetro na tela e no papel.
///
/// <para>E o mesmo motor que desenha, nos dois destinos. Por isso a linha que
/// o layout quebrou aqui e exatamente a linha que cabe la, e "FOLHA 2/3" na
/// previa e "FOLHA 2/3" no papel - nao por disciplina, mas porque nao existe
/// outra metrica no aplicativo.</para>
/// </summary>
public interface IMedidorTexto
{
    /// <summary>Largura de uma linha unica, sem quebra.</summary>
    float LarguraMm(string texto, EstiloTexto estilo);

    /// <summary>
    /// Altura de uma linha, do topo do ascendente a base do descendente,
    /// incluindo o espacamento natural da fonte.
    /// </summary>
    float AlturaLinhaMm(EstiloTexto estilo);

    /// <summary>
    /// Quebra o texto em linhas que cabem na largura dada. Quebra em espaco
    /// quando da, e no meio da palavra quando nao da - codigo de produto e
    /// NCM nao tem espaco e ainda assim precisam caber na coluna.
    /// </summary>
    IReadOnlyList<string> Quebrar(string texto, EstiloTexto estilo, float larguraMm);
}
