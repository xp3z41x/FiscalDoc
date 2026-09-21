namespace FiscalDoc.Tests;

/// <summary>
/// <c>[Fact]</c> para teste que so faz sentido sobre documento fiscal de
/// verdade.
///
/// <para>O corpus real <b>nao acompanha o repositorio</b>: sao notas emitidas,
/// com CPF e nome completo de oito pessoas e CNPJ, endereco, telefone e dado
/// comercial de vinte e duas empresas. Esse dado nao e nosso para publicar.
/// Quem clona recebe as amostras sinteticas de <c>tests/Amostras</c>, que o
/// gerador em <c>tools/</c> reconstroi a qualquer momento.</para>
///
/// <para>Marcado assim, o teste aparece como <b>ignorado</b> numa copia sem
/// corpus - e nao como falha. A distincao importa: falha quer dizer defeito, e
/// nao ha defeito nenhum em nao ter o corpus. A decisao e tomada na
/// descoberta, entao funciona em qualquer executor, sem pacote extra.</para>
/// </summary>
public sealed class FatoComCorpusRealAttribute : FactAttribute
{
    public FatoComCorpusRealAttribute()
    {
        if (!Amostras.TemCorpusReal)
        {
            Skip = Amostras.MotivoCorpusAusente;
        }
    }
}

/// <summary>
/// <c>[Theory]</c> para teste que so faz sentido sobre documento fiscal de
/// verdade. Ver <see cref="FatoComCorpusRealAttribute"/>.
/// </summary>
public sealed class TeoriaComCorpusRealAttribute : TheoryAttribute
{
    public TeoriaComCorpusRealAttribute()
    {
        if (!Amostras.TemCorpusReal)
        {
            Skip = Amostras.MotivoCorpusAusente;
        }
    }
}
