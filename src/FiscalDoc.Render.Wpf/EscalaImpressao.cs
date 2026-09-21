using FiscalDoc.Layout.DisplayList;

namespace FiscalDoc.Render.Wpf;

/// <summary>
/// Como encaixar o conteudo na area imprimivel daquele equipamento: quanto
/// reduzir, e quanto deslocar.
/// </summary>
/// <param name="Escala">
/// Fator uniforme. 1 quer dizer 1:1 exato.
/// </param>
/// <param name="DeslocamentoXMm">Deslocamento horizontal, em milimetros.</param>
/// <param name="DeslocamentoYMm">Deslocamento vertical, em milimetros.</param>
public readonly record struct AjusteImpressao(
    float Escala,
    float DeslocamentoXMm,
    float DeslocamentoYMm)
{
    /// <summary>Imprime exatamente onde o layout mandou, sem tocar em nada.</summary>
    public static readonly AjusteImpressao Nenhum = new(1f, 0f, 0f);

    /// <summary>Verdadeiro quando ha algo a aplicar no Graphics.</summary>
    public bool Altera =>
        Escala < 1f || DeslocamentoXMm != 0f || DeslocamentoYMm != 0f;
}

/// <summary>
/// Encaixa o conteudo na area que a impressora consegue marcar.
/// </summary>
public static class EscalaImpressao
{
    /// <summary>
    /// Calcula reducao e deslocamento.
    ///
    /// <para>O MOC posiciona o conteudo do DANFE de 0,25 cm a 20,82 cm: sobram
    /// 1,8 mm de margem a direita numa folha de 21 cm, e o conteudo
    /// <b>comeca</b> a 2,5 mm da borda. Impressora comum nao marca cerca de
    /// 4 mm de cada lado. Ou seja, em 1:1 estrito falta espaco nas duas
    /// pontas.</para>
    ///
    /// <para><b>Caixa dentro de caixa, nao borda contra borda.</b> Comparar
    /// so <c>conteudo.Direita</c> com <c>imprimivel.Direita</c> resolve a
    /// direita e <b>piora</b> a esquerda: reduzir em torno da origem puxa a
    /// borda esquerda <i>para dentro</i> da faixa que a impressora nao marca.
    /// Numa laser comum (margem de 4,23 mm) o DANFE perdia 1,76 mm do lado
    /// esquerdo - a moldura inteira dos nove quadros e o primeiro caractere de
    /// cada linha da coluna CODIGO - enquanto o aviso na tela dizia
    /// tranquilizadores 98,9%.</para>
    ///
    /// <para>Aqui a largura do conteudo entra na largura imprimivel, e o que
    /// sobrar de margem vira deslocamento. O deslocamento so aparece quando e
    /// necessario: numa impressora de margem zero o resultado e exatamente
    /// 1:1, na posicao absoluta que o MOC manda.</para>
    /// </summary>
    public static AjusteImpressao Calcular(RetanguloMm conteudo, RetanguloMm imprimivel)
    {
        if (conteudo.Largura <= 0 || conteudo.Altura <= 0
            || imprimivel.Largura <= 0 || imprimivel.Altura <= 0)
        {
            return AjusteImpressao.Nenhum;
        }

        float escala = Math.Min(
            imprimivel.Largura / conteudo.Largura,
            imprimivel.Altura / conteudo.Altura);

        if (float.IsNaN(escala) || escala <= 0f)
        {
            // Driver reportando area imprimivel absurda. Imprimir 1:1 e melhor
            // do que espelhar ou colapsar a folha.
            return AjusteImpressao.Nenhum;
        }

        // Nunca amplia: 1:1 e o teto.
        escala = Math.Min(escala, 1f);

        // So empurra o que estiver antes da borda imprimivel. Quem ja cabe
        // fica exatamente onde o layout mandou.
        float dx = Math.Max(0f, imprimivel.X - (conteudo.X * escala));
        float dy = Math.Max(0f, imprimivel.Y - (conteudo.Y * escala));

        return new AjusteImpressao(escala, dx, dy);
    }
}
