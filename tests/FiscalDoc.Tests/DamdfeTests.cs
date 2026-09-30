using System.Drawing;
using FiscalDoc.Core.Model.Mdfe;
using FiscalDoc.Core.Parsing;
using FiscalDoc.Layout.Barcode;
using FiscalDoc.Layout.Damdfe;
using FiscalDoc.Layout.DisplayList;
using FiscalDoc.Render.Wpf;

namespace FiscalDoc.Tests;

/// <summary>
/// O que o DAMDFE imprime, contra o manual (MOC MDF-e 3.00b, Anexo II).
/// Nasceu do QR Code: o manual o exige com no minimo 25 x 25 mm (2.3 e
/// 2.6.2), os oito modelos da secao 2.7 o poem no canto superior direito do
/// cabecalho, e o DAMDFE nao o imprimia.
///
/// <para>As amostras sao sinteticas (tools/gerar-amostras-transporte.py): a
/// comum e a de 90 documentos trazem o QR da emissao normal, a de
/// contingencia o que carrega a assinatura da chave.</para>
/// </summary>
public sealed class DamdfeTests
{
    private static MdfeDocumento Ler(string arquivo)
    {
        var ok = Assert.IsType<ResultadoLeitura.Ok>(LeitorDocumento.Ler(Amostras.Sintetica(arquivo)));
        return Assert.IsType<MdfeDocumento>(ok.Documento);
    }

    /// <summary>Le uma amostra modificada, gravada num arquivo temporario.</summary>
    private static MdfeDocumento LerVariacao(string arquivo, Func<string, string> alterar)
    {
        string xml = alterar(File.ReadAllText(Amostras.Sintetica(arquivo)));
        string caminho = Path.Combine(Path.GetTempPath(), $"damdfe-{Guid.NewGuid()}.xml");
        File.WriteAllText(caminho, xml);

        try
        {
            var ok = Assert.IsType<ResultadoLeitura.Ok>(LeitorDocumento.Ler(caminho));
            return Assert.IsType<MdfeDocumento>(ok.Documento);
        }
        finally
        {
            File.Delete(caminho);
        }
    }

    /// <summary>Tira o grupo infMDFeSupl inteiro do XML.</summary>
    private static string SemSuplementares(string xml)
    {
        int ini = xml.IndexOf("<infMDFeSupl>", StringComparison.Ordinal);
        int fim = xml.IndexOf("</infMDFeSupl>", StringComparison.Ordinal) + "</infMDFeSupl>".Length;
        return xml.Remove(ini, fim - ini);
    }

    private static ConjuntoPaginas Montar(MdfeDocumento mdfe) =>
        DamdfeLayout.Construir(mdfe, new MedidorTextoWpf());

    private static List<Primitiva.CodigoQr> Qrs(ConjuntoPaginas c) =>
        c.Paginas.SelectMany(p => p.Primitivas.OfType<Primitiva.CodigoQr>()).ToList();

    private static Primitiva.Texto TextoUnico(Pagina p, Func<string, bool> filtro) =>
        p.Primitivas.OfType<Primitiva.Texto>().Single(t => filtro(t.Conteudo));

    // ============================================================== leitura

    [Theory]
    [InlineData("mdfe-300-rodoviario.xml", false)]
    [InlineData("mdfe-300-contingencia.xml", true)]
    public void Qr_code_cita_a_chave_do_manifesto_e_so_a_contingencia_assina(string arquivo, bool contingencia)
    {
        // Visao Geral 9.2: URL do portal, chMDFe e tpAmb; na contingencia
        // off-line, tambem o sign. O Anexo I rejeita chave divergente (F116),
        // sign ausente na contingencia (F117) e sign na emissao normal (F118).
        MdfeDocumento m = Ler(arquivo);

        Assert.Equal(contingencia, m.EmContingencia);
        Assert.StartsWith("https://", m.QrCode, StringComparison.Ordinal);
        Assert.Contains($"?chMDFe={m.Chave!.Caracteres}&tpAmb=1", m.QrCode, StringComparison.Ordinal);
        Assert.Equal(contingencia, m.QrCode!.Contains("&sign=", StringComparison.Ordinal));
    }

    [Fact]
    public void Qr_code_e_achado_tambem_dentro_de_infMDFe()
    {
        // O schema o poe ao lado de infMDFe; um arquivo que o grave dentro
        // ainda traz o QR Code.
        string? esperado = Ler("mdfe-300-rodoviario.xml").QrCode;

        MdfeDocumento m = LerVariacao("mdfe-300-rodoviario.xml", xml =>
        {
            int ini = xml.IndexOf("<infMDFeSupl>", StringComparison.Ordinal);
            int fim = xml.IndexOf("</infMDFeSupl>", StringComparison.Ordinal) + "</infMDFeSupl>".Length;
            return SemSuplementares(xml).Replace("</infMDFe>", xml[ini..fim] + "</infMDFe>", StringComparison.Ordinal);
        });

        Assert.NotNull(esperado);
        Assert.Equal(esperado, m.QrCode);
    }

    [Fact]
    public void Arquivo_sem_infMDFeSupl_nao_ganha_qr_code()
    {
        // O grupo e 0-1 no schema - a obrigatoriedade veio por regra de
        // validacao (Anexo I, F114), com data propria -, e um manifesto antigo
        // pode nao traze-lo. Nada e inventado: sem o grupo, sem simbolo. A
        // chave continua la, que e a outra forma de consulta.
        MdfeDocumento m = LerVariacao("mdfe-300-rodoviario.xml", SemSuplementares);

        Assert.Null(m.QrCode);

        ConjuntoPaginas c = Montar(m);

        Assert.Empty(Qrs(c));
        Assert.Contains(
            m.Chave!.Formatada,
            c.Paginas[0].Primitivas.OfType<Primitiva.Texto>().Select(t => t.Conteudo));
    }

    // ============================================================== desenho

    [Fact]
    public void Qr_code_aparece_em_toda_folha()
    {
        // O cabecalho se repete em toda folha, e o QR vai junto.
        ConjuntoPaginas c = Montar(Ler("mdfe-300-muitos-documentos.xml"));

        Assert.True(c.Total > 1);
        Assert.All(c.Paginas, p => Assert.Single(p.Primitivas.OfType<Primitiva.CodigoQr>()));
    }

    [Theory]
    [InlineData("mdfe-300-rodoviario.xml")]
    [InlineData("mdfe-300-contingencia.xml")]
    public void Qr_code_desenhado_e_o_do_arquivo_e_nada_alem(string arquivo)
    {
        // O conteudo e o qrCodMDFe, sem recalculo - nem o sign da contingencia,
        // que so o certificado do emitente sabe produzir.
        MdfeDocumento m = Ler(arquivo);
        MatrizQr esperada = QrCode.Codificar(m.QrCode!);

        List<Primitiva.CodigoQr> qrs = Qrs(Montar(m));
        Assert.NotEmpty(qrs);

        foreach (Primitiva.CodigoQr qr in qrs)
        {
            Assert.Equal(esperada.Tamanho, qr.Matriz.Tamanho);

            for (int y = 0; y < esperada.Tamanho; y++)
            {
                for (int x = 0; x < esperada.Tamanho; x++)
                {
                    Assert.Equal(esperada.Escuro(x, y), qr.Matriz.Escuro(x, y));
                }
            }
        }
    }

    [Theory]
    [InlineData("mdfe-300-rodoviario.xml")]
    [InlineData("mdfe-300-contingencia.xml")]
    public void Qr_code_fica_no_canto_superior_direito_do_cabecalho(string arquivo)
    {
        // Os oito modelos da secao 2.7 - normal e contingencia dos quatro
        // modais - poem o QR no alto da folha, a direita do emitente, da
        // identificacao e da chave de acesso.
        MdfeDocumento m = Ler(arquivo);
        ConjuntoPaginas c = Montar(m);
        Pagina p = c.Paginas[0];

        RetanguloMm qr = p.Primitivas.OfType<Primitiva.CodigoQr>().Single().Caixa;
        RetanguloMm sigla = TextoUnico(p, t => t == "DAMDFE").Caixa;
        RetanguloMm chave = TextoUnico(p, t => t == m.Chave!.Formatada).Caixa;
        RetanguloMm primeiroQuadro = TextoUnico(p, t => t == "EMITENTE").Caixa;

        Assert.True(qr.Y < sigla.Base, $"o QR comeca em {qr.Y:0.0} mm, abaixo do titulo");
        Assert.True(qr.Base <= primeiroQuadro.Y, "o QR invade o corpo do documento");
        Assert.True(qr.X >= chave.Direita, "ha campo do cabecalho a direita do QR");
        Assert.True(c.ExtensaoUsada.Direita - qr.Direita < 2f, "o QR nao esta junto da margem direita");
    }

    [Fact]
    public void Qr_code_tem_ao_menos_os_25_mm_do_manual()
    {
        // Anexo II, 2.3 e 2.6.2: "tamanho minimo 25 mm x 25 mm".
        ConjuntoPaginas c = Montar(Ler("mdfe-300-rodoviario.xml"));
        Primitiva.CodigoQr qr = c.Paginas[0].Primitivas.OfType<Primitiva.CodigoQr>().Single();

        Assert.True(qr.Caixa.Largura >= 25f, $"QR com {qr.Caixa.Largura} mm");
        Assert.Equal(qr.Caixa.Largura, qr.Caixa.Altura);
    }

    [Theory]
    [InlineData("mdfe-300-rodoviario.xml")]
    [InlineData("mdfe-300-contingencia.xml")]
    public void Qr_code_impresso_a_300_dpi_ainda_tem_os_25_mm_do_manual(string arquivo)
    {
        // A caixa e a reserva do layout; o manual mede o simbolo impresso, e no
        // papel o renderizador arredonda o modulo para baixo, em pontos
        // inteiros da impressora. O pior caso e a contingencia: o sign leva o
        // simbolo da versao 6 para a 17, e a 300 dpi - a resolucao minima que o
        // manual pede para o codigo de barras (2.2) - o modulo cai para tres
        // pontos. Mede-se pela tinta, que vai de uma borda a outra dos padroes
        // de localizacao: a matriz inteira.
        const double Dpi = 300;
        const double PxPorMm = Dpi / 25.4;

        ConjuntoPaginas c = Montar(Ler(arquivo));
        Primitiva.CodigoQr qr = c.Paginas[0].Primitivas.OfType<Primitiva.CodigoQr>().Single();

        using Bitmap bmp = RenderTeste.Papel(c.Paginas[0], c.Papel, Dpi);
        double moduloMm = LarguraDaTintaPx(bmp, qr.Caixa, PxPorMm) / PxPorMm / qr.Matriz.Tamanho;

        // A imagem e o conteudo mais a margem segura, que vem em modulos junto
        // com a matriz - e cumpre os 10% do manual, ver QrCodeTests.
        double imagemMm = moduloMm * (qr.Matriz.Tamanho + (2 * qr.MargemModulos));

        Assert.True(imagemMm >= 25.0, $"imagem de {imagemMm:0.00} mm, modulo de {moduloMm:0.000} mm");
    }

    /// <summary>Largura, em pixels, da tinta dentro da caixa do QR.</summary>
    private static int LarguraDaTintaPx(Bitmap bmp, RetanguloMm caixa, double pxPorMm)
    {
        int x0 = (int)(caixa.X * pxPorMm);
        int x1 = (int)Math.Ceiling(caixa.Direita * pxPorMm);
        int y0 = (int)(caixa.Y * pxPorMm);
        int y1 = (int)Math.Ceiling(caixa.Base * pxPorMm);

        int min = int.MaxValue;
        int max = int.MinValue;

        for (int y = y0; y < y1; y++)
        {
            for (int x = x0; x < x1; x++)
            {
                if (bmp.GetPixel(x, y).R < 128)
                {
                    min = Math.Min(min, x);
                    max = Math.Max(max, x);
                }
            }
        }

        Assert.True(max >= min, "nenhuma tinta dentro da caixa do QR");
        return max - min + 1;
    }

    [Fact]
    public void Qr_code_maior_que_o_simbolo_perde_o_simbolo_e_nao_o_documento()
    {
        MdfeDocumento m = LerVariacao("mdfe-300-rodoviario.xml", xml =>
        {
            int ini = xml.IndexOf("<qrCodMDFe>", StringComparison.Ordinal) + "<qrCodMDFe>".Length;
            int fim = xml.IndexOf("</qrCodMDFe>", StringComparison.Ordinal);
            return xml[..ini] + new string('A', 3000) + xml[fim..];
        });

        ConjuntoPaginas c = Montar(m);

        Assert.True(c.Total >= 1);
        Assert.Empty(Qrs(c));
    }

    // ========================================================== diagramacao

    [Fact]
    public void Identificacao_do_documento_cabe_na_coluna_estreitada_pelo_qr()
    {
        // Com o QR, a coluna de identificacao estreita para uns 32 mm, e a
        // descricao do DAMDFE e mais longa que a do DACTE. Titulo, descricao,
        // numero, serie e folha tem de caber em sequencia, sem sobrepor, antes
        // da faixa de baixo.
        var medidor = new MedidorTextoWpf();
        Pagina p = DamdfeLayout.Construir(Ler("mdfe-300-rodoviario.xml"), medidor).Paginas[0];

        Primitiva.Texto descricao = TextoUnico(p, t => t.StartsWith("Documento Auxiliar", StringComparison.Ordinal));

        int linhas = medidor.Quebrar(descricao.Conteudo, descricao.Estilo, descricao.Caixa.Largura).Count;
        float necessaria = linhas * medidor.AlturaLinhaMm(descricao.Estilo);

        Assert.True(necessaria <= descricao.Caixa.Altura + 0.01f,
            $"{linhas} linhas pedem {necessaria:0.00} mm numa caixa de {descricao.Caixa.Altura:0.00} mm");

        RetanguloMm[] coluna =
        [
            TextoUnico(p, t => t == "DAMDFE").Caixa,
            descricao.Caixa,
            TextoUnico(p, t => t.StartsWith("N. ", StringComparison.Ordinal)).Caixa,
            TextoUnico(p, t => t.StartsWith("SÉRIE ", StringComparison.Ordinal)).Caixa,
            TextoUnico(p, t => t.StartsWith("FOLHA ", StringComparison.Ordinal)).Caixa,
            TextoUnico(p, t => t == "CNPJ / CPF DO EMITENTE").Caixa,
        ];

        for (int i = 1; i < coluna.Length; i++)
        {
            Assert.True(coluna[i - 1].Base <= coluna[i].Y + 0.01f,
                $"a linha {i} da coluna de identificacao sobrepoe a seguinte");
        }
    }
}
