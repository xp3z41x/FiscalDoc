using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FiscalDoc.Layout.DisplayList;

namespace FiscalDoc.Render.Wpf;

/// <summary>
/// Reproduz a display list na tela usando o <b>DirectWrite</b>, pelo WPF.
///
/// <para><b>E o unico reprodutor do aplicativo.</b> A tela e o papel recebem
/// a mesma <see cref="Pagina"/>, nas mesmas coordenadas em milimetros, pela
/// mesma funcao, com a mesma metrica de texto que paginou o documento
/// (<c>MedidorTextoWpf</c>). A previa nao se parece com o documento: ela
/// <b>e</b> o documento.</para>
///
/// <para><b>O que este reprodutor NAO decide.</b> Nao pagina, nao mede para o
/// layout, nao escolhe fonte, nao escolhe coordenada. So converte a geometria
/// ja decidida em tinta, de um jeito na tela e de outro no papel - ver
/// <see cref="Modo"/>.</para>
///
/// <para><b>A conversao de milimetro e explicita.</b> Nada de pendurar uma
/// escala no <c>DrawingContext</c> e desenhar em milimetro: o WPF dimensiona
/// o glifo pelo em em unidades do proprio contexto, <b>antes</b> de qualquer
/// transformacao, e um em de 1,76 mm seria tratado como 1,76 unidade - o
/// texto sairia destruido. Cada coordenada e multiplicada na entrada.</para>
///
/// <para><b><see cref="TextFormattingMode.Ideal"/>, e nao Display.</b> Ideal
/// nao encaixa glifo em grade de pixel, entao a metrica e <b>invariante de
/// escala</b>: a mesma linha mede o mesmo milimetro a 4 px/mm na tela e a
/// 600 dpi no papel. E isso que permite que <c>MedidorTextoWpf</c> pagine uma
/// vez so e o resultado valha para os dois. Display arredondaria cada avanco
/// para pixel inteiro - medido, engorda o texto em 3,5 % na mediana e ate
/// 11 % - e a paginacao passaria a depender do dispositivo.</para>
///
/// <para><b>Na tela, cinza, nao ClearType.</b> O
/// <see cref="RenderTargetBitmap"/> desliga o ClearType por conta propria - a
/// superficie pode ter alfa. Medido: ClearType e Grayscale saem byte a byte
/// identicos, com zero pixel colorido. No papel a questao nem se coloca: o
/// XPS leva vetor, e quem rasteriza e o RIP.</para>
/// </summary>
public sealed class RenderizadorWpf
{
    /// <summary>Unidades do WPF por milimetro: uma unidade e 1/96 de polegada.</summary>
    private const double UnidadesPorMm = 96.0 / 25.4;

    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("pt-BR");

    private readonly CacheTipos _tipos = new();

    /// <summary>
    /// Como converter milimetro em unidade de desenho, e quanto vale um ponto
    /// do dispositivo nessa unidade.
    ///
    /// <para><b>Tela:</b> a unidade <b>e</b> o pixel, entao
    /// <c>UnidadesPorPonto = 1</c> e <c>Encaixar = true</c> - o fio tem de
    /// cair em fileira inteira ou a folha parece desfocada.</para>
    ///
    /// <para><b>Papel:</b> a unidade e o DIP do XPS, que e resolucao
    /// nenhuma - quem rasteriza e o RIP da impressora, no DPI dela. Encaixar
    /// em DIP seria encaixar numa grade imaginaria e <b>perder</b> precisao,
    /// entao no papel nao se encaixa nada: a geometria vai em milimetro
    /// exato. Mas o modulo do codigo de barras continua sendo quantizado para
    /// ponto inteiro <b>da impressora</b>, que e o que o leitor exige.</para>
    /// </summary>
    private readonly record struct Modo(double PorMm, double PorPonto, bool Encaixar)
    {
        internal static Modo Tela(double pxPorMm) => new(pxPorMm, 1.0, true);

        /// <param name="escala">
        /// Reducao que sera aplicada por cima. Entra na conta do ponto porque
        /// a quantizacao precisa valer no ponto FINAL da impressora: quantizar
        /// antes e reduzir depois devolvia modulo de codigo de barras com
        /// fracao de ponto - exatamente a barra irregular que a quantizacao
        /// existe para impedir.
        /// </param>
        internal static Modo Papel(double dotsPorPolegada, double escala) =>
            new(
                UnidadesPorMm,
                96.0 / Math.Max(dotsPorPolegada, 1.0) / Math.Max(escala, 0.01),
                false);
    }

    /// <summary>
    /// Desenha uma pagina direto na memoria de um bitmap BGRA pre-multiplicado
    /// (o <c>Format32bppPArgb</c> do GDI+, que e o <c>Pbgra32</c> do WPF).
    ///
    /// <para>Escrever no buffer do chamador evita a copia intermediaria entre
    /// os dois mundos: o WPF rasteriza, e os pixels vao direto para o bitmap
    /// que a janela ja tem em maos.</para>
    /// </summary>
    /// <param name="pagina">Pagina da display list.</param>
    /// <param name="pxPorMm">Pixels de dispositivo por milimetro do layout.</param>
    /// <param name="larguraPx">Largura do destino, em pixels.</param>
    /// <param name="alturaPx">Altura do destino, em pixels.</param>
    /// <param name="destino">Ponteiro para os pixels do destino.</param>
    /// <param name="bytesPorLinha">Passo de linha do destino, em bytes.</param>
    public void Desenhar(
        Pagina pagina,
        double pxPorMm,
        int larguraPx,
        int alturaPx,
        IntPtr destino,
        int bytesPorLinha)
    {
        ArgumentNullException.ThrowIfNull(pagina);
        ArgumentOutOfRangeException.ThrowIfLessThan(larguraPx, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(alturaPx, 1);

        var visual = new DrawingVisual();

        using (DrawingContext dc = visual.RenderOpen())
        {
            dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, larguraPx, alturaPx));

            Modo modo = Modo.Tela(pxPorMm);

            foreach (Primitiva p in pagina.Primitivas)
            {
                DesenharPrimitiva(dc, p, modo);
            }
        }

        var alvo = new RenderTargetBitmap(larguraPx, alturaPx, 96, 96, PixelFormats.Pbgra32);
        alvo.Render(visual);

        alvo.CopyPixels(
            new Int32Rect(0, 0, larguraPx, alturaPx),
            destino,
            bytesPorLinha * alturaPx,
            bytesPorLinha);
    }

    /// <summary>
    /// Desenha uma pagina para <b>papel</b>, num contexto do XPS.
    ///
    /// <para>A unidade e o DIP (1/96 de polegada) e a origem e o canto
    /// <b>fisico</b> da folha - que e onde o XPS poe a origem da pagina. Por
    /// isso nao ha deslocamento de margem nenhum aqui: a coordenada do layout
    /// ja e milimetro absoluto da folha, e o que cai na margem fisica da
    /// impressora e recortado pelo proprio dispositivo. A aritmetica de
    /// HardMargin que o caminho antigo precisava deixou de existir.</para>
    ///
    /// <para>Nada e encaixado em grade: no papel quem rasteriza e o RIP, no
    /// DPI dele. O unico valor quantizado e o modulo do codigo de barras e do
    /// QR, que vai para ponto inteiro da impressora - ver <see cref="Modo"/>.
    /// </para>
    /// </summary>
    /// <param name="dc">Contexto da pagina XPS.</param>
    /// <param name="pagina">Pagina da display list.</param>
    /// <param name="dpi">Resolucao declarada pela impressora.</param>
    /// <param name="escala">
    /// Reducao necessaria para o conteudo caber na area imprimivel daquele
    /// equipamento. 1 quer dizer 1:1 exato.
    /// </param>
    public void DesenharNoPapel(DrawingContext dc, Pagina pagina, double dpi, AjusteImpressao ajuste)
    {
        ArgumentNullException.ThrowIfNull(dc);
        ArgumentNullException.ThrowIfNull(pagina);

        bool altera = ajuste.Altera;

        if (altera)
        {
            // Deslocamento primeiro, escala depois: o deslocamento ja vem
            // calculado sobre o conteudo REDUZIDO, entao ele nao pode ser
            // escalado junto.
            var grupo = new TransformGroup();
            grupo.Children.Add(new ScaleTransform(ajuste.Escala, ajuste.Escala));
            grupo.Children.Add(new TranslateTransform(
                ajuste.DeslocamentoXMm * UnidadesPorMm,
                ajuste.DeslocamentoYMm * UnidadesPorMm));

            dc.PushTransform(grupo);
        }

        Modo modo = Modo.Papel(dpi, ajuste.Escala);

        foreach (Primitiva p in pagina.Primitivas)
        {
            DesenharPrimitiva(dc, p, modo);
        }

        if (altera)
        {
            dc.Pop();
        }
    }

    /// <summary>
    /// Encaixa uma coordenada de traco no pixel.
    ///
    /// Um traco e centrado na coordenada: com espessura impar ele so cobre uma
    /// fileira inteira se o centro cair no meio do pixel; com espessura par,
    /// se cair na fronteira. Sem isso, o fio de 0,15 mm da moldura do DANFE -
    /// que quase nunca cai em fronteira inteira - vira duas fileiras de cinza,
    /// e a folha inteira parece desfocada.
    ///
    /// <para>Medido: <c>EdgeMode.Aliased</c> sozinho <b>nao</b> resolve isto;
    /// o encaixe explicito resolve.</para>
    /// </summary>
    private static double EncaixarTraco(double v, double espessura) =>
        (int)Math.Round(espessura) % 2 == 1
            ? Math.Floor(v) + 0.5
            : Math.Round(v);

    /// <summary>
    /// Encaixa uma borda de preenchimento no pixel. Preenchimento nao e
    /// centrado: a borda mora entre dois pixels, entao arredonda direto.
    /// </summary>
    private static double EncaixarArea(double v) => Math.Round(v);

    private void DesenharPrimitiva(DrawingContext dc, Primitiva p, Modo m)
    {
        switch (p)
        {
            case Primitiva.Linha l:
                DesenharLinha(dc, l, m);
                break;

            case Primitiva.Contorno c:
                DesenharContorno(dc, c, m);
                break;

            case Primitiva.Preenchimento f:
                dc.DrawRectangle(_tipos.Pincel(f.Cor), null, AreaEncaixada(f.Caixa, m));
                break;

            case Primitiva.Texto t:
                DesenharTexto(dc, t, m);
                break;

            case Primitiva.CodigoBarras b:
                DesenharCodigoBarras(dc, b, m);
                break;

            case Primitiva.CodigoQr q:
                DesenharCodigoQr(dc, q, m);
                break;

            case Primitiva.Rotacionado r:
                DesenharRotacionado(dc, r, m);
                break;
        }
    }

    private static Rect AreaEncaixada(RetanguloMm caixa, Modo m)
    {
        double esq = m.Encaixar ? EncaixarArea(caixa.X * m.PorMm) : caixa.X * m.PorMm;
        double topo = m.Encaixar ? EncaixarArea(caixa.Y * m.PorMm) : caixa.Y * m.PorMm;
        double dir = m.Encaixar ? EncaixarArea(caixa.Direita * m.PorMm) : caixa.Direita * m.PorMm;
        double baixo = m.Encaixar ? EncaixarArea(caixa.Base * m.PorMm) : caixa.Base * m.PorMm;

        return new Rect(esq, topo, Math.Max(dir - esq, 0), Math.Max(baixo - topo, 0));
    }

    /// <summary>
    /// Espessura do traco, em pixels <b>inteiros</b>, nunca menos que um.
    ///
    /// <para>Inteira, e nao so no minimo um: encaixar a posicao sem encaixar a
    /// espessura nao adianta nada. Um fio de 1,42 pixel centrado no meio do
    /// pixel cobre de -0,21 a +1,21 - tres fileiras, duas delas em meio-tom,
    /// que e exatamente o borrao que o encaixe existe para eliminar. Com a
    /// espessura inteira, um fio de n pixels cobre n fileiras cheias.</para>
    ///
    /// <para>Arredondar encolhe ou engorda o fio em fracao de pixel, o que na
    /// tela e invisivel. No papel nao se arredonda nada: la o traco vai com o
    /// milimetro exato, com o piso de um ponto do dispositivo.</para>
    /// </summary>
    private Pen Caneta(float espessuraMm, Modo m)
    {
        double bruta = espessuraMm * m.PorMm;

        // Na tela, pixel inteiro (ver acima). No papel, o milimetro exato -
        // com o minimo de um ponto da impressora, senao um fio de 0,15 mm
        // sumiria numa matricial de 120 dpi.
        double espessura = m.Encaixar
            ? Math.Max(Math.Round(bruta), 1.0)
            : Math.Max(bruta, m.PorPonto);

        var caneta = new Pen(_tipos.Pincel(Tinta.Preto), espessura);
        caneta.Freeze();
        return caneta;
    }

    private void DesenharLinha(DrawingContext dc, Primitiva.Linha l, Modo m)
    {
        Pen caneta = Caneta(l.EspessuraMm, m);

        double x1 = l.De.X * m.PorMm;
        double y1 = l.De.Y * m.PorMm;
        double x2 = l.Ate.X * m.PorMm;
        double y2 = l.Ate.Y * m.PorMm;

        // Encaixe so na tela. No papel a coordenada em milimetro e mais
        // precisa que qualquer grade que pudessemos inventar em DIP.
        if (m.Encaixar)
        {
            if (Math.Abs(y1 - y2) < 0.001)
            {
                y1 = y2 = EncaixarTraco(y1, caneta.Thickness);
                (x1, x2) = PontasEncaixadas(x1, x2);
            }
            else if (Math.Abs(x1 - x2) < 0.001)
            {
                x1 = x2 = EncaixarTraco(x1, caneta.Thickness);
                (y1, y2) = PontasEncaixadas(y1, y2);
            }
        }

        dc.DrawLine(caneta, new Point(x1, y1), new Point(x2, y2));
    }

    /// <summary>
    /// Encaixa as duas pontas de um segmento no pixel, garantindo que ele
    /// sobreviva.
    ///
    /// Arredondar cada ponta por conta propria pode igualar as duas: um
    /// segmento de menos de um pixel vira comprimento zero, e o WPF nao
    /// desenha nada. E o caso da linha tracejada do canhoto, cujos tracos tem
    /// 2 mm - a 0,38 px/mm, no zoom minimo, cada traco mede 0,76 px e cerca
    /// de um quarto deles sumiria, deixando a linha de destaque pontilhada de
    /// forma irregular em vez de tracejada.
    /// </summary>
    private static (double De, double Ate) PontasEncaixadas(double de, double ate)
    {
        double a = EncaixarArea(de);
        double b = EncaixarArea(ate);

        if (Math.Abs(a - b) >= 1.0)
        {
            return (a, b);
        }

        // Colapsou: devolve um pixel inteiro, no sentido original.
        return ate >= de ? (a, a + 1.0) : (a, a - 1.0);
    }

    private void DesenharContorno(DrawingContext dc, Primitiva.Contorno c, Modo m)
    {
        Pen caneta = Caneta(c.EspessuraMm, m);
        double e = caneta.Thickness;

        double esq = m.Encaixar ? EncaixarTraco(c.Caixa.X * m.PorMm, e) : c.Caixa.X * m.PorMm;
        double topo = m.Encaixar ? EncaixarTraco(c.Caixa.Y * m.PorMm, e) : c.Caixa.Y * m.PorMm;
        double dir = m.Encaixar ? EncaixarTraco(c.Caixa.Direita * m.PorMm, e) : c.Caixa.Direita * m.PorMm;
        double baixo = m.Encaixar ? EncaixarTraco(c.Caixa.Base * m.PorMm, e) : c.Caixa.Base * m.PorMm;

        dc.DrawRectangle(
            null, caneta, new Rect(esq, topo, Math.Max(dir - esq, 0), Math.Max(baixo - topo, 0)));
    }

    private void DesenharTexto(DrawingContext dc, Primitiva.Texto t, Modo m)
    {
        if (string.IsNullOrEmpty(t.Conteudo) || t.Caixa.EstaVazio)
        {
            return;
        }

        double x = t.Caixa.X * m.PorMm;
        double y = t.Caixa.Y * m.PorMm;
        double largura = t.Caixa.Largura * m.PorMm;
        double altura = t.Caixa.Altura * m.PorMm;

        var texto = new FormattedText(
            t.Conteudo,
            Cultura,
            FlowDirection.LeftToRight,
            _tipos.Obter(t.Estilo),
            t.Estilo.TamanhoMm * m.PorMm,
            _tipos.Pincel(t.Cor),
            null,
            TextFormattingMode.Ideal,
            1.0)
        {
            Trimming = TextTrimming.None,
        };

        if (t.Quebrar)
        {
            // Sem folga nenhuma, e de proposito.
            //
            // Quem quebrou estas linhas foi o MedidorTextoWpf, que e este
            // mesmo motor, no mesmo modo de formatacao e na mesma cultura:
            // a linha que ele julgou caber cabe aqui, exatamente. Enquanto a
            // medicao era GDI+ e o desenho DirectWrite as duas divergiam ate
            // 3,58 %, e uma linha ja quebrada podia ser quebrada de novo aqui
            // - a linha extra estourava a altura reservada e o recorte engolia
            // uma linha inteira da descricao do produto, sem aviso. Era
            // preciso um colchao; agora nao e mais.
            texto.MaxTextWidth = largura;

            texto.TextAlignment = t.Horizontal switch
            {
                AlinhamentoH.Centro => TextAlignment.Center,
                AlinhamentoH.Direita => TextAlignment.Right,
                _ => TextAlignment.Left,
            };
        }
        else
        {
            // Sem quebra: posiciona a mao, porque MaxTextWidth e o que faz o
            // FormattedText quebrar, e aqui ele nao pode.
            double deslocamentoH = t.Horizontal switch
            {
                AlinhamentoH.Centro => (largura - texto.Width) / 2.0,
                AlinhamentoH.Direita => largura - texto.Width,
                _ => 0.0,
            };

            // Nunca para a esquerda da caixa.
            //
            // Um valor alinhado a direita que nao cabe recebia deslocamento
            // NEGATIVO, e o recorte comia o comeco: "25,1293333333" saia
            // "293333333" - um preco plausivel e errado, que e o pior
            // resultado possivel num documento fiscal. Preso a borda
            // esquerda, o que se perde e o fim, e o numero fica visivelmente
            // truncado em vez de silenciosamente falso.
            x += Math.Max(0.0, deslocamentoH);
        }

        double deslocamentoV = t.Vertical switch
        {
            AlinhamentoV.Meio => (altura - texto.Height) / 2.0,
            AlinhamentoV.Base => altura - texto.Height,
            _ => 0.0,
        };

        // Nunca para cima. Com o texto mais alto que a caixa - um rotulo de
        // bloco que virou tres linhas onde cabiam duas - um deslocamento
        // negativo empurraria o bloco para FORA da caixa pelo topo, e o
        // recorte comeria a PRIMEIRA linha. Perder o fim de um campo ja e
        // ruim; perder o comeco e pior, porque o que sobra parece completo.
        y += Math.Max(0.0, deslocamentoV);

        // O recorte impede que um campo comprido invada o vizinho - num DANFE
        // isso seria pior do que faltar informacao. Um pixel de folga absorve
        // o arredondamento do rasterizador, e nada alem disso.
        // A folga de um pixel e concessao de TELA: existe para absorver o
        // arredondamento do encaixe na grade. No papel nao ha encaixe, entao
        // nao ha o que absorver - e um DIP de folga la seriam 0,26 mm de
        // invasao no campo vizinho, que no quadro de produtos sao colunas
        // encostadas sem calha.
        double folga = m.Encaixar ? 1.0 : 0.0;

        var recorte = new Rect(
            (t.Caixa.X * m.PorMm) - folga,
            (t.Caixa.Y * m.PorMm) - folga,
            largura + (2 * folga),
            altura + (2 * folga));

        dc.PushClip(new RectangleGeometry(recorte));
        dc.DrawText(texto, new Point(x, y));
        dc.Pop();
    }

    /// <summary>
    /// Mesma regra do renderizador GDI+: a largura do modulo e quantizada uma
    /// unica vez para um numero inteiro de pixels, e todas as barras sao
    /// multiplos inteiros dela. Barra de largura irregular e o que faz leitor
    /// recusar.
    ///
    /// <para>A lista do Code128C comeca pela margem clara, que e espaco - nao
    /// por barra.</para>
    /// </summary>
    private void DesenharCodigoBarras(DrawingContext dc, Primitiva.CodigoBarras b, Modo m)
    {
        int totalModulos = 0;
        foreach (int largura in b.Modulos)
        {
            totalModulos += largura;
        }

        if (totalModulos == 0 || b.Caixa.EstaVazio)
        {
            return;
        }

        double disponivel = b.Caixa.Largura * m.PorMm / totalModulos;
        double minimo = b.LarguraModuloMmMinima * m.PorMm;
        double modulo;

        if (disponivel <= minimo)
        {
            // O dispositivo nao tem resolucao para o minimo normativo nesta
            // caixa. Preenche exatamente: na tela nenhum scanner esta lendo, e
            // sair da moldura seria pior.
            modulo = disponivel;
        }
        else
        {
            // Quantiza PARA BAIXO, em pontos inteiros do dispositivo. Na tela
            // o ponto e o pixel; no papel e o dot da impressora, que o DIP nao
            // conhece - dai o fator. Barra de largura irregular e o que faz
            // leitor recusar.
            modulo = Math.Floor(disponivel / m.PorPonto) * m.PorPonto;

            if (modulo < m.PorPonto)
            {
                modulo = disponivel;
            }
            else if (modulo < minimo)
            {
                modulo = minimo;
            }
        }

        double usado = modulo * totalModulos;
        double xBruto = (b.Caixa.X * m.PorMm) + (((b.Caixa.Largura * m.PorMm) - usado) / 2.0);
        double x = m.Encaixar ? EncaixarArea(xBruto) : xBruto;

        double topo = m.Encaixar ? EncaixarArea(b.Caixa.Y * m.PorMm) : b.Caixa.Y * m.PorMm;
        double baixo = m.Encaixar ? EncaixarArea(b.Caixa.Base * m.PorMm) : b.Caixa.Base * m.PorMm;
        SolidColorBrush pincel = _tipos.Pincel(Tinta.Preto);

        bool barra = false;

        foreach (int modulos in b.Modulos)
        {
            double largura = modulos * modulo;

            if (barra)
            {
                dc.DrawRectangle(
                    pincel, null, new Rect(x, topo, largura, Math.Max(baixo - topo, 0)));
            }

            x += largura;
            barra = !barra;
        }
    }

    /// <summary>
    /// Mesma regra das barras, com o detalhe extra do QR: cada modulo e
    /// desenhado com a <b>mesma</b> largura inteira, e nao entre duas bordas
    /// calculadas - senao dois modulos escuros vizinhos deixam um fio branco
    /// entre si por arredondamento, e o leitor le isso como modulo claro.
    /// </summary>
    private void DesenharCodigoQr(DrawingContext dc, Primitiva.CodigoQr q, Modo m)
    {
        int n = q.Matriz.Tamanho;

        if (n <= 0 || q.Caixa.EstaVazio)
        {
            return;
        }

        int totalModulos = n + (2 * q.MargemModulos);
        double lado = Math.Min(q.Caixa.Largura, q.Caixa.Altura) * m.PorMm;
        double disponivel = lado / totalModulos;

        // Abaixo de quatro pontos por modulo nao ha resolucao para um QR
        // legivel de qualquer forma; quantizar so encolheria o simbolo. E o
        // caso da previa de tela; numa impressora, nunca.
        const int PontosMinimosPorModulo = 4;

        double pontos = disponivel / m.PorPonto;

        // O escape so vale na TELA. Numa impressora de 150 dpi o simbolo
        // tambem cai abaixo de quatro pontos por modulo, e la deixar de
        // quantizar produz justamente o modulo irregular que o leitor recusa.
        double modulo = pontos >= PontosMinimosPorModulo || !m.Encaixar
            ? Math.Max(Math.Floor(pontos), 1) * m.PorPonto
            : disponivel;

        double usado = modulo * totalModulos;
        double x0 = (q.Caixa.X * m.PorMm) + (((q.Caixa.Largura * m.PorMm) - usado) / 2.0) + (q.MargemModulos * modulo);
        double y0 = (q.Caixa.Y * m.PorMm) + (((q.Caixa.Altura * m.PorMm) - usado) / 2.0) + (q.MargemModulos * modulo);

        if (m.Encaixar)
        {
            x0 = EncaixarArea(x0);
            y0 = EncaixarArea(y0);
        }

        SolidColorBrush pincel = _tipos.Pincel(Tinta.Preto);

        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                if (q.Matriz.Escuro(x, y))
                {
                    dc.DrawRectangle(
                        pincel, null, new Rect(x0 + (x * modulo), y0 + (y * modulo), modulo, modulo));
                }
            }
        }
    }

    private void DesenharRotacionado(DrawingContext dc, Primitiva.Rotacionado r, Modo m)
    {
        dc.PushTransform(new RotateTransform(r.Graus, r.Centro.X * m.PorMm, r.Centro.Y * m.PorMm));

        foreach (Primitiva filho in r.Filhos)
        {
            DesenharPrimitiva(dc, filho, m);
        }

        dc.Pop();
    }
}
