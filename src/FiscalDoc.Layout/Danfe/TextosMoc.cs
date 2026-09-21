using System.Text;
using FiscalDoc.Core.Model.Nfe;
using FiscalDoc.Core.Values;

namespace FiscalDoc.Layout.Danfe;

/// <summary>
/// Textos de redacao fixa do DANFE, transcritos do MOC.
///
/// Ficam reunidos aqui por dois motivos. Primeiro, sao <b>normativos</b>: o
/// MOC 7.0 Anexo III define a redacao exata dos dizeres de contingencia, e uma
/// palavra trocada descaracteriza o documento. Segundo, e o unico lugar do
/// layout onde ha texto em portugues corrido - deixa-lo espalhado tornaria
/// impossivel conferir contra a norma.
/// </summary>
public static class TextosMoc
{
    /// <summary>
    /// Campo 1 do quadro de identificacao (MOC Anexo II 3.9), logo abaixo da
    /// chave de acesso. O texto muda conforme a forma de emissao.
    /// </summary>
    public static string Consulta(TipoEmissao tipo) => tipo switch
    {
        // 3.9.3 - EPEC tem endereco proprio, sem "ou no site da Sefaz".
        TipoEmissao.ContingenciaEpec =>
            "Consulta de autenticidade no portal da NF-e www.nfe.fazenda.gov.br/portal",

        // 3.9.2 - em FS e FS-DA este campo recebe o SEGUNDO codigo de barras,
        // com os 36 caracteres de "Dados da NF-e". Enquanto esse segundo
        // codigo nao for emitido, o campo fica sem a mensagem de consulta, que
        // ali nao cabe.
        TipoEmissao.ContingenciaFsIa or TipoEmissao.ContingenciaFsDa =>
            string.Empty,

        // 3.9.1 - normal, SVC-AN e SVC-RS.
        _ =>
            "Consulta de autenticidade no portal nacional da NF-e "
            + "www.nfe.fazenda.gov.br/portal ou no site da Sefaz Autorizadora",
    };

    /// <summary>Rotulo do campo 2 (MOC Anexo II 3.9).</summary>
    public static string RotuloProtocolo(TipoEmissao tipo) => tipo switch
    {
        TipoEmissao.ContingenciaEpec => "PROTOCOLO DE AUTORIZAÇÃO DO EPEC",
        TipoEmissao.ContingenciaFsIa or TipoEmissao.ContingenciaFsDa => "DADOS DA NF-E",
        _ => "PROTOCOLO DE AUTORIZAÇÃO DE USO",
    };

    /// <summary>
    /// Monta o conteudo de INFORMACOES COMPLEMENTARES na ordem em que o MOC
    /// exige que as informacoes obrigatorias apareçam, seguidas do texto livre
    /// do emitente.
    /// </summary>
    public static string MontarInformacoesComplementares(NfeDocumento nfe)
    {
        var sb = new StringBuilder();

        // 1. Homologacao. Secao 3 do Anexo II: o DANFE de ambiente de
        //    homologacao "sempre devera conter a frase SEM VALOR FISCAL" em
        //    Informacoes Complementares ou em marca d'agua destacada.
        if (nfe.ExigeSemValorFiscal)
        {
            Acrescentar(sb, "SEM VALOR FISCAL - EMITIDO EM AMBIENTE DE HOMOLOGAÇÃO");
        }

        // 2. Dizer de contingencia (MOC 7.0 Anexo III), no corpo do documento.
        string? dizer = Rotulos.DizerContingencia(nfe.Ide.TipoEmissao);
        if (dizer is not null)
        {
            Acrescentar(sb, dizer.ToUpperInvariant());
        }

        // 3. Motivo e inicio da contingencia, obrigatorios em FS, FS-DA e SVC.
        if (Rotulos.ExigeJustificativaContingencia(nfe.Ide.TipoEmissao))
        {
            if (nfe.Ide.DataHoraContingencia is { } dh)
            {
                Acrescentar(sb, $"Início da contingência: {Formatos.DataHora(dh)}");
            }

            if (!string.IsNullOrWhiteSpace(nfe.Ide.JustificativaContingencia))
            {
                Acrescentar(sb, $"Motivo: {nfe.Ide.JustificativaContingencia}");
            }
        }

        // 4. Informacoes de interesse do fisco (infAdFisco) vem antes das do
        //    contribuinte.
        if (!string.IsNullOrWhiteSpace(nfe.InfoAdicionais?.FiscoInteresse))
        {
            Acrescentar(sb, nfe.InfoAdicionais!.FiscoInteresse!);
        }

        // 5. Texto livre do emitente.
        if (!string.IsNullOrWhiteSpace(nfe.InfoAdicionais?.Complementares))
        {
            Acrescentar(sb, nfe.InfoAdicionais!.Complementares!);
        }

        return sb.ToString();
    }

    private static void Acrescentar(StringBuilder sb, string texto)
    {
        if (sb.Length > 0)
        {
            sb.Append('\n');
        }

        sb.Append(texto);
    }
}
