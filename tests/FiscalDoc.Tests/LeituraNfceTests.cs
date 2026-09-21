using FiscalDoc.Core.Model.Nfe;
using FiscalDoc.Core.Parsing;

namespace FiscalDoc.Tests;

/// <summary>
/// Leitura das 12 NFC-e reais, modelo 65.
///
/// A NFC-e usa o mesmo leiaute da NF-e, entao a maior parte do parser ja
/// estava exercitada pelas amostras do modelo 55. O que e novo e o que estes
/// testes cobrem: o grupo <c>infNFeSupl</c> (QR Code e endereco de consulta),
/// o grupo <c>pag</c> com troco, e o destinatario que pode simplesmente nao
/// existir - consumidor que nao quis se identificar.
/// </summary>
public sealed class LeituraNfceTests
{
    public static TheoryData<string> TodasAsReais()
    {
        var dados = new TheoryData<string>();

        foreach (FileInfo f in Amostras.ReaisNfceSemGuarda())
        {
            dados.Add(f.Name);
        }

        // Sem corpus real o provedor ainda precisa devolver UMA linha: teoria
        // com zero linhas e FALHA no xUnit, e o que se quer aqui e "ignorado".
        // O corpo do teste chama ExigirCorpusReal e a linha vira ignorada.
        if (dados.Count == 0)
        {
            dados.Add(string.Empty);
        }

        return dados;
    }

    private static NfeDocumento Ler(string nome)
    {
        Amostras.ExigirCorpusReal();

        ResultadoLeitura r = LeitorDocumento.Ler(
            Path.Combine(Amostras.Raiz.FullName, "Exemplos XML", nome));

        var ok = Assert.IsType<ResultadoLeitura.Ok>(r);
        return Assert.IsType<NfeDocumento>(ok.Documento);
    }

    private static NfeDocumento LerSintetica(string arquivo)
    {
        var ok = Assert.IsType<ResultadoLeitura.Ok>(
            LeitorDocumento.Ler(Amostras.Sintetica(arquivo)));

        return Assert.IsType<NfeDocumento>(ok.Documento);
    }

    [FatoComCorpusReal]
    public void Corpus_real_tem_doze_nfce()
    {
        Assert.Equal(12, Amostras.ReaisNfce().Length);
    }

    [TeoriaComCorpusReal]
    [MemberData(nameof(TodasAsReais))]
    public void Toda_nfce_real_e_aceita_como_modelo_65(string nome)
    {
        NfeDocumento nfce = Ler(nome);

        Assert.Equal("4.00", nfce.VersaoLeiaute);
        Assert.Equal("65", nfce.Ide.Modelo);
        Assert.True(nfce.EhNfce);
        Assert.NotEmpty(nfce.Itens);

        // O modelo embutido na chave tem de bater com ide/mod, e o DV fechar.
        Assert.NotNull(nfce.Chave);
        Assert.Equal("65", nfce.Chave!.Modelo);
        Assert.True(
            nfce.Chave.DigitoVerificadorConfere,
            $"DV nao confere para a chave {nfce.Chave.Digitos}");
    }

    [TeoriaComCorpusReal]
    [MemberData(nameof(TodasAsReais))]
    public void Toda_nfce_real_traz_qr_code_e_endereco_de_consulta(string nome)
    {
        NfeDocumento nfce = Ler(nome);

        // infNFeSupl e obrigatorio no modelo 65: sem ele nao ha divisao IV
        // nem divisao V do DANFE NFC-e.
        Assert.NotNull(nfce.Suplementares);
        Assert.False(string.IsNullOrWhiteSpace(nfce.Suplementares!.QrCode));
        Assert.False(string.IsNullOrWhiteSpace(nfce.Suplementares.UrlConsultaChave));

        // A URL do QR Code carrega a propria chave do documento.
        Assert.Contains(nfce.Chave!.Digitos, nfce.Suplementares.QrCode!, StringComparison.Ordinal);
    }

    [TeoriaComCorpusReal]
    [MemberData(nameof(TodasAsReais))]
    public void Toda_nfce_real_traz_pelo_menos_uma_forma_de_pagamento(string nome)
    {
        NfeDocumento nfce = Ler(nome);

        Assert.NotEmpty(nfce.Pagamentos.Formas);
        Assert.All(nfce.Pagamentos.Formas, p => Assert.NotNull(p.Valor));
        Assert.All(nfce.Pagamentos.Formas, p => Assert.NotEmpty(p.Rotulo));
    }

    [TeoriaComCorpusReal]
    [MemberData(nameof(TodasAsReais))]
    public void Toda_nfce_real_esta_autorizada_em_producao(string nome)
    {
        NfeDocumento nfce = Ler(nome);

        Assert.Equal(Ambiente.Producao, nfce.Ide.Ambiente);
        Assert.Equal(TipoEmissao.Normal, nfce.Ide.TipoEmissao);
        Assert.False(nfce.ContingenciaOffline);
        Assert.False(nfce.SemProtocolo);
        Assert.Equal("100", nfce.Protocolo!.CodigoStatus);
    }

    [FatoComCorpusReal]
    public void Tres_amostras_reais_nao_identificam_o_consumidor()
    {
        // NFC-e abaixo do limite de identificacao obrigatoria nao tem grupo
        // dest nenhum. O destinatario vira um registro so com nulos, e o
        // DANFE NFC-e imprime "CONSUMIDOR NAO IDENTIFICADO".
        int semConsumidor = Amostras.ReaisNfce()
            .Select(f => Ler(f.Name))
            .Count(n => n.Destinatario.Documento is null);

        Assert.Equal(3, semConsumidor);
    }

    [FatoComCorpusReal]
    public void Troco_e_lido_de_fora_do_detPag()
    {
        // vTroco e irmao dos detPag dentro de pag, e nao filho de um deles.
        // Ler no lugar errado devolveria nulo em silencio.
        NfeDocumento comTroco = Amostras.ReaisNfce()
            .Select(f => Ler(f.Name))
            .Single(n => n.Pagamentos.Troco is not null);

        Assert.Equal(43.00m, comTroco.Pagamentos.Troco);
        Assert.Equal(200.00m, comTroco.Pagamentos.Formas.Single().Valor);
    }

    [FatoComCorpusReal]
    public void Formas_de_pagamento_do_corpus_sao_dinheiro_e_pix()
    {
        var codigos = Amostras.ReaisNfce()
            .Select(f => Ler(f.Name))
            .SelectMany(n => n.Pagamentos.Formas)
            .Select(p => p.Codigo ?? string.Empty)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(c => c, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["01", "17"], codigos);

        Assert.Equal("Dinheiro", Rotulos.FormaPagamento("01"));
        Assert.Equal("Pagamento Instantâneo (PIX) - Dinâmico", Rotulos.FormaPagamento("17"));
    }

    [Fact]
    public void Codigo_de_pagamento_desconhecido_vira_o_proprio_codigo()
    {
        // A tabela do tPag ganhou codigos novos em 2023 e ganhara outros. Um
        // codigo que ela nao conhece nao pode virar rotulo aproximado.
        Assert.Equal("77", Rotulos.FormaPagamento("77"));
        Assert.Equal(string.Empty, Rotulos.FormaPagamento(null));
    }

    [Fact]
    public void Descricao_do_emissor_prevalece_sobre_a_tabela()
    {
        // tPag 99 obriga o emissor a preencher xPag, e e esse texto que vale.
        NfeDocumento nfce = LerSintetica("nfce-varios-pagamentos.xml");

        Pagamento outros = nfce.Pagamentos.Formas.Single(p => p.Codigo == "99");

        Assert.Equal("Vale-troca da loja", outros.Descricao);
        Assert.Equal("Vale-troca da loja", outros.Rotulo);
    }

    [Fact]
    public void Contingencia_offline_e_reconhecida_e_nao_tem_protocolo()
    {
        NfeDocumento nfce = LerSintetica("nfce-contingencia-offline.xml");

        Assert.Equal(TipoEmissao.ContingenciaOfflineNfce, nfce.Ide.TipoEmissao);
        Assert.True(nfce.ContingenciaOffline);
        Assert.True(nfce.SemProtocolo);
    }

    [Fact]
    public void Arquivo_sem_infNFeSupl_e_lido_sem_inventar_o_grupo()
    {
        // Arquivo irregular - o grupo e obrigatorio no modelo 65. O parser nao
        // pode preencher a lacuna, so registra que nao veio.
        NfeDocumento nfce = LerSintetica("nfce-sem-suplementares.xml");

        Assert.True(nfce.EhNfce);
        Assert.Null(nfce.Suplementares);
    }

    [Fact]
    public void Consumidor_estrangeiro_e_identificado_pelo_idEstrangeiro()
    {
        NfeDocumento nfce = LerSintetica("nfce-consumidor-estrangeiro.xml");

        Assert.Null(nfce.Destinatario.Cpf);
        Assert.Null(nfce.Destinatario.Cnpj);
        Assert.Equal("PA-AB123456", nfce.Destinatario.Documento);
    }

    [FatoComCorpusReal]
    public void Nfe_modelo_55_tambem_passou_a_carregar_o_grupo_pag()
    {
        // O grupo pag e obrigatorio no leiaute 4.00 dos dois modelos. Ele e
        // lido para a NF-e tambem - o DANFE do Anexo II e que nao tem quadro
        // para exibi-lo.
        var ok = Assert.IsType<ResultadoLeitura.Ok>(
            LeitorDocumento.Ler(Amostras.Real("RENASCENCA")));

        var nfe = Assert.IsType<NfeDocumento>(ok.Documento);

        Assert.False(nfe.EhNfce);
        Assert.NotEmpty(nfe.Pagamentos.Formas);
    }

    [FatoComCorpusReal]
    public void Titulo_da_janela_diz_nfce_e_nao_nfe()
    {
        NfeDocumento nfce = Ler(Path.GetFileName(Amostras.ReaisNfce()[0].FullName));

        Assert.StartsWith("NFC-e ", nfce.TituloCurto, StringComparison.Ordinal);
    }
}
