using FiscalDoc.Core.Model;

namespace FiscalDoc.Core.Parsing;

/// <summary>
/// Resultado tipado da abertura de um arquivo. Toda falha de leitura vira um
/// caso aqui - nunca uma excecao que escapa para a interface. E o renderizador
/// desenha o estado de erro como desenharia qualquer outro conteudo, sem
/// MessageBox e sem stack trace na tela. Ver plano 2.9.
/// </summary>
public abstract record ResultadoLeitura
{
    /// <summary>Documento reconhecido e dentro do escopo.</summary>
    public sealed record Ok(DocumentoFiscal Documento) : ResultadoLeitura;

    /// <summary>XML sintaticamente invalido.</summary>
    public sealed record XmlInvalido(string Detalhe, int Linha, int Coluna) : ResultadoLeitura;

    /// <summary>XML bem formado, mas nao e documento fiscal.</summary>
    public sealed record NaoFiscal(string ElementoRaiz) : ResultadoLeitura;

    /// <summary>Documento fiscal de um modelo fora do escopo (67, 64...).</summary>
    public sealed record ModeloForaDoEscopo(string Modelo) : ResultadoLeitura;

    /// <summary>O arquivo nao pode ser lido do disco.</summary>
    public sealed record ArquivoIlegivel(string Detalhe) : ResultadoLeitura;

    /// <summary>
    /// Arquivo alem do teto que um documento fiscal justifica. Recusado sem
    /// ser materializado - ver <c>XmlSource.MaximoBytes</c>.
    /// </summary>
    public sealed record ArquivoGrandeDemais(long Bytes) : ResultadoLeitura;

    /// <summary>
    /// Mensagem pronta para a tela, em portugues, sem jargao tecnico e sem
    /// nome de excecao. Um unico lugar para todo texto de erro de leitura.
    /// </summary>
    public string MensagemUsuario => this switch
    {
        Ok => string.Empty,

        ArquivoGrandeDemais =>
            "Este arquivo é grande demais para um documento fiscal."
            + "\nSe ele realmente e uma nota, pode estar corrompido.",

        XmlInvalido(var _, var linha, var coluna) when linha > 0 =>
            $"Arquivo XML inválido (linha {linha}, coluna {coluna}).",

        XmlInvalido =>
            "Arquivo XML inválido.",

        NaoFiscal(var raiz) =>
            $"Este arquivo não é um documento fiscal.\nElemento raiz encontrado: \u201c{raiz}\u201d.",

        ModeloForaDoEscopo(var modelo) =>
            $"Documento {ModeloFiscal.Descrever(modelo)} não é suportado.\n"
            + "O FiscalDoc abre NF-e (55), NFC-e (65), CT-e (57) e MDF-e (58).",

        // Sem o detalhe da excecao: File.OpenRead("") devolve
        // "The value cannot be an empty string. (Parameter 'path')" - ingles,
        // com nome de parametro, na area do documento de um aplicativo que e
        // todo em portugues. O detalhe segue guardado, para diagnostico.
        ArquivoIlegivel =>
            "Não foi possível ler o arquivo."
            + "\nVerifique se ele ainda existe e se voce tem permissao para abri-lo.",

        _ => "Não foi possível abrir o arquivo.",
    };
}
