using System.Printing;
using System.Windows.Documents;
using System.Windows.Media;
using FiscalDoc.Layout.DisplayList;
using FiscalDoc.Render.Wpf;
using Wpf = System.Windows;

namespace FiscalDoc.App;

/// <summary>
/// Pipeline de impressao, sobre XPS.
///
/// Nao faz suposicao nenhuma sobre a impressora: o tamanho de papel vem da
/// lista do proprio driver, a area imprimivel e lida em tempo de execucao, e
/// a escala e calculada contra aquele equipamento. Funciona em laser, jato de
/// tinta, matricial e driver virtual.
///
/// <para><b>Por que XPS e nao GDI+.</b> A pagina vai para a impressora como
/// <b>vetor</b>: quem rasteriza e o RIP do equipamento, no DPI dele, e nao o
/// aplicativo. Some com isso toda a aritmetica de ponto de dispositivo que o
/// caminho antigo precisava - e com ela a classe de defeitos que ela
/// carregava. Mais importante: o desenho passa a ser o <b>mesmo</b>
/// <see cref="RenderizadorWpf"/> que desenha a tela, com a <b>mesma</b>
/// metrica de texto que paginou o documento (<c>MedidorTextoWpf</c>). Previa
/// e papel voltaram a ser identicos por construcao.</para>
///
/// <para><b>Origem.</b> O XPS poe a origem da pagina no canto <b>fisico</b>
/// da folha, que e exatamente onde o layout ja conta seus milimetros. Nao ha
/// deslocamento de margem a aplicar: o que cair dentro da margem fisica da
/// impressora e recortado pelo proprio equipamento, e a escala existe
/// justamente para que isso nao aconteca.</para>
/// </summary>
internal sealed class Impressao
{
    /// <summary>Unidades do XPS por milimetro: uma unidade e 1/96 de polegada.</summary>
    private const double UnidadesPorMm = 96.0 / 25.4;

    /// <summary>
    /// Resolucao suposta quando o driver nao declara nenhuma. So afeta a
    /// quantizacao do modulo do codigo de barras; 600 dpi e o laser tipico.
    /// </summary>
    private const double DpiSuposto = 600.0;

    private readonly ConjuntoPaginas _conjunto;
    private readonly RenderizadorWpf _renderizador = new();

    /// <summary>O que o aplicativo sugeriu, para distinguir da escolha do usuario.</summary>
    private PageOrientation? _orientacaoSugerida;

    private PageMediaSizeName? _midiaSugerida;

    internal Impressao(ConjuntoPaginas conjunto) => _conjunto = conjunto;

    /// <summary>Escala aplicada na ultima impressao. 1,0 quer dizer 1:1 exato.</summary>
    internal float UltimaEscala { get; private set; } = 1f;

    /// <summary>
    /// Mostra o dialogo de impressao e imprime. Devolve false se o usuario
    /// cancelou.
    /// </summary>
    internal bool Imprimir()
    {
        var dialogo = new Wpf.Controls.PrintDialog
        {
            UserPageRangeEnabled = _conjunto.Total > 1,
            MinPage = 1,
            MaxPage = (uint)Math.Max(1, _conjunto.Total),
        };

        AjustarPapel(dialogo);

        if (dialogo.ShowDialog() != true)
        {
            return false;
        }

        // O usuario pode ter trocado de impressora no dialogo. Refaz a escolha
        // do papel contra o driver que ele selecionou - mas SEM desfazer o que
        // ele mesmo tiver escolhido: se ele trocou o papel ou a orientacao na
        // caixa de dialogo, foi de proposito, e sobrescrever isso tirava dele
        // a unica forma de imprimir num papel diferente do que o layout pede.
        AjustarPapel(dialogo, respeitarEscolha: true);

        (int primeira, int ultima) = FaixaEscolhida(dialogo);

        RetanguloMm imprimivel = AreaImprimivelMm(dialogo);
        AjusteImpressao ajuste = EscalaImpressao.Calcular(_conjunto.ExtensaoUsada, imprimivel);
        UltimaEscala = ajuste.Escala;

        var paginador = new PaginadorFiscal(
            _renderizador,
            _conjunto,
            primeira,
            ultima,
            ResolucaoDpi(dialogo),
            ajuste,
            TamanhoPaginaDip(dialogo));

        dialogo.PrintDocument(paginador, "FiscalDoc");
        return true;
    }

    private (int Primeira, int Ultima) FaixaEscolhida(Wpf.Controls.PrintDialog d)
    {
        if (d.PageRangeSelection != Wpf.Controls.PageRangeSelection.UserPages)
        {
            return (1, _conjunto.Total);
        }

        int de = Math.Clamp(d.PageRange.PageFrom, 1, _conjunto.Total);
        int ate = Math.Clamp(d.PageRange.PageTo, de, _conjunto.Total);
        return (de, ate);
    }

    /// <summary>
    /// Escolhe orientacao e papel <b>da lista do driver</b>.
    ///
    /// <para>O documento em paisagem - DANFE tpImp 2, DACTE, DAMDFE - pede
    /// uma folha de 297 x 210 mm. O driver <b>nao lista papel deitado</b>: a
    /// lista e sempre em retrato, e quem deita a folha e a orientacao. Por
    /// isso a procura e feita com as medidas giradas. Sem isso nenhum papel
    /// era encontrado, o trabalho caia no padrao do driver em retrato, e o
    /// DACTE saia reduzido a cerca de 70% com a coluna da direita fora da
    /// folha.</para>
    ///
    /// <para>A busca tem tres degraus: papel com a <b>mesma largura</b> do
    /// documento e altura suficiente (a bobina, na termica; a propria A4, no
    /// DANFE), depois A4 (a laser comum, onde o cupom sai 1:1 no alto da
    /// folha), e por fim o menor papel em que o documento caiba. Nao achando
    /// nada, mantem-se o papel do driver e a escala cuida da diferenca.</para>
    /// </summary>
    private void AjustarPapel(Wpf.Controls.PrintDialog d, bool respeitarEscolha = false)
    {
        try
        {
            PrintTicket ticket = d.PrintTicket;
            bool deitado = _conjunto.Papel.LarguraMm > _conjunto.Papel.AlturaMm;

            PageOrientation desejada = deitado
                ? PageOrientation.Landscape
                : PageOrientation.Portrait;

            bool usuarioMudouOrientacao =
                respeitarEscolha
                && ticket.PageOrientation is { } atual
                && atual != _orientacaoSugerida;

            if (!usuarioMudouOrientacao)
            {
                ticket.PageOrientation = desejada;
            }

            _orientacaoSugerida = ticket.PageOrientation;


            // Procura sempre em retrato, que e como o driver mede.
            TamanhoPapel procurado = deitado ? _conjunto.Papel.Girado : _conjunto.Papel;

            // PrintQueue vem NULO quando nao ha impressora padrao ou o
            // servico de spool esta parado - e o getter engole a excecao antes
            // de devolver null, entao o catch abaixo nao ve nada. Sem esta
            // guarda, Ctrl+P derrubava o aplicativo com NullReferenceException
            // em vez de mostrar o aviso que o MainForm ja tem pronto.
            if (d.PrintQueue is not { } fila)
            {
                return;
            }

            PrintCapabilities caps = fila.GetPrintCapabilities(ticket);

            bool usuarioMudouPapel =
                respeitarEscolha
                && ticket.PageMediaSize?.PageMediaSizeName is { } nome
                && nome != _midiaSugerida;

            if (!usuarioMudouPapel)
            {
                PageMediaSize? escolhido = EscolherMidia(caps.PageMediaSizeCapability, procurado);

                if (escolhido is not null)
                {
                    ticket.PageMediaSize = escolhido;
                    _midiaSugerida = escolhido.PageMediaSizeName;
                }
            }

            // Sem isto, um driver cujo ticket padrao pede reducao aplica a
            // dele UMA SEGUNDA VEZ por cima da nossa, e a fidelidade
            // milimetrica vai embora sem aviso.
            ticket.PageScalingFactor = 100;

            d.PrintTicket = ticket;
        }
        catch (PrintSystemException)
        {
            // Fila indisponivel ou driver recusando o ticket: segue com o que
            // o dialogo ja tem. Nao imprimir por causa disso seria pior.
        }
    }

    /// <summary>Ver <see cref="AjustarPapel"/> para os tres degraus.</summary>
    internal static PageMediaSize? EscolherMidia(
        IReadOnlyCollection<PageMediaSize> disponiveis, TamanhoPapel desejado)
    {
        ArgumentNullException.ThrowIfNull(disponiveis);

        // Tolerancia de dois milimetros: a A4 do driver mede 210,06 mm, e uma
        // bobina de 80 mm costuma vir declarada como 80 ou como a largura
        // imprimivel, conforme o fabricante.
        const double ToleranciaMm = 2.0;

        PageMediaSize? mesmaLargura = null;
        PageMediaSize? a4 = null;
        PageMediaSize? menorQueCabe = null;
        double areaMenor = double.MaxValue;
        double alturaMesmaLargura = double.MaxValue;

        foreach (PageMediaSize p in disponiveis)
        {
            if (p.Width is not { } larguraDip || p.Height is not { } alturaDip)
            {
                continue;
            }

            double largura = larguraDip / UnidadesPorMm;
            double altura = alturaDip / UnidadesPorMm;

            if (largura <= 0 || altura <= 0)
            {
                continue;
            }

            bool cabe = largura + ToleranciaMm >= desejado.LarguraMm
                     && altura + ToleranciaMm >= desejado.AlturaMm;

            if (!cabe)
            {
                continue;
            }

            if (Math.Abs(largura - desejado.LarguraMm) <= ToleranciaMm
                && alturaDip < alturaMesmaLargura)
            {
                mesmaLargura = p;
                alturaMesmaLargura = alturaDip;
            }

            if (p.PageMediaSizeName == PageMediaSizeName.ISOA4)
            {
                a4 = p;
            }

            if (larguraDip * alturaDip < areaMenor)
            {
                menorQueCabe = p;
                areaMenor = larguraDip * alturaDip;
            }
        }

        return mesmaLargura ?? a4 ?? menorQueCabe;
    }

    /// <summary>
    /// Area que a impressora consegue marcar, em milimetros da folha.
    ///
    /// <para>Vem de <see cref="PrintCapabilities.PageImageableArea"/>, que ja
    /// respeita a orientacao do ticket - diferente do caminho antigo, onde a
    /// area imprimivel vinha sempre em retrato e precisava ser girada a
    /// mao.</para>
    /// </summary>
    private static RetanguloMm AreaImprimivelMm(Wpf.Controls.PrintDialog d)
    {
        try
        {
            if (d.PrintQueue is not { } fila)
            {
                return RecursoDoDialogo(d);
            }

            PageImageableArea? area = fila.GetPrintCapabilities(d.PrintTicket).PageImageableArea;

            if (area is not null && area.ExtentWidth > 0 && area.ExtentHeight > 0)
            {
                return new RetanguloMm(
                    (float)(area.OriginWidth / UnidadesPorMm),
                    (float)(area.OriginHeight / UnidadesPorMm),
                    (float)(area.ExtentWidth / UnidadesPorMm),
                    (float)(area.ExtentHeight / UnidadesPorMm));
            }
        }
        catch (PrintSystemException)
        {
            // Cai no recurso abaixo.
        }

        return RecursoDoDialogo(d);
    }

    /// <summary>
    /// Sem capacidades do driver: usa a area imprimivel que o proprio dialogo
    /// reporta, com origem zero.
    /// </summary>
    private static RetanguloMm RecursoDoDialogo(Wpf.Controls.PrintDialog d) =>
        new(
            0f,
            0f,
            (float)(d.PrintableAreaWidth / UnidadesPorMm),
            (float)(d.PrintableAreaHeight / UnidadesPorMm));

    private static double ResolucaoDpi(Wpf.Controls.PrintDialog d)
    {
        try
        {
            if (d.PrintTicket?.PageResolution?.X is { } x && x > 0)
            {
                return x;
            }
        }
        catch (PrintSystemException)
        {
            // Segue com o suposto.
        }

        return DpiSuposto;
    }

    private static Wpf.Size TamanhoPaginaDip(Wpf.Controls.PrintDialog d)
    {
        PageMediaSize? midia = d.PrintTicket?.PageMediaSize;

        if (midia?.Width is { } l && midia.Height is { } a && l > 0 && a > 0)
        {
            bool deitado = d.PrintTicket?.PageOrientation
                is PageOrientation.Landscape or PageOrientation.ReverseLandscape;

            // PageMediaSize e sempre em retrato; a orientacao gira a folha.
            return deitado ? new Wpf.Size(a, l) : new Wpf.Size(l, a);
        }

        return new Wpf.Size(d.PrintableAreaWidth, d.PrintableAreaHeight);
    }

    /// <summary>
    /// Estima a escala que seria aplicada na impressora padrao, sem imprimir.
    /// Serve para avisar o usuario antes, quando aquele equipamento nao
    /// comporta 1:1.
    /// </summary>
    internal static float EstimarEscala(ConjuntoPaginas conjunto)
    {
        ArgumentNullException.ThrowIfNull(conjunto);

        try
        {
            using var servidor = new LocalPrintServer();
            using PrintQueue fila = servidor.DefaultPrintQueue;

            // Copia: mutar o UserPrintTicket vivo da fila mudaria a
            // preferencia do usuario no sistema inteiro, so para estimar.
            PrintTicket? original = fila.UserPrintTicket ?? fila.DefaultPrintTicket;

            if (original is null)
            {
                return 1f;
            }

            var ticket = new PrintTicket
            {
                PageMediaSize = original.PageMediaSize,
                PageResolution = original.PageResolution,
            };

            ticket.PageOrientation = conjunto.Papel.LarguraMm > conjunto.Papel.AlturaMm
                ? PageOrientation.Landscape
                : PageOrientation.Portrait;

            PageImageableArea? area = fila.GetPrintCapabilities(ticket).PageImageableArea;

            if (area is null || area.ExtentWidth <= 0 || area.ExtentHeight <= 0)
            {
                return 1f;
            }

            var imprimivel = new RetanguloMm(
                (float)(area.OriginWidth / UnidadesPorMm),
                (float)(area.OriginHeight / UnidadesPorMm),
                (float)(area.ExtentWidth / UnidadesPorMm),
                (float)(area.ExtentHeight / UnidadesPorMm));

            return EscalaImpressao.Calcular(conjunto.ExtensaoUsada, imprimivel).Escala;
        }
        catch (Exception ex) when (ex is PrintSystemException
                                      or InvalidOperationException
                                      or NullReferenceException
                                      or System.ComponentModel.Win32Exception)
        {
            // Sem impressora instalada, driver quebrado ou spooler parado nao
            // ha o que estimar, e isso nao e erro: o usuario ainda pode
            // visualizar o documento.
            return 1f;
        }
    }

    /// <summary>
    /// Entrega as paginas ao XPS, uma por vez, desenhando cada uma so quando
    /// pedida - a display list inteira ja existe, mas os visuais nao.
    /// </summary>
    private sealed class PaginadorFiscal(
        RenderizadorWpf renderizador,
        ConjuntoPaginas conjunto,
        int primeira,
        int ultima,
        double dpi,
        AjusteImpressao ajuste,
        Wpf.Size tamanho) : DocumentPaginator
    {
        private Wpf.Size _tamanho = tamanho;

        public override bool IsPageCountValid => true;

        public override int PageCount => Math.Max(0, ultima - primeira + 1);

        public override Wpf.Size PageSize
        {
            get => _tamanho;
            set => _tamanho = value;
        }

        public override IDocumentPaginatorSource? Source => null;

        public override DocumentPage GetPage(int pageNumber)
        {
            int indice = primeira - 1 + pageNumber;

            if (indice < 0 || indice >= conjunto.Total)
            {
                return DocumentPage.Missing;
            }

            var visual = new DrawingVisual();

            using (DrawingContext dc = visual.RenderOpen())
            {
                renderizador.DesenharNoPapel(dc, conjunto.Paginas[indice], dpi, ajuste);
            }

            var caixa = new Wpf.Rect(_tamanho);
            return new DocumentPage(visual, _tamanho, caixa, caixa);
        }
    }
}
