using FiscalDoc.Core.Model.Nfe;
using FiscalDoc.Core.Parsing;
using FiscalDoc.Core.Values;
using FiscalDoc.Layout.Barcode;
using FiscalDoc.Layout.DisplayList;
using FiscalDoc.Layout.Nfce;
using FiscalDoc.Render.Wpf;

namespace FiscalDoc.Tests;

/// <summary>
/// O DANFE NFC-e, conferido contra o Manual de Especificacoes Tecnicas do
/// DANFE NFC-e e QR Code, versao 6.0.
///
/// O manual nao publica tabela de coordenadas - "nao sao reguladas as posicoes
/// das informacoes" - entao nao ha o que conferir ao milimetro, como ha no
/// DANFE do Anexo II. O que ele publica, e o que estes testes cobrem, e:
/// quais divisoes existem, em que ordem, com que redacao literal, o que some
/// quando nao ha dado, e o que <b>nao</b> pode ser impresso.
/// </summary>
public sealed class DanfeNfceTests
{
    private static NfeDocumento LerReal(string parcial)
    {
        var ok = Assert.IsType<ResultadoLeitura.Ok>(
            LeitorDocumento.Ler(Amostras.RealNfce(parcial)));

        return Assert.IsType<NfeDocumento>(ok.Documento);
    }

    private static NfeDocumento LerSintetica(string arquivo)
    {
        var ok = Assert.IsType<ResultadoLeitura.Ok>(
            LeitorDocumento.Ler(Amostras.Sintetica(arquivo)));

        return Assert.IsType<NfeDocumento>(ok.Documento);
    }

    private static ConjuntoPaginas Montar(NfeDocumento nfce)
    {
        var medidor = new MedidorTextoWpf();
        return DanfeNfce.Construir(nfce, medidor);
    }

    private static List<string> Textos(Pagina p)
    {
        var t = new List<string>();

        foreach (Primitiva x in p.Primitivas)
        {
            if (x is Primitiva.Texto txt)
            {
                t.Add(txt.Conteudo);
            }
        }

        return t;
    }

    private static string TextoCorrido(ConjuntoPaginas c) =>
        string.Join("\n", c.Paginas.SelectMany(Textos));

    /// <summary>
    /// Todo o texto do documento numa linha so, com as linhas separadas por
    /// espaco. Serve para conferir frase que a quebra de linha partiu em duas
    /// primitivas - numa bobina de 80 mm isso acontece com qualquer frase
    /// longa em corpo de destaque, e o que importa e que a frase esteja
    /// inteira no papel.
    /// </summary>
    private static string TextoEmendado(ConjuntoPaginas c) =>
        string.Join(" ", c.Paginas.SelectMany(Textos));

    /// <summary>O mesmo sem espaco nenhum, para quebra no meio da palavra.</summary>
    private static string TextoSemEspacos(ConjuntoPaginas c) =>
        string.Concat(c.Paginas.SelectMany(Textos))
            .Replace(" ", string.Empty, StringComparison.Ordinal);

    // ================================================================ papel

    [FatoComCorpusReal]
    public void Cupom_sai_em_bobina_de_80_mm_com_a_altura_do_conteudo()
    {
        ConjuntoPaginas c = Montar(LerReal("56096"));

        Assert.Equal(1, c.Total);
        Assert.Equal(DanfeNfce.LarguraBobinaMm, c.Papel.LarguraMm);

        // Bobina nao tem folha: a altura do papel e a do conteudo mais o pe.
        // Uma altura fixa deixaria metros de papel em branco num cupom de tres
        // itens.
        Assert.True(
            c.Papel.AlturaMm < 200f,
            $"cupom de 3 itens com {c.Papel.AlturaMm:0.0} mm de papel");

        Assert.True(c.ExtensaoUsada.Base <= c.Papel.AlturaMm + 0.01f);
    }

    [FatoComCorpusReal]
    public void Toda_nfce_real_cabe_no_papel_e_respeita_as_margens_laterais()
    {
        var medidor = new MedidorTextoWpf();

        foreach (FileInfo f in Amostras.ReaisNfce())
        {
            var ok = Assert.IsType<ResultadoLeitura.Ok>(LeitorDocumento.Ler(f.FullName));
            ConjuntoPaginas c = DanfeNfce.Construir((NfeDocumento)ok.Documento, medidor);

            Assert.All(c.Paginas, p => Assert.NotEmpty(p.Primitivas));

            Assert.True(
                c.ExtensaoUsada.Base <= c.Papel.AlturaMm + 0.01f,
                $"{f.Name}: conteúdo mais alto que o papel");

            // Manual 3.1: no minimo 2 mm de margem em cada lateral.
            foreach (Pagina p in c.Paginas)
            {
                foreach (Primitiva x in p.Primitivas)
                {
                    RetanguloMm caixa = CaixaDe(x);

                    Assert.True(
                        caixa.X >= 2f - 0.01f && caixa.Direita <= c.Papel.LarguraMm - 2f + 0.01f,
                        $"{f.Name}: {x.GetType().Name} fora da margem "
                        + $"({caixa.X:0.00} a {caixa.Direita:0.00} mm)");
                }
            }
        }
    }

    private static RetanguloMm CaixaDe(Primitiva p) => p switch
    {
        Primitiva.Texto t => t.Caixa,
        Primitiva.Contorno c => c.Caixa,
        Primitiva.Preenchimento f => f.Caixa,
        Primitiva.CodigoBarras b => b.Caixa,
        Primitiva.CodigoQr q => q.Caixa,
        Primitiva.Linha l => RetanguloMm.DeCantos(
            Math.Min(l.De.X, l.Ate.X), Math.Min(l.De.Y, l.Ate.Y),
            Math.Max(l.De.X, l.Ate.X), Math.Max(l.De.Y, l.Ate.Y)),
        _ => new RetanguloMm(0, 0, 0, 0),
    };

    // ============================================================ divisoes

    [FatoComCorpusReal]
    public void Divisao_i_traz_emitente_documento_endereco_e_a_identificacao()
    {
        NfeDocumento nfce = LerReal("56096");
        string texto = TextoCorrido(Montar(nfce));

        Assert.Contains(nfce.Emitente.RazaoSocial!, texto, StringComparison.Ordinal);
        Assert.Contains(Formatos.Cnpj(nfce.Emitente.Cnpj), texto, StringComparison.Ordinal);
        Assert.Contains(nfce.Emitente.InscricaoEstadual!, texto, StringComparison.Ordinal);
        Assert.Contains(nfce.Emitente.Endereco.Municipio!, texto, StringComparison.Ordinal);

        // Texto literal que o manual manda imprimir no cabecalho.
        Assert.Contains(TextosNfce.IdentificacaoDanfe, texto, StringComparison.Ordinal);

        // "Endereco Completo do Emitente SEM a indicacao do pais."
        Assert.DoesNotContain("Brasil", texto, StringComparison.OrdinalIgnoreCase);
    }

    [FatoComCorpusReal]
    public void Divisao_ii_traz_as_seis_informacoes_obrigatorias_de_cada_item()
    {
        NfeDocumento nfce = LerReal("56096");
        ConjuntoPaginas c = Montar(nfce);
        List<string> textos = c.Paginas.SelectMany(Textos).ToList();
        string semEspacos = TextoSemEspacos(c);

        // Codigo, Descricao, Qtde, Un, Valor unit. e Valor total - o manual
        // lista as seis nominalmente.
        foreach (ItemNfe item in nfce.Itens)
        {
            Assert.Contains(item.Codigo!, textos);
            Assert.Contains(item.Unidade!, textos);
            Assert.Contains(Formatos.Quantidade(item.Quantidade), textos);
            Assert.Contains(Formatos.Moeda(item.ValorUnitario), textos);
            Assert.Contains(Formatos.Moeda(item.ValorTotal), textos);

            // A descricao pode ter sido quebrada em varias linhas - a coluna
            // tem 26 mm. O que nao pode e perder caracter no caminho.
            Assert.Contains(
                item.Descricao!.Replace(" ", string.Empty, StringComparison.Ordinal),
                semEspacos,
                StringComparison.Ordinal);
        }
    }

    [FatoComCorpusReal]
    public void Divisao_iii_traz_totais_pagamento_e_troco()
    {
        NfeDocumento nfce = LerReal("56101");
        string texto = TextoCorrido(Montar(nfce));

        Assert.Contains("Qtde. total de itens", texto, StringComparison.Ordinal);
        Assert.Contains("Valor total R$", texto, StringComparison.Ordinal);
        Assert.Contains("FORMA DE PAGAMENTO", texto, StringComparison.Ordinal);
        Assert.Contains("VALOR PAGO R$", texto, StringComparison.Ordinal);
        Assert.Contains("Dinheiro", texto, StringComparison.Ordinal);

        // Esta e a unica amostra real com troco.
        Assert.Contains("Troco R$", texto, StringComparison.Ordinal);
        Assert.Contains(Formatos.Moeda(nfce.Pagamentos.Troco), texto, StringComparison.Ordinal);
    }

    [FatoComCorpusReal]
    public void Valor_a_pagar_so_aparece_quando_ha_acrescimo_ou_desconto()
    {
        // Regra explicita do manual: "deve ser impresso apenas se existir
        // acrescimo ou desconto". Sem eles seria repetir o valor total.
        string semAjuste = TextoCorrido(Montar(LerReal("56096")));
        Assert.DoesNotContain("Valor a pagar R$", semAjuste, StringComparison.Ordinal);

        string comAjuste = TextoCorrido(Montar(LerSintetica("nfce-desconto-e-acrescimo.xml")));
        Assert.Contains("Valor a pagar R$", comAjuste, StringComparison.Ordinal);
        Assert.Contains("Desconto R$", comAjuste, StringComparison.Ordinal);
        Assert.Contains("Frete R$", comAjuste, StringComparison.Ordinal);
        Assert.Contains("Outras despesas R$", comAjuste, StringComparison.Ordinal);
    }

    [Fact]
    public void Acrescimos_nao_sao_somados_num_valor_unico()
    {
        // O manual descreve uma linha unica "Acrescimos ... /Desconto R$", mas
        // essa soma nao existe no XML e o MOC 3.1 proibe imprimir o que nao
        // consta do arquivo. Cada parcela sai com o valor que tem no arquivo.
        NfeDocumento nfce = LerSintetica("nfce-desconto-e-acrescimo.xml");
        List<string> textos = Montar(nfce).Paginas.SelectMany(Textos).ToList();

        decimal soma = (nfce.Totais.ValorFrete ?? 0m)
                     + (nfce.Totais.ValorSeguro ?? 0m)
                     + (nfce.Totais.OutrasDespesas ?? 0m);

        Assert.DoesNotContain(Formatos.Moeda(soma), textos);
        Assert.DoesNotContain("Acréscimos R$", textos);
    }

    [Fact]
    public void Soma_das_formas_de_pagamento_nao_e_impressa()
    {
        // Mesma regra: tres vPag no arquivo nao autorizam imprimir a soma.
        NfeDocumento nfce = LerSintetica("nfce-varios-pagamentos.xml");
        List<string> textos = Montar(nfce).Paginas.SelectMany(Textos).ToList();

        Assert.Equal(3, nfce.Pagamentos.Formas.Count);

        decimal soma = nfce.Pagamentos.Formas.Sum(p => p.Valor ?? 0m);
        Assert.DoesNotContain(Formatos.Moeda(soma), textos);

        // As tres formas saem, cada uma com o seu valor.
        foreach (Pagamento p in nfce.Pagamentos.Formas)
        {
            Assert.Contains(p.Rotulo, textos);
        }
    }

    [FatoComCorpusReal]
    public void Divisao_iv_traz_a_frase_do_manual_o_endereco_e_a_chave_em_blocos()
    {
        NfeDocumento nfce = LerReal("56096");
        List<string> textos = Montar(nfce).Paginas.SelectMany(Textos).ToList();

        Assert.Contains(TextosNfce.ConsultaPorChave, textos);
        Assert.Contains(nfce.Suplementares!.UrlConsultaChave!, textos);

        // Onze blocos de quatro digitos, com um espaco entre cada bloco.
        Assert.Contains(nfce.Chave!.Formatada, textos);
        Assert.DoesNotContain(nfce.Chave.Digitos, textos);
    }

    [FatoComCorpusReal]
    public void Divisao_v_traz_o_qr_code_do_arquivo_no_tamanho_minimo_do_manual()
    {
        NfeDocumento nfce = LerReal("56096");
        ConjuntoPaginas c = Montar(nfce);

        Primitiva.CodigoQr qr = c.Paginas
            .SelectMany(p => p.Primitivas)
            .OfType<Primitiva.CodigoQr>()
            .Single();

        // Manual 3.2: no minimo 25 x 25 mm.
        Assert.True(qr.Caixa.Largura >= 25f, $"QR com {qr.Caixa.Largura:0.0} mm");
        Assert.True(qr.Caixa.Altura >= 25f, $"QR com {qr.Caixa.Altura:0.0} mm");

        // O conteudo e o campo qrCode do arquivo, e nada alem dele: o simbolo
        // desenhado tem de ser identico ao que sai de codificar aquele texto.
        MatrizQr esperada = QrCode.Codificar(nfce.Suplementares!.QrCode!);

        Assert.Equal(esperada.Tamanho, qr.Matriz.Tamanho);

        for (int y = 0; y < esperada.Tamanho; y++)
        {
            for (int x = 0; x < esperada.Tamanho; x++)
            {
                Assert.Equal(esperada.Escuro(x, y), qr.Matriz.Escuro(x, y));
            }
        }
    }

    [Fact]
    public void Arquivo_sem_qr_code_nao_desenha_qr_nenhum()
    {
        // O grupo e obrigatorio no modelo 65, mas um arquivo irregular nao
        // pode virar excecao nem QR inventado.
        ConjuntoPaginas c = Montar(LerSintetica("nfce-sem-suplementares.xml"));

        Assert.Empty(c.Paginas.SelectMany(p => p.Primitivas).OfType<Primitiva.CodigoQr>());

        // A chave continua legivel, que e a outra forma de consulta.
        Assert.Contains("CHAVE DE ACESSO", TextoCorrido(c), StringComparison.Ordinal);
    }

    [TeoriaComCorpusReal]
    [InlineData("56096", "CONSUMIDOR CPF: 952.160.497-20")]
    [InlineData("56098", TextosNfce.ConsumidorNaoIdentificado)]
    public void Divisao_vi_usa_a_redacao_do_manual_para_o_consumidor(
        string amostra, string esperado)
    {
        Assert.Contains(esperado, TextoCorrido(Montar(LerReal(amostra))), StringComparison.Ordinal);
    }

    [Fact]
    public void Consumidor_estrangeiro_usa_o_terceiro_rotulo_do_manual()
    {
        string texto = TextoCorrido(Montar(LerSintetica("nfce-consumidor-estrangeiro.xml")));

        Assert.Contains("CONSUMIDOR Id. Estrangeiro: PA-AB123456", texto, StringComparison.Ordinal);
    }

    [FatoComCorpusReal]
    public void Divisao_vii_traz_numero_serie_emissao_e_protocolo()
    {
        NfeDocumento nfce = LerReal("56096");
        string texto = TextoCorrido(Montar(nfce));

        Assert.Contains(Formatos.NumeroNota(nfce.Ide.Numero), texto, StringComparison.Ordinal);
        Assert.Contains("Série " + Formatos.Serie(nfce.Ide.Serie), texto, StringComparison.Ordinal);

        // Data e hora convertidas para o horario local do emitente, que e o
        // que o proprio offset do arquivo carrega.
        Assert.Contains("08/12/2025 08:51:04", texto, StringComparison.Ordinal);

        Assert.Contains(
            "Protocolo de autorização: " + nfce.Protocolo!.Numero,
            texto,
            StringComparison.Ordinal);
    }

    // ====================================================== contingencia

    [Fact]
    public void Contingencia_repete_o_aviso_nos_dois_lugares_e_suprime_o_protocolo()
    {
        NfeDocumento nfce = LerSintetica("nfce-contingencia-offline.xml");
        List<string> textos = Montar(nfce).Paginas.SelectMany(Textos).ToList();

        // "O texto deve ser exibido em dois locais no documento": abaixo do
        // cabecalho e abaixo da identificacao da NFC-e.
        Assert.Equal(2, textos.Count(t => t == TextosNfce.ContingenciaLinha1));
        Assert.Equal(2, textos.Count(t => t == TextosNfce.ContingenciaLinha2));

        // "No caso de emissao em contingencia a informacao sobre o protocolo
        // de autorizacao sera suprimida."
        Assert.DoesNotContain(
            textos, t => t.Contains("Protocolo de autorização", StringComparison.Ordinal));

        // A via so e identificada em contingencia, que e quando existe uma
        // segunda via a ser guardada pelo estabelecimento.
        Assert.Contains(
            textos, t => t.Contains(TextosNfce.ViaConsumidor, StringComparison.Ordinal));
    }

    [FatoComCorpusReal]
    public void Fora_da_contingencia_nao_ha_aviso_nem_identificacao_de_via()
    {
        string texto = TextoCorrido(Montar(LerReal("56096")));

        Assert.DoesNotContain(TextosNfce.ContingenciaLinha1, texto, StringComparison.Ordinal);
        Assert.DoesNotContain(TextosNfce.ViaConsumidor, texto, StringComparison.Ordinal);
    }

    [FatoComCorpusReal]
    public void Homologacao_imprime_a_frase_exata_do_manual()
    {
        // Redacao literal, em caixa alta, com travessao - nao hifen. Em corpo
        // de destaque a frase nao cabe numa linha de 74 mm, entao e conferida
        // no texto emendado: o que importa e que saia inteira.
        Assert.Contains(
            TextosNfce.Homologacao,
            TextoEmendado(Montar(LerSintetica("nfce-homologacao.xml"))),
            StringComparison.Ordinal);

        // E nao aparece em producao.
        Assert.DoesNotContain(
            "HOMOLOGAÇÃO", TextoEmendado(Montar(LerReal("56096"))), StringComparison.Ordinal);
    }

    // ========================================================== divisao IX

    [FatoComCorpusReal]
    public void Mensagem_do_contribuinte_e_a_carga_tributaria_saem_no_pe()
    {
        NfeDocumento nfce = LerReal("56096");
        string texto = TextoCorrido(Montar(nfce));

        Assert.Contains(nfce.InfoAdicionais!.Complementares!, texto, StringComparison.Ordinal);

        // Lei 12.741/2012, a partir do vTotTrib do arquivo.
        Assert.Contains("Lei Federal nº 12.741/2012", texto, StringComparison.Ordinal);
        Assert.Contains(
            Formatos.Moeda(nfce.Totais.ValorTotalTributos), texto, StringComparison.Ordinal);
    }

    [FatoComCorpusReal]
    public void Frase_retirada_do_manual_nao_e_impressa()
    {
        // "Nao permite aproveitamento de credito de ICMS" saiu do manual a
        // pedido da Sefaz do Parana, por induzir a erro quanto ao Nota Parana.
        // Continua em muito cupom por ai; aqui nao.
        string texto = TextoCorrido(Montar(LerReal("56096")));

        Assert.DoesNotContain("aproveitamento de crédito", texto, StringComparison.OrdinalIgnoreCase);
    }

    // ========================================================== paginacao

    [Fact]
    public void Cupom_longo_demais_pagina_repetindo_cabecalho_e_colunas()
    {
        ConjuntoPaginas c = Montar(LerSintetica("nfce-muitos-itens.xml"));

        Assert.True(c.Total > 1, "120 itens deveriam paginar");

        // Paginado, o papel passa a ter altura fixa - uma folha, nao bobina.
        Assert.Equal(297f, c.Papel.AlturaMm);

        foreach (Pagina p in c.Paginas)
        {
            List<string> textos = Textos(p);

            // Cabecalho do emitente e das colunas em toda pagina: sem eles a
            // continuacao seria uma lista de numeros sem titulo.
            Assert.Contains(TextosNfce.IdentificacaoDanfe, textos);
            Assert.Contains("DESCRIÇÃO", textos);

            Assert.Contains(textos, t => t.StartsWith("FOLHA ", StringComparison.Ordinal));
        }

        // O QR, o consumidor e o protocolo saem uma vez so, na ultima pagina.
        Assert.Single(c.Paginas.SelectMany(p => p.Primitivas).OfType<Primitiva.CodigoQr>());
    }

    [FatoComCorpusReal]
    public void Cupom_de_uma_pagina_nao_numera_folha()
    {
        // "FOLHA 01/01" num cupom de bobina seria ruido: nao ha outra folha.
        List<string> textos = Montar(LerReal("56096")).Paginas.SelectMany(Textos).ToList();

        Assert.DoesNotContain(textos, t => t.StartsWith("FOLHA", StringComparison.Ordinal));
    }

    [FatoComCorpusReal]
    public void Bobina_mais_estreita_reescala_as_colunas_sem_estourar()
    {
        // O manual admite 56 mm de largura minima. As colunas do detalhe sao
        // proporcionais, entao o cupom continua fechando na margem.
        var medidor = new MedidorTextoWpf();

        ConjuntoPaginas c = DanfeNfce.Construir(
            LerReal("56096"), medidor, estilos: null, larguraPapelMm: 58f);

        Assert.Equal(58f, c.Papel.LarguraMm);

        foreach (Primitiva x in c.Paginas.SelectMany(p => p.Primitivas))
        {
            RetanguloMm caixa = CaixaDe(x);
            Assert.True(
                caixa.Direita <= 58f - 2f + 0.01f,
                $"{x.GetType().Name} passa da margem: {caixa.Direita:0.00} mm");
        }
    }
}
