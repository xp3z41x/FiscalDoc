using FiscalDoc.Core.Model.Nfe;
using FiscalDoc.Core.Parsing;

namespace FiscalDoc.Tests;

/// <summary>
/// Variacoes que o corpus real nao cobre, derivadas dele por
/// tools/gerar-amostras-sinteticas.py.
/// </summary>
public sealed class VariacoesTests
{
    private static NfeDocumento Ler(string nomeArquivo)
    {
        ResultadoLeitura r = LeitorDocumento.Ler(Amostras.Sintetica(nomeArquivo));
        var ok = Assert.IsType<ResultadoLeitura.Ok>(r);
        return Assert.IsType<NfeDocumento>(ok.Documento);
    }

    [Fact]
    public void Arquivo_iso88591_e_lido_com_acentos_corretos()
    {
        NfeDocumento nfe = Ler("encoding-iso88591.xml");

        // Se o encoding fosse tratado como UTF-8, isto viria com caractere de
        // substituicao no lugar dos acentos.
        string texto = nfe.Emitente.RazaoSocial + nfe.Emitente.Endereco.Municipio
                       + nfe.Destinatario.RazaoSocial;

        Assert.DoesNotContain('�', texto);
        Assert.NotEmpty(nfe.Itens);
    }

    [Fact]
    public void Arquivo_latin1_declarado_como_utf8_ainda_abre()
    {
        // Emissor desleixado: declara UTF-8 e grava bytes Latin-1. E melhor
        // abrir com um acento possivelmente torto do que recusar um documento
        // que o usuario le em qualquer outro visualizador.
        NfeDocumento nfe = Ler("encoding-latin1-declarado-utf8.xml");

        Assert.NotEmpty(nfe.Itens);
        Assert.NotNull(nfe.Chave);
    }

    [FatoComCorpusReal]
    public void Arquivo_sem_declaracao_de_encoding_vale_utf8()
    {
        // Amostra real, nao sintetica: o RINALDI de 41,8 KB nao declara
        // encoding, e o padrao XML manda assumir UTF-8.
        string caminho = Amostras.PaisagemComMaisItens();
        ResultadoLeitura r = LeitorDocumento.Ler(caminho);

        Assert.IsType<ResultadoLeitura.Ok>(r);
    }

    [Fact]
    public void Documento_sem_protocolo_e_lido_e_sinalizado()
    {
        NfeDocumento nfe = Ler("sem-protocolo.xml");

        // Nao e erro: em contingencia FS/FS-DA o DANFE sai antes da
        // autorizacao. O MOC proibe sintetizar protocolo, entao fica nulo.
        Assert.True(nfe.SemProtocolo);
        Assert.Null(nfe.Protocolo);
        Assert.NotEmpty(nfe.Itens);
    }

    [Fact]
    public void Nfe_sem_envelope_nfeproc_e_lida()
    {
        NfeDocumento nfe = Ler("sem-envelope.xml");

        Assert.NotNull(nfe.Chave);
        Assert.NotEmpty(nfe.Itens);
        Assert.True(nfe.SemProtocolo);
    }

    [Fact]
    public void Homologacao_exige_sem_valor_fiscal()
    {
        NfeDocumento nfe = Ler("homologacao.xml");

        Assert.Equal(Ambiente.Homologacao, nfe.Ide.Ambiente);
        Assert.True(nfe.ExigeSemValorFiscal);
    }

    [Theory]
    [InlineData("contingencia-fsda.xml", TipoEmissao.ContingenciaFsDa,
        "DANFE em Contingência - impresso em decorrência de problemas técnicos")]
    [InlineData("contingencia-epec.xml", TipoEmissao.ContingenciaEpec,
        "DANFE impresso em contingência - EPEC regularmente recebida pela Receita Federal do Brasil")]
    public void Contingencia_carrega_o_dizer_obrigatorio(
        string arquivo, TipoEmissao esperado, string dizer)
    {
        NfeDocumento nfe = Ler(arquivo);

        Assert.Equal(esperado, nfe.Ide.TipoEmissao);
        Assert.Equal(dizer, Rotulos.DizerContingencia(esperado));
    }

    [Fact]
    public void Svc_an_nao_tem_dizer_proprio_mas_exige_motivo_e_data()
    {
        NfeDocumento nfe = Ler("contingencia-svcan.xml");

        Assert.Equal(TipoEmissao.ContingenciaSvcAn, nfe.Ide.TipoEmissao);

        // SVC nao tem legenda propria; o que se imprime e xJust e dhCont.
        Assert.Null(Rotulos.DizerContingencia(TipoEmissao.ContingenciaSvcAn));
        Assert.True(Rotulos.ExigeJustificativaContingencia(TipoEmissao.ContingenciaSvcAn));

        Assert.NotNull(nfe.Ide.JustificativaContingencia);
        Assert.NotNull(nfe.Ide.DataHoraContingencia);
    }

    [Fact]
    public void Emissao_normal_nao_tem_dizer_de_contingencia()
    {
        Assert.Null(Rotulos.DizerContingencia(TipoEmissao.Normal));
        Assert.False(Rotulos.ExigeJustificativaContingencia(TipoEmissao.Normal));
    }

    [FatoComCorpusReal]
    public void Quadro_issqn_aparece_so_quando_o_grupo_existe()
    {
        NfeDocumento com = Ler("com-issqn.xml");
        Assert.NotNull(com.Issqn);
        Assert.Equal(1500.00m, com.Issqn!.ValorTotalServicos);
        Assert.Equal(75.00m, com.Issqn.ValorIssqn);

        // Nenhuma amostra real tem ISSQN: o quadro tem de sumir, e o MOC
        // 3.3.3 permite redistribuir a altura liberada.
        foreach (FileInfo f in Amostras.ReaisNfe())
        {
            ResultadoLeitura r = LeitorDocumento.Ler(f.FullName);
            var ok = Assert.IsType<ResultadoLeitura.Ok>(r);
            Assert.Null(((NfeDocumento)ok.Documento).Issqn);
        }
    }

    [Fact]
    public void Destinatario_pessoa_fisica_usa_cpf()
    {
        NfeDocumento nfe = Ler("destinatario-cpf.xml");

        Assert.Null(nfe.Destinatario.Cnpj);

        // CPF ficticio com DV valido, produzido pela anonimizacao das amostras
        // (tools/anonimizar.py) - as amostras vao para o repositorio publico.
        Assert.Equal("11144477735", nfe.Destinatario.Cpf);
        Assert.Equal("11144477735", nfe.Destinatario.Documento);
    }
}
