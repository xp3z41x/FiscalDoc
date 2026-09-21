using FiscalDoc.Core.Model.Nfe;
using FiscalDoc.Core.Values;

namespace FiscalDoc.Layout.Nfce;

/// <summary>
/// Textos de redacao fixa do DANFE NFC-e, transcritos do Manual de
/// Especificacoes Tecnicas do DANFE NFC-e e QR Code, versao 6.0 (marco/2025).
///
/// <para>Reunidos aqui pelo mesmo motivo do <c>TextosMoc</c> do DANFE: sao
/// <b>normativos</b> e uma palavra trocada descaracteriza o documento. O
/// manual chega a fixar a caixa - "em caixa alta" - em dois deles.</para>
///
/// <para>Uma ausencia merece registro explicito: a frase <i>"Nao permite
/// aproveitamento de credito de ICMS"</i>, comum em cupons antigos, <b>nao
/// existe</b> no manual vigente. Ela foi retirada a pedido da Sefaz do Parana,
/// por induzir a erro quanto ao programa Nota Parana. Nao imprimimos.</para>
/// </summary>
public static class TextosNfce
{
    /// <summary>
    /// Divisao I. O manual pede o "Texto: 'Documento Auxiliar da Nota Fiscal
    /// de Consumidor Eletronica'" no cabecalho.
    /// </summary>
    public const string IdentificacaoDanfe =
        "Documento Auxiliar da Nota Fiscal de Consumidor Eletrônica";

    /// <summary>Divisao IV, primeira linha, com a redacao do manual.</summary>
    public const string ConsultaPorChave = "Consulte pela Chave de Acesso em";

    /// <summary>
    /// Divisao VI, quando o consumidor nao quis ser identificado. O manual
    /// manda "apenas nesta divisao a mensagem 'CONSUMIDOR NÃO IDENTIFICADO'".
    /// </summary>
    public const string ConsumidorNaoIdentificado = "CONSUMIDOR NÃO IDENTIFICADO";

    /// <summary>
    /// Divisao VIII. Duas linhas, "em destaque", impressas em <b>dois</b>
    /// lugares: abaixo do cabecalho e abaixo da identificacao da NFC-e.
    /// </summary>
    public const string ContingenciaLinha1 = "EMITIDA EM CONTINGÊNCIA";

    public const string ContingenciaLinha2 = "Pendente de autorização";

    /// <summary>
    /// Divisao VIII. "Para qualquer NFC-e emitida em ambiente de homologacao
    /// e obrigatorio imprimir nesta area, de forma centralizada e em caixa
    /// alta, o seguinte texto."
    /// </summary>
    public const string Homologacao =
        "EMITIDA EM AMBIENTE DE HOMOLOGAÇÃO – SEM VALOR FISCAL";

    /// <summary>
    /// Identificacao da via. O manual so cria a distincao entre vias na
    /// emissao em contingencia, onde a 2a via fica a disposicao do Fisco e
    /// leva "Via do Estabelecimento" ao lado da data e hora de emissao.
    /// </summary>
    public const string ViaConsumidor = "Via Consumidor";

    /// <summary>
    /// Divisao IX, facultativa: a carga tributaria da Lei 12.741/2012. Sai do
    /// campo vTotTrib do proprio arquivo - o manual lembra que esse campo
    /// "nao e de preenchimento obrigatorio" e tem natureza informativa.
    /// </summary>
    public static string TributosTotais(decimal valor) =>
        "Informação dos Tributos Totais Incidentes (Lei Federal nº 12.741/2012): R$ "
        + Formatos.Moeda(valor);

    /// <summary>
    /// Divisao VI: rotulo do documento do consumidor. O manual fixa as tres
    /// redacoes, em caixa alta.
    /// </summary>
    public static string RotuloConsumidor(Destinatario dest)
    {
        ArgumentNullException.ThrowIfNull(dest);

        if (!string.IsNullOrWhiteSpace(dest.Cnpj))
        {
            return "CONSUMIDOR CNPJ: " + Formatos.Cnpj(dest.Cnpj);
        }

        if (!string.IsNullOrWhiteSpace(dest.Cpf))
        {
            return "CONSUMIDOR CPF: " + Formatos.Cpf(dest.Cpf);
        }

        if (!string.IsNullOrWhiteSpace(dest.IdEstrangeiro))
        {
            return "CONSUMIDOR Id. Estrangeiro: " + dest.IdEstrangeiro;
        }

        return ConsumidorNaoIdentificado;
    }

    /// <summary>
    /// Endereco numa linha so, sem o pais - o manual pede "Endereco Completo
    /// do Emitente sem a indicacao do pais", e o mesmo formato serve ao
    /// endereco do consumidor na divisao VI.
    /// </summary>
    public static string LinhaDeEndereco(Endereco e)
    {
        ArgumentNullException.ThrowIfNull(e);

        var partes = new List<string>(5);

        if (e.LinhaLogradouro.Length > 0)
        {
            partes.Add(e.LinhaLogradouro);
        }

        if (!string.IsNullOrWhiteSpace(e.Bairro))
        {
            partes.Add(e.Bairro!);
        }

        string cidade = string.Join(
            "/",
            new[] { e.Municipio, e.Uf }.Where(x => !string.IsNullOrWhiteSpace(x)));

        if (cidade.Length > 0)
        {
            partes.Add(cidade);
        }

        if (!string.IsNullOrWhiteSpace(e.Cep))
        {
            partes.Add("CEP " + Formatos.Cep(e.Cep));
        }

        return string.Join(" - ", partes);
    }
}
