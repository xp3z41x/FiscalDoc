using System.Drawing;
using System.Drawing.Imaging;
using FiscalDoc.Core.Model.Nfe;
using FiscalDoc.Core.Parsing;
using FiscalDoc.Layout;
using FiscalDoc.Core.Model;
using FiscalDoc.Core.Model.Cte;
using FiscalDoc.Core.Model.Evento;
using FiscalDoc.Core.Model.Mdfe;
using FiscalDoc.Layout.Dacte;
using FiscalDoc.Layout.Damdfe;
using FiscalDoc.Layout.Danfe;
using FiscalDoc.Layout.Evento;
using FiscalDoc.Layout.Nfce;
using FiscalDoc.Layout.DisplayList;
using FiscalDoc.Render.Wpf;

namespace FiscalDoc.Tests;

/// <summary>
/// Renderiza amostras para PNG em tests/Saida, para conferencia visual.
///
/// Nao e teste de regressao - e a lupa. Nenhuma assercao substitui olhar para
/// um DANFE e ver que o quadro do emitente esta torto. Roda junto com a suite
/// porque assim as imagens estao sempre atualizadas com o codigo.
/// </summary>
public sealed class GeradorVisual
{
    /// <summary>150 dpi: legivel na tela sem gerar arquivo gigante.</summary>
    private const float Dpi = 150f;

    internal static string PastaSaida
    {
        get
        {
            string p = Path.Combine(Amostras.Raiz.FullName, "tests", "Saida");
            Directory.CreateDirectory(p);
            return p;
        }
    }

    /// <summary>
    /// Rasteriza no modo <b>papel</b>: e o que a impressora recebe, so que
    /// revelado numa resolucao fixa em vez de ir para o RIP.
    /// </summary>
    internal static Bitmap RenderizarPagina(Pagina pagina, TamanhoPapel papel, float dpi = Dpi) =>
        RenderTeste.Papel(pagina, papel, dpi);

    private static NfeDocumento Ler(string caminho)
    {
        var ok = Assert.IsType<ResultadoLeitura.Ok>(LeitorDocumento.Ler(caminho));
        return Assert.IsType<NfeDocumento>(ok.Documento);
    }

    [Fact]
    public void Gerar_pngs_para_conferencia_visual()
    {
        var medidor = new MedidorTextoWpf();

        // As sinteticas sempre; as reais so quando o corpus esta presente. O
        // corpus real nao acompanha o repositorio, e quem acabou de clonar
        // ainda assim quer as PNGs para conferir o desenho a olho.
        var alvos = new List<(string Rotulo, string Caminho)>
        {
            ("05-homologacao", Amostras.Sintetica("homologacao.xml")),
            ("06-contingencia-fsda", Amostras.Sintetica("contingencia-fsda.xml")),
            ("07-sem-protocolo", Amostras.Sintetica("sem-protocolo.xml")),
            ("08-com-issqn", Amostras.Sintetica("com-issqn.xml")),
            ("11-reforma-is-e-total", Amostras.Sintetica("reforma-com-is-e-total.xml")),
        };

        if (Amostras.TemCorpusReal)
        {
            alvos.AddRange(
            [
                ("01-simples-2itens", Amostras.RetratoComMenosItens()),
                ("02-medio-10itens", Amostras.Real("RENASCENCA")),
                ("03-longo-99itens", Amostras.Real("ZOUIL")),
                ("04-infadprod-74itens", Amostras.Real("DALMASIO")),
                ("09-paisagem-27itens", Amostras.PaisagemComMaisItens()),
                ("10-paisagem-2itens", Amostras.PaisagemComMenosItens()),
            ]);
        }

        foreach ((string rotulo, string caminho) in alvos)
        {
            NfeDocumento nfe = Ler(caminho);

            // tpImp decide a orientacao, exatamente como o aplicativo faz.
            ConjuntoPaginas c = nfe.Paisagem
                ? DanfePaisagem.Construir(nfe, medidor)
                : DanfeRetrato.Construir(nfe, medidor);

            // So as duas primeiras paginas: o que interessa conferir a olho e
            // a primeira e a continuacao.
            int quantas = Math.Min(2, c.Total);

            for (int i = 0; i < quantas; i++)
            {
                using Bitmap bmp = RenderizarPagina(c.Paginas[i], c.Papel);
                string nome = quantas == 1 ? $"{rotulo}.png" : $"{rotulo}-p{i + 1}de{c.Total}.png";
                bmp.Save(Path.Combine(PastaSaida, nome), ImageFormat.Png);
            }
        }

        // NFC-e: bobina de 80 mm, altura do conteudo. O layout sai do modelo
        // do documento, e nao de uma escolha do gerador.
        var cupons = new List<(string Rotulo, string Caminho)>
        {
            ("54-nfce-contingencia", Amostras.Sintetica("nfce-contingencia-offline.xml")),
            ("55-nfce-homologacao", Amostras.Sintetica("nfce-homologacao.xml")),
            ("56-nfce-desconto", Amostras.Sintetica("nfce-desconto-e-acrescimo.xml")),
            ("57-nfce-varios-pagamentos", Amostras.Sintetica("nfce-varios-pagamentos.xml")),
            ("58-nfce-estrangeiro", Amostras.Sintetica("nfce-consumidor-estrangeiro.xml")),
            ("59-nfce-120itens", Amostras.Sintetica("nfce-muitos-itens.xml")),
            ("5A-nfce-sem-qrcode", Amostras.Sintetica("nfce-sem-suplementares.xml")),
        };

        if (Amostras.TemCorpusReal)
        {
            cupons.AddRange(
            [
                ("50-nfce-3itens", Amostras.RealNfce("56096")),
                ("51-nfce-14itens", Amostras.RealNfce("56100")),
                ("52-nfce-sem-consumidor", Amostras.RealNfce("56098")),
                ("53-nfce-com-troco", Amostras.RealNfce("56101")),
            ]);
        }

        foreach ((string rotulo, string caminho) in cupons)
        {
            ConjuntoPaginas c = DanfeNfce.Construir(Ler(caminho), medidor);
            int quantas = Math.Min(2, c.Total);

            for (int i = 0; i < quantas; i++)
            {
                using Bitmap bmp = RenderizarPagina(c.Paginas[i], c.Papel);
                string nome = c.Total == 1 ? $"{rotulo}.png" : $"{rotulo}-p{i + 1}de{c.Total}.png";
                bmp.Save(Path.Combine(PastaSaida, nome), ImageFormat.Png);
            }
        }

        // CT-e, MDF-e e eventos: o layout sai do tipo do documento lido, e nao
        // de uma escolha do gerador - exatamente como no aplicativo.
        (string Rotulo, string Arquivo)[] transporte =
        [
            ("20-dacte-400", "cte-400-rodoviario.xml"),
            ("21-dacte-300", "cte-300-rodoviario.xml"),
            ("22-dacte-contingencia", "cte-400-contingencia.xml"),
            ("23-dacte-homologacao", "cte-400-homologacao.xml"),
            ("24-dacte-48-documentos", "cte-400-muitos-documentos.xml"),
            ("30-damdfe", "mdfe-300-rodoviario.xml"),
            ("31-damdfe-90-documentos", "mdfe-300-muitos-documentos.xml"),
            ("32-damdfe-contingencia", "mdfe-300-contingencia.xml"),
            ("40-evento-cce", "evento-nfe-cce.xml"),
            ("41-evento-cancelamento", "evento-nfe-cancelamento.xml"),
            ("42-evento-cte-entrega", "evento-cte-entrega.xml"),
        ];

        foreach ((string rotulo, string arquivo) in transporte)
        {
            var ok = Assert.IsType<ResultadoLeitura.Ok>(
                LeitorDocumento.Ler(Amostras.Sintetica(arquivo)));

            ConjuntoPaginas c = ok.Documento switch
            {
                CteDocumento cte => DacteLayout.Construir(cte, medidor),
                MdfeDocumento mdfe => DamdfeLayout.Construir(mdfe, medidor),
                EventoDocumento ev => EventoLayout.Construir(ev, medidor),
                _ => throw new InvalidOperationException($"tipo inesperado em {arquivo}"),
            };

            int quantas = Math.Min(2, c.Total);

            for (int i = 0; i < quantas; i++)
            {
                using Bitmap bmp = RenderizarPagina(c.Paginas[i], c.Papel);
                string nome = c.Total == 1 ? $"{rotulo}.png" : $"{rotulo}-p{i + 1}de{c.Total}.png";
                bmp.Save(Path.Combine(PastaSaida, nome), ImageFormat.Png);
            }
        }

        // Pagina de calibracao tambem.
        ConjuntoPaginas cal = PaginaCalibracao.Construir(TamanhoPapel.A4);
        using (Bitmap bmp = RenderizarPagina(cal.Paginas[0], cal.Papel))
        {
            bmp.Save(Path.Combine(PastaSaida, "00-calibracao.png"), ImageFormat.Png);
        }

        Assert.NotEmpty(Directory.GetFiles(PastaSaida, "*.png"));
    }
}
