using System.Drawing.Imaging;
using FiscalDoc.Layout.DisplayList;
using FiscalDoc.Render.Wpf;

namespace FiscalDoc.App;

/// <summary>Como a pagina se ajusta a janela.</summary>
internal enum ModoZoom
{
    AjustarLargura,
    AjustarPagina,
    CemPorCento,

    /// <summary>
    /// Zoom escolhido a mao, com Ctrl + roda.
    ///
    /// <para>Antes o Ctrl + roda marcava <see cref="CemPorCento"/>, e a barra
    /// passava a exibir "100%" com a pagina em 187%. Pior: escolher "100%" na
    /// lista nao fazia nada, porque o modo ja era aquele - o controle ficava
    /// morto ate o usuario passar por outro modo.</para>
    /// </summary>
    Livre,
}

/// <summary>
/// Superficie de visualizacao da pagina.
///
/// Reproduz exatamente a mesma display list que a impressora recebe - a mesma
/// pagina, nas mesmas coordenadas em milimetros, com a mesma paginacao, que e
/// decidida uma unica vez pelo medidor a 600 dpi. A previa nao "parece" o
/// documento: ela <b>e</b> o documento, numa resolucao menor.
///
/// <para>O que difere do papel e so o rasterizador. Aqui quem converte a
/// geometria em pixel e o <see cref="RenderizadorWpf"/>, ou seja o DirectWrite;
/// na impressora e o GDI+. A razao esta documentada no proprio
/// <c>RenderizadorWpf</c>: a 4 ou 5 pixels por milimetro - um A4 inteiro numa
/// tela de 1440 linhas - o GDI+ quadricula Times New Roman nos 5 e 6 pt que o
/// MOC exige, e o DirectWrite nao.</para>
///
/// A pagina rasterizada fica em cache: rolar nao redesenha milhares de
/// primitivas, so copia um bitmap. O cache e invalidado quando muda a pagina,
/// o zoom ou o DPI do monitor.
/// </summary>
internal sealed class PaginaView : Control
{
    private const int MargemPx = 12;

    /// <summary>Passo de uma seta do teclado, em pixels.</summary>
    private const int PassoDaSeta = 40;

    /// <summary>
    /// Teto de megapixels de uma pagina rasterizada.
    ///
    /// <para>Cada desenho aloca DUAS superficies do tamanho da pagina: o
    /// bitmap que fica em cache e a superficie do WPF que o produz. A A4 em 5x
    /// num monitor de 192 dpi da 89 megapixels - 356 MB cada, 713 MB de pico
    /// por marca da roda -, e a superficie do WPF nao e descartavel, entao ela
    /// so volta quando o coletor decidir. Segurar Ctrl e girar a roda
    /// acumulava mais de um gigabyte antes de qualquer coleta.</para>
    ///
    /// <para>24 megapixels sao ~96 MB por superficie e cobrem a A4 inteira a
    /// cerca de 19 px/mm - quatro vezes a densidade em que o documento ja e
    /// confortavelmente legivel. O limite nao aparece para quem le; aparece
    /// para quem estava prestes a travar a maquina.</para>
    /// </summary>
    private const double MegapixelsMaximos = 24.0;

    /// <summary>Piso: abaixo disso a folha vira um selo ilegivel.</summary>
    internal const float ZoomMinimo = 0.1f;

    /// <summary>Teto duro, independente de papel e monitor.</summary>
    internal const float ZoomMaximoAbsoluto = 5f;

    private readonly RenderizadorWpf _renderizador = new();

    private ConjuntoPaginas? _conjunto;
    private int _indicePagina;
    private ModoZoom _modo = ModoZoom.AjustarLargura;
    private float _zoom = 1f;

    private Bitmap? _cache;
    private float _zoomDoCache;
    private int _paginaDoCache = -1;
    private float _dpiDoCache;

    private Point _rolagem;
    private string? _mensagem;
    private bool _primeiroDesenho = true;

    /// <summary>
    /// Aviso que acompanha o documento sem substitui-lo - ao contrario de
    /// <c>_mensagem</c>, que e estado de erro e apaga a pagina.
    /// </summary>
    private string? _mensagemTemporaria;

    internal PaginaView()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint
            | ControlStyles.UserPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw,
            true);

        BackColor = Color.FromArgb(0x50, 0x50, 0x50);
        TabStop = true;

        // Este e um Control puro, nao um ContainerControl: o WinForms nao
        // aplica escalonamento automatico ao seu conteudo, entao nao ha nada a
        // desligar aqui. Todo o dimensionamento passa pela transformacao em
        // milimetros. Ver plano 3.2.
    }

    internal event EventHandler? EstadoMudou;

    internal int TotalPaginas => _conjunto?.Total ?? 0;

    internal int PaginaAtual => _indicePagina + 1;

    internal float ZoomEfetivo => _zoom;

    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(
        System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal ModoZoom Modo
    {
        get => _modo;
        set
        {
            if (_modo == value)
            {
                return;
            }

            _modo = value;
            RecalcularZoom();
            Invalidate();
            EstadoMudou?.Invoke(this, EventArgs.Empty);
        }
    }

    internal void Mostrar(ConjuntoPaginas conjunto)
    {
        _conjunto = conjunto;
        _mensagem = null;
        _indicePagina = 0;
        _rolagem = Point.Empty;
        DescartarCache();
        RecalcularZoom();
        Invalidate();
        EstadoMudou?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Estado de erro. Desenhado como qualquer outro conteudo, na area do
    /// documento - sem MessageBox e sem stack trace. Ver plano 2.9.
    /// </summary>
    internal void MostrarMensagem(string mensagem)
    {
        _conjunto = null;
        _mensagem = mensagem;
        _indicePagina = 0;
        _rolagem = Point.Empty;
        DescartarCache();
        Invalidate();
        EstadoMudou?.Invoke(this, EventArgs.Empty);
    }

    internal void Limpar()
    {
        _conjunto = null;
        _mensagem = null;
        DescartarCache();
        Invalidate();
        EstadoMudou?.Invoke(this, EventArgs.Empty);
    }

    internal void IrParaPagina(int numero, bool aoFim = false)
    {
        if (_conjunto is null || _conjunto.Total == 0)
        {
            return;
        }

        int novo = Math.Clamp(numero - 1, 0, _conjunto.Total - 1);
        if (novo == _indicePagina)
        {
            return;
        }

        _indicePagina = novo;
        _rolagem = Point.Empty;
        DescartarCache();

        if (aoFim)
        {
            // Entrando pela borda de baixo: posiciona no fim da folha.
            try
            {
                (_, int maxY) = LimitesDeRolagem(ObterBitmap());
                _rolagem = _rolagem with { Y = maxY };
            }
            catch (Exception ex) when (EhFalhaDeRasterizacao(ex))
            {
                ReduzirZoomAposFalha();
            }
        }

        Invalidate();
        EstadoMudou?.Invoke(this, EventArgs.Empty);
    }

    internal void PaginaAnterior(bool aoFim = false) => IrParaPagina(PaginaAtual - 1, aoFim);

    internal void ProximaPagina() => IrParaPagina(PaginaAtual + 1);

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(BackColor);

        if (_mensagem is not null)
        {
            DesenharMensagem(g);
            return;
        }

        if (_conjunto is null)
        {
            DesenharMensagem(g, Mensagens.SemDocumento);
            return;
        }

        Bitmap bmp;

        try
        {
            bmp = ObterBitmap();
        }
        catch (Exception ex) when (EhFalhaDeRasterizacao(ex))
        {
            // Rasterizar DENTRO do OnPaint significa que uma falha aqui vira
            // excecao no pintor, caixa de erro modal, e a caixa ao ser fechada
            // descobre a area do cliente, que pede WM_PAINT, que falha de novo:
            // um laco de caixas que so o Gerenciador de Tarefas encerra.
            //
            // A pagina em 5x num monitor de 192 dpi pede 89 megapixels; nao ha
            // como prometer que sempre cabe. Entao a falha vira mensagem e o
            // zoom volta para um valor que cabe.
            // Vale para QUALQUER modo: a falha nao e privilegio do zoom livre,
            // e "ajustar largura" - o padrao - e justamente o que produz a
            // pagina mais alta.
            ReduzirZoomAposFalha();
            DesenharMensagem(g, Mensagens.ZoomAlemDaMemoria);
            return;
        }

        int x = PosicaoX(bmp);
        int y = MargemPx - _rolagem.Y;

        // Sombra discreta para a folha se destacar do fundo.
        using (var sombra = new SolidBrush(Color.FromArgb(0x30, 0, 0, 0)))
        {
            g.FillRectangle(sombra, x + 3, y + 3, bmp.Width, bmp.Height);
        }

        // Retangulo de origem explicito, em pixel. DrawImageUnscaled desenha
        // pelo tamanho FISICO da imagem: se o DPI gravado no bitmap divergir
        // do DPI do Graphics de destino - o que acontece na hora em que a
        // janela muda de monitor - o GDI+ reamostra a folha inteira e borra
        // tudo em silencio. Com os dois retangulos em pixel, o desenho e
        // sempre um a um.
        g.DrawImage(
            bmp,
            new Rectangle(x, y, bmp.Width, bmp.Height),
            new Rectangle(0, 0, bmp.Width, bmp.Height),
            GraphicsUnit.Pixel);

        using var borda = new Pen(Color.FromArgb(0x20, 0x20, 0x20));
        g.DrawRectangle(borda, x, y, bmp.Width, bmp.Height);

        if (_primeiroDesenho)
        {
            _primeiroDesenho = false;

            // O marcador que tools/bench/measure-startup.ps1 manda correlacionar
            // com o Process/Start do kernel. Estava declarado e nunca emitido:
            // quem seguisse o procedimento documentado via um trace com t0 e
            // sem t1.
            StartupTrace.Log.FirstPaintCompleted();
        }
    }

    /// <summary>
    /// Onde a folha comeca na horizontal: centrada quando sobra espaco,
    /// rolada quando falta.
    /// </summary>
    private int PosicaoX(Bitmap bmp) =>
        Math.Max(MargemPx, (ClientSize.Width - bmp.Width) / 2) - _rolagem.X;

    /// <summary>
    /// Falha ao rasterizar a pagina - tipicamente memoria. A pagina em 5x num
    /// monitor de 192 dpi pede 89 megapixels, e nao ha como prometer que
    /// sempre cabe.
    /// </summary>
    private static bool EhFalhaDeRasterizacao(Exception ex) =>
        ex is OutOfMemoryException
            or ArgumentException
            or InvalidOperationException
            or System.Runtime.InteropServices.COMException;

    /// <summary>
    /// Volta para um zoom que cabe e avisa. Chamado de todo lugar que
    /// rasteriza, nao so do OnPaint.
    /// </summary>
    private void ReduzirZoomAposFalha()
    {
        DescartarCache();

        _modo = ModoZoom.AjustarPagina;
        RecalcularZoom();

        _mensagemTemporaria = Mensagens.ZoomAlemDaMemoria;

        Invalidate();
        EstadoMudou?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Aviso pendente, consumido pela janela e limpo.</summary>
    internal string? ConsumirAviso()
    {
        string? aviso = _mensagemTemporaria;
        _mensagemTemporaria = null;
        return aviso;
    }

    /// <summary>Quanto ainda da para rolar em cada eixo, em pixels.</summary>
    private (int X, int Y) LimitesDeRolagem(Bitmap bmp) =>
        (Math.Max(0, bmp.Width + (2 * MargemPx) - ClientSize.Width),
         Math.Max(0, bmp.Height + (2 * MargemPx) - ClientSize.Height));

    /// <summary>
    /// Rola nos dois eixos, com limite.
    ///
    /// <para>A rolagem horizontal simplesmente nao existia: <c>_rolagem.X</c>
    /// era lido no desenho e nunca escrito, e as setas esquerda/direita eram
    /// declaradas em <see cref="IsInputKey"/> e engolidas sem tratamento. Com
    /// zoom de 2x numa janela comum, a coluna de totais do DANFE ficava fora
    /// da tela sem nenhum jeito de alcanca-la - e dar zoom e a unica forma de
    /// ler texto de 5 pt, entao a falta derrubava justamente o recurso que o
    /// zoom existe para servir.</para>
    /// </summary>
    private void Rolar(int dx, int dy)
    {
        if (_conjunto is null)
        {
            return;
        }

        Bitmap bmp;

        try
        {
            bmp = ObterBitmap();
        }
        catch (Exception ex) when (EhFalhaDeRasterizacao(ex))
        {
            // Mesma razao do OnPaint: rasterizar pode faltar memoria, e uma
            // excecao saindo de um manipulador de teclado ou de roda vira
            // caixa modal dizendo que o aplicativo "precisa fechar".
            ReduzirZoomAposFalha();
            return;
        }
        (int maxX, int maxY) = LimitesDeRolagem(bmp);

        var novo = new Point(
            Math.Clamp(_rolagem.X + dx, 0, maxX),
            Math.Clamp(_rolagem.Y + dy, 0, maxY));

        if (novo == _rolagem)
        {
            return;
        }

        _rolagem = novo;
        Invalidate();
    }

    private void DesenharMensagem(Graphics g, string? texto = null)
    {
        string t = texto ?? _mensagem ?? string.Empty;

        using var fonte = new Font("Segoe UI", 10f);
        using var pincel = new SolidBrush(Color.FromArgb(0xDD, 0xDD, 0xDD));
        using var formato = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
        };

        var area = new RectangleF(
            ClientSize.Width * 0.1f, ClientSize.Height * 0.3f,
            ClientSize.Width * 0.8f, ClientSize.Height * 0.4f);

        g.DrawString(t, fonte, pincel, area, formato);
    }

    private Bitmap ObterBitmap()
    {
        float dpi = DeviceDpi;

        if (_cache is not null
            && _paginaDoCache == _indicePagina
            && Math.Abs(_zoomDoCache - _zoom) < 0.0001f
            && Math.Abs(_dpiDoCache - dpi) < 0.01f)
        {
            return _cache;
        }

        DescartarCache();

        ConjuntoPaginas c = _conjunto!;
        float pxPorMm = dpi / 25.4f * _zoom;

        int largura = Math.Max(1, (int)MathF.Round(c.Papel.LarguraMm * pxPorMm));
        int altura = Math.Max(1, (int)MathF.Round(c.Papel.AlturaMm * pxPorMm));

        // Format32bppPArgb e exatamente o Pbgra32 do WPF: o rasterizador
        // escreve direto nestes bytes, sem copia intermediaria entre os dois
        // mundos. Como a pagina e limpa para branco opaco, a pre-multiplicacao
        // e identidade.
        var bmp = new Bitmap(largura, altura, PixelFormat.Format32bppPArgb);

        try
        {
            bmp.SetResolution(dpi, dpi);

            BitmapData trava = bmp.LockBits(
                new Rectangle(0, 0, largura, altura), ImageLockMode.WriteOnly, bmp.PixelFormat);

            try
            {
                _renderizador.Desenhar(
                    c.Paginas[_indicePagina], pxPorMm, largura, altura, trava.Scan0, trava.Stride);
            }
            finally
            {
                bmp.UnlockBits(trava);
            }
        }
        catch
        {
            // O bitmap so vai para o cache no fim. Se o desenho falhar - e a
            // superficie do WPF tem o mesmo tamanho, entao falta de memoria
            // atinge as duas - este aqui ficaria orfao ate a finalizacao,
            // justamente quando a memoria ja esta no limite.
            bmp.Dispose();
            throw;
        }

        _cache = bmp;
        _zoomDoCache = _zoom;
        _paginaDoCache = _indicePagina;
        _dpiDoCache = dpi;

        return bmp;
    }

    /// <summary>
    /// Maior zoom que respeita <see cref="MegapixelsMaximos"/> para este papel
    /// e este monitor.
    /// </summary>
    private float ZoomMaximo() => _conjunto is null
        ? ZoomMaximoAbsoluto
        : LimiteDeZoom.Maximo(_conjunto.Papel, DeviceDpi / 25.4, MegapixelsMaximos);

    private void RecalcularZoom()
    {
        if (_conjunto is null)
        {
            return;
        }

        float pxPorMm = DeviceDpi / 25.4f;
        float dispW = Math.Max(1, ClientSize.Width - (2 * MargemPx));
        float dispH = Math.Max(1, ClientSize.Height - (2 * MargemPx));

        _zoom = _modo switch
        {
            ModoZoom.AjustarLargura => dispW / (_conjunto.Papel.LarguraMm * pxPorMm),
            ModoZoom.AjustarPagina => Math.Min(
                dispW / (_conjunto.Papel.LarguraMm * pxPorMm),
                dispH / (_conjunto.Papel.AlturaMm * pxPorMm)),

            // Livre mantem o que o usuario escolheu com Ctrl + roda.
            ModoZoom.Livre => _zoom,

            _ => 1f,
        };

        _zoom = Math.Clamp(_zoom, ZoomMinimo, ZoomMaximo());
        _rolagem = Point.Empty;
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);

        if (_modo is ModoZoom.CemPorCento or ModoZoom.Livre)
        {
            // O zoom nao muda, mas a janela sim: sem reclampar, rolar ate o fim
            // e depois maximizar deixava a folha empurrada para fora do topo,
            // com uma faixa cinza embaixo, ate a proxima marca de roda.
            if (_conjunto is not null && _cache is not null)
            {
                (int maxX, int maxY) = LimitesDeRolagem(_cache);
                _rolagem = new Point(
                    Math.Clamp(_rolagem.X, 0, maxX),
                    Math.Clamp(_rolagem.Y, 0, maxY));
            }
        }
        else
        {
            RecalcularZoom();
            DescartarCache();
            EstadoMudou?.Invoke(this, EventArgs.Empty);
        }

        Invalidate();
    }

    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);

        // Mudou de monitor: o bitmap em cache esta na densidade errada.
        RecalcularZoom();
        DescartarCache();
        Invalidate();
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);

        if (_conjunto is null)
        {
            return;
        }

        if (ModifierKeys.HasFlag(Keys.Control))
        {
            // Ctrl + roda: zoom sem ocupar espaco na barra.
            float fator = e.Delta > 0 ? 1.15f : 1f / 1.15f;
            float novoZoom = Math.Clamp(_zoom * fator, ZoomMinimo, ZoomMaximo());

            // No teto, mais uma marca da roda nao muda nada - e sem esta
            // saida cada marca descartava o cache e re-rasterizava a maior
            // pagina possivel, sem diferenca visivel alguma.
            if (Math.Abs(novoZoom - _zoom) < 0.0001f)
            {
                return;
            }

            _modo = ModoZoom.Livre;
            _zoom = novoZoom;
            _rolagem = Point.Empty;
            DescartarCache();
            Invalidate();
            EstadoMudou?.Invoke(this, EventArgs.Empty);
            return;
        }

        Bitmap bmp = ObterBitmap();
        (int maxX, int maxY) = LimitesDeRolagem(bmp);

        // Shift + roda rola na horizontal, como em qualquer visualizador.
        if (ModifierKeys.HasFlag(Keys.Shift))
        {
            Rolar(-PassoDaRoda(e.Delta), 0);
            return;
        }

        if (maxY == 0)
        {
            // Pagina inteira visivel: a roda vira navegacao entre paginas.
            if (e.Delta < 0)
            {
                ProximaPagina();
            }
            else
            {
                PaginaAnterior();
            }

            return;
        }

        int novo = Math.Clamp(_rolagem.Y - PassoDaRoda(e.Delta), 0, maxY);

        if (novo == _rolagem.Y)
        {
            // Chegou ao fim da pagina: continua para a pagina vizinha.
            if (e.Delta < 0 && PaginaAtual < TotalPaginas)
            {
                ProximaPagina();
            }
            else if (e.Delta > 0 && PaginaAtual > 1)
            {
                // Sobe para a pagina anterior pelo FIM dela, nao pelo comeco.
                // Rolar uma marca para tras e ser jogado ao topo da folha
                // anterior - uma pagina inteira acima de onde se estava -
                // tornava impossivel reler a ultima linha da pagina de cima.
                PaginaAnterior(aoFim: true);
            }

            return;
        }

        _rolagem = _rolagem with { Y = novo };
        Invalidate();
    }

    /// <summary>
    /// Quanto uma marca da roda rola, em pixels.
    ///
    /// <para>Usar <c>e.Delta</c> como se fosse pixel - 120 por marca - ignora
    /// a preferencia do sistema e, num touchpad de precisao, transforma cada
    /// fragmento de gesto num salto. Aqui o delta vira fracao de marca e
    /// multiplica o passo configurado.</para>
    /// </summary>
    private static int PassoDaRoda(int delta)
    {
        const int PixelsPorLinha = 20;

        int linhas = SystemInformation.MouseWheelScrollLines;

        // -1 quer dizer "uma tela por vez" na configuracao do Windows.
        if (linhas < 0)
        {
            linhas = 12;
        }

        return (int)Math.Round(delta / 120.0 * linhas * PixelsPorLinha);
    }

    protected override bool IsInputKey(Keys keyData) => keyData switch
    {
        Keys.Up or Keys.Down or Keys.Left or Keys.Right
            or Keys.PageUp or Keys.PageDown or Keys.Home or Keys.End => true,
        _ => base.IsInputKey(keyData),
    };

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (_conjunto is null)
        {
            return;
        }

        switch (e.KeyCode)
        {
            case Keys.PageDown:
                ProximaPagina();
                e.Handled = true;
                break;

            case Keys.PageUp:
                PaginaAnterior();
                e.Handled = true;
                break;

            case Keys.Home:
                // Sempre move: se ja esta na primeira pagina, vai para o topo
                // dela. Antes IrParaPagina saia cedo quando o indice nao
                // mudava, e a tecla ficava inerte num documento de uma folha.
                IrParaPagina(1);
                Rolar(-int.MaxValue / 2, -int.MaxValue / 2);
                e.Handled = true;
                break;

            case Keys.End:
                IrParaPagina(TotalPaginas);
                Rolar(int.MaxValue / 2, int.MaxValue / 2);
                e.Handled = true;
                break;

            case Keys.Down:
                Rolar(0, PassoDaSeta);
                e.Handled = true;
                break;

            case Keys.Up:
                Rolar(0, -PassoDaSeta);
                e.Handled = true;
                break;

            case Keys.Right:
                Rolar(PassoDaSeta, 0);
                e.Handled = true;
                break;

            case Keys.Left:
                Rolar(-PassoDaSeta, 0);
                e.Handled = true;
                break;
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
    }

    private void DescartarCache()
    {
        _cache?.Dispose();
        _cache = null;
        _paginaDoCache = -1;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            DescartarCache();
        }

        base.Dispose(disposing);
    }
}
