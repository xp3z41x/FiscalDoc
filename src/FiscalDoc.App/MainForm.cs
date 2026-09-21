using FiscalDoc.Layout.DisplayList;

namespace FiscalDoc.App;

/// <summary>
/// A janela. Uma so, e o minimo de controles que o escopo justifica:
///
/// <list type="bullet">
/// <item><b>Abrir</b> - escopo item 1; sem ele o app so funciona por
/// associacao de arquivo.</item>
/// <item><b>Imprimir</b> - escopo item 4.</item>
/// <item><b>Zoom</b> - uma folha A4 inteira nao e legivel na tela e 100% nao
/// cabe; sem zoom o item 3 ("visualizacao") nao se cumpre.</item>
/// <item><b>Pagina N/M</b> - so aparece quando M &gt; 1. Sem ele as paginas
/// seguintes ficariam inalcancaveis.</item>
/// <item><b>Faixa de aviso</b> - so aparece quando ha o que dizer.</item>
/// </list>
///
/// Nao ha ribbon, barra lateral, splash, menu, icone decorativo nem item
/// desabilitado sem razao. Ver plano 3.
/// </summary>
internal sealed class MainForm : Form
{
    private readonly AberturaDocumento _abertura = new();

    private readonly ToolStrip _barra = new();
    private readonly ToolStripButton _btnAbrir = new();
    private readonly ToolStripButton _btnImprimir = new();
    private readonly ToolStripComboBox _cmbZoom = new();
    private readonly ToolStripLabel _lblPagina = new();
    private readonly ToolStripButton _btnAnterior = new();
    private readonly ToolStripButton _btnProxima = new();

    private readonly Label _aviso = new();
    private readonly PaginaView _view = new();

    private ConjuntoPaginas? _paginas;

    internal MainForm(string? caminhoInicial)
    {
        Text = Mensagens.TituloJanela;
        ClientSize = new Size(1000, 780);
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(560, 400);
        AllowDrop = true;
        KeyPreview = true;

        MontarBarra();
        MontarAviso();

        _view.Dock = DockStyle.Fill;
        _view.EstadoMudou += (_, _) => AtualizarEstado();

        // Ordem importa: o ultimo adicionado fica mais ao fundo no Dock.Fill.
        Controls.Add(_view);
        Controls.Add(_aviso);
        Controls.Add(_barra);

        DragEnter += AoArrastar;
        DragDrop += AoSoltar;
        KeyDown += AoTeclar;

        AtualizarEstado();

        // Guardado para o OnShown: no construtor a janela ainda nao tem handle,
        // e BeginInvoke sem handle lanca InvalidOperationException.
        _caminhoInicial = caminhoInicial;
    }

    private readonly string? _caminhoInicial;

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);

        if (_caminhoInicial is not null)
        {
            // Duplo clique no Explorer: abre direto, sem tela intermediaria.
            Abrir(_caminhoInicial);
        }
    }

    private void MontarBarra()
    {
        _barra.Dock = DockStyle.Top;
        _barra.GripStyle = ToolStripGripStyle.Hidden;
        _barra.RenderMode = ToolStripRenderMode.System;
        _barra.Padding = new Padding(4, 2, 4, 2);

        _btnAbrir.Text = "Abrir";
        _btnAbrir.DisplayStyle = ToolStripItemDisplayStyle.Text;
        _btnAbrir.ToolTipText = "Abrir um XML de documento fiscal (Ctrl+O)";
        _btnAbrir.Click += (_, _) => EscolherArquivo();

        _btnImprimir.Text = "Imprimir";
        _btnImprimir.DisplayStyle = ToolStripItemDisplayStyle.Text;
        _btnImprimir.ToolTipText = "Imprimir (Ctrl+P)";
        _btnImprimir.Click += (_, _) => Imprimir();

        _cmbZoom.DropDownStyle = ComboBoxStyle.DropDownList;
        _cmbZoom.Items.AddRange(["Ajustar largura", "Ajustar página", "100%"]);
        _cmbZoom.SelectedIndex = 0;
        _cmbZoom.Width = 130;
        _cmbZoom.ToolTipText = "Zoom (Ctrl + roda do mouse)";
        _cmbZoom.SelectedIndexChanged += (_, _) =>
        {
            // -1 e o estado "nenhum dos tres", que a barra mostra enquanto o
            // zoom veio do Ctrl + roda. Nao e uma escolha do usuario.
            if (_cmbZoom.SelectedIndex >= 0)
            {
                _view.Modo = (ModoZoom)_cmbZoom.SelectedIndex;
            }
        };

        _btnAnterior.Text = "◀";
        _btnAnterior.DisplayStyle = ToolStripItemDisplayStyle.Text;
        _btnAnterior.ToolTipText = "Página anterior (Page Up)";
        _btnAnterior.Click += (_, _) => _view.PaginaAnterior();

        _btnProxima.Text = "▶";
        _btnProxima.DisplayStyle = ToolStripItemDisplayStyle.Text;
        _btnProxima.ToolTipText = "Próxima página (Page Down)";
        _btnProxima.Click += (_, _) => _view.ProximaPagina();

        _lblPagina.Text = string.Empty;

        _barra.Items.AddRange(
        [
            _btnAbrir,
            _btnImprimir,
            new ToolStripSeparator(),
            _cmbZoom,
            new ToolStripSeparator(),
            _btnAnterior,
            _lblPagina,
            _btnProxima,
        ]);
    }

    private void MontarAviso()
    {
        _aviso.Dock = DockStyle.Top;
        _aviso.AutoSize = false;
        _aviso.Height = 30;
        _aviso.TextAlign = ContentAlignment.MiddleLeft;
        _aviso.Padding = new Padding(10, 0, 10, 0);
        _aviso.BackColor = Color.FromArgb(0xFF, 0xF4, 0xCE);
        _aviso.ForeColor = Color.FromArgb(0x5C, 0x45, 0x00);
        _aviso.Visible = false;
    }

    // =====================================================================

    private void EscolherArquivo()
    {
        using var dlg = new OpenFileDialog
        {
            Filter = Mensagens.FiltroAbertura,
            Title = "Abrir documento fiscal",
            CheckFileExists = true,
        };

        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            Abrir(dlg.FileName);
        }
    }

    private void Abrir(string caminho)
    {
        Cursor = Cursors.WaitCursor;

        try
        {
            AberturaDocumento.Resultado r = _abertura.Abrir(caminho);

            _paginas = r.Paginas;
            Text = r.Titulo;

            if (r.Paginas is not null)
            {
                _view.Mostrar(r.Paginas);
                AvisarSobreEscala(r.Paginas);
            }
            else
            {
                _view.MostrarMensagem(r.Mensagem ?? string.Empty);
                MostrarAviso(null);
            }
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }

    /// <summary>
    /// Se a impressora padrao nao comportar 1:1, o usuario tem direito de saber
    /// antes de imprimir - e nao depois, com a regua na mao. Ver plano 2.4.
    /// </summary>
    private void AvisarSobreEscala(ConjuntoPaginas paginas)
    {
        float escala = Impressao.EstimarEscala(paginas);
        MostrarAviso(escala < 0.999f ? Mensagens.AvisoEscala(escala) : null);
    }

    private void MostrarAviso(string? texto)
    {
        _aviso.Text = texto ?? string.Empty;
        _aviso.Visible = texto is not null;
    }

    /// <summary>
    /// Verdadeiro enquanto o dialogo de impressao esta aberto.
    ///
    /// <para>O dialogo do WPF nao recebe janela dona: ele tira o dono de
    /// <c>System.Windows.Application.Current.MainWindow</c>, e este processo
    /// nao tem uma - a janela e WinForms. Sem dono, o Windows nao desabilita
    /// a janela principal, e ela continua respondendo por baixo do dialogo
    /// modal: da para abrir outro documento (trocando <c>_paginas</c> debaixo
    /// de um trabalho vivo), empilhar um segundo dialogo, ou fechar a janela
    /// com a impressao em curso.</para>
    ///
    /// <para>Desabilitar a janela pela duracao do dialogo produz o mesmo
    /// efeito pratico de uma modal de verdade. O que continua sem solucao aqui
    /// e a ordem-z: clicar na barra de tarefas ainda traz a janela para a
    /// frente do dialogo. Resolver isso exigiria criar um
    /// <c>System.Windows.Application</c> so para hospedar um dono, o que traz
    /// um segundo ciclo de vida de aplicacao para dentro do processo.</para>
    /// </summary>
    private bool _imprimindo;

    private void Imprimir()
    {
        if (_paginas is null || _imprimindo)
        {
            return;
        }

        var impressao = new Impressao(_paginas);

        _imprimindo = true;
        Enabled = false;

        try
        {
            impressao.Imprimir();
        }
        catch (Exception ex) when (ex is System.Printing.PrintSystemException
                                      or System.Windows.Xps.XpsWriterException
                                      or System.Windows.Xps.XpsSerializationException
                                      or System.ComponentModel.Win32Exception
                                      or InvalidOperationException
                                      or NullReferenceException
                                      or ArgumentException
                                      or System.Xml.XmlException
                                      or System.Runtime.InteropServices.ExternalException)
        {
            // Sem impressora, driver recusando, fila que sumiu no meio do
            // trabalho, falha do subsistema de impressao. Nada disso e motivo
            // para derrubar o aplicativo nem para mostrar pilha de chamadas -
            // o documento continua na tela.
            MostrarAviso(Mensagens.ImpressaoFalhou);
        }
        finally
        {
            Enabled = true;
            _imprimindo = false;

            // Desabilitar tira o foco; devolver e o que faz o teclado voltar a
            // chegar na pagina.
            if (!IsDisposed && CanFocus)
            {
                Activate();
            }
        }
    }

    private void AtualizarEstado()
    {
        // O WinForms ainda dispara eventos de layout enquanto a janela e
        // desmontada. Tocar num controle ja descartado ali lancaria
        // ObjectDisposedException, e o processo ficaria preso numa caixa de
        // mensagem em vez de morrer junto com a janela.
        if (IsDisposed || Disposing)
        {
            return;
        }

        bool temDocumento = _paginas is not null;
        bool multiPagina = _view.TotalPaginas > 1;

        _btnImprimir.Enabled = temDocumento;
        _cmbZoom.Enabled = temDocumento;

        // Navegacao de pagina so existe quando ha mais de uma. Um controle que
        // nunca faria nada nao tem por que ocupar espaco.
        _btnAnterior.Visible = multiPagina;
        _btnProxima.Visible = multiPagina;
        _lblPagina.Visible = multiPagina;

        if (multiPagina)
        {
            _lblPagina.Text = $"{_view.PaginaAtual} / {_view.TotalPaginas}";
            _btnAnterior.Enabled = _view.PaginaAtual > 1;
            _btnProxima.Enabled = _view.PaginaAtual < _view.TotalPaginas;
        }

        // No modo livre nenhum dos tres itens esta ativo: a barra mostra a
        // porcentagem na dica e deixa a lista em branco. Antes ela exibia
        // "100%" com a pagina em 187%, e escolher "100%" nao fazia nada.
        int indiceDesejado = _view.Modo == ModoZoom.Livre ? -1 : (int)_view.Modo;

        if (temDocumento && indiceDesejado != _cmbZoom.SelectedIndex)
        {
            _cmbZoom.SelectedIndex = indiceDesejado;
        }

        if (_view.ConsumirAviso() is { } avisoDaView)
        {
            MostrarAviso(avisoDaView);
        }

        _cmbZoom.ToolTipText = temDocumento
            ? $"Zoom: {_view.ZoomEfetivo * 100:0}%  (Ctrl + roda do mouse)"
            : "Zoom (Ctrl + roda do mouse)";
    }

    // =====================================================================

    private static void AoArrastar(object? sender, DragEventArgs e)
    {
        e.Effect = e.Data?.GetDataPresent(DataFormats.FileDrop) == true
            ? DragDropEffects.Copy
            : DragDropEffects.None;
    }

    private void AoSoltar(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } arquivos)
        {
            // Uma janela, um documento: abre o primeiro.
            Abrir(arquivos[0]);
        }
    }

    private void AoTeclar(object? sender, KeyEventArgs e)
    {
        if (!e.Control)
        {
            return;
        }

        switch (e.KeyCode)
        {
            case Keys.O:
                EscolherArquivo();
                e.Handled = true;
                break;

            case Keys.P:
                Imprimir();
                e.Handled = true;
                break;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Nao descartar _barra, _aviso nem _view aqui: eles estao em
            // Controls e a classe base os descarta na ordem certa. Descartar
            // antes da hora fazia o OnResize que o WinForms ainda dispara
            // durante o desmonte cair em AtualizarEstado com o combo ja morto.
        }

        base.Dispose(disposing);
    }
}
