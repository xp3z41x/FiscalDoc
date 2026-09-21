using FiscalDoc.Core.Model.Nfe;
using FiscalDoc.Core.Values;
using FiscalDoc.Layout.Barcode;
using FiscalDoc.Layout.Composition;
using FiscalDoc.Layout.DisplayList;

namespace FiscalDoc.Layout.Nfce;

/// <summary>
/// Tamanhos de fonte do DANFE NFC-e.
///
/// <para>O Manual de Especificacoes Tecnicas do DANFE NFC-e <b>nao</b> fixa
/// fonte nem corpo - diferente do MOC do DANFE, que traz a secao 3.7 com os
/// minimos. O que o manual exige e largura minima de papel (56 mm), margem
/// lateral minima (2 mm) e legibilidade por seis meses. Os valores abaixo sao
/// convencao da casa, escolhidos para caber na bobina de 80 mm; ficam reunidos
/// num registro, e nao espalhados pelo desenho, para que ajustar seja trocar
/// uma constante.</para>
///
/// <para>A familia segue a do DANFE por coerencia dentro do aplicativo: um
/// mesmo usuario imprime os dois, e duas tipografias diferentes so
/// pareceriam descuido.</para>
/// </summary>
public sealed record EstilosNfce
{
    public EstiloTexto EmitenteNome { get; init; } =
        new(EstiloTexto.FamiliaPadrao, 8f, Negrito: true);

    public EstiloTexto EmitenteDados { get; init; } = new(EstiloTexto.FamiliaPadrao, 6f);

    /// <summary>"Documento Auxiliar da Nota Fiscal de Consumidor Eletrônica".</summary>
    public EstiloTexto Identificacao { get; init; } = new(EstiloTexto.FamiliaPadrao, 6f);

    public EstiloTexto ColunaItem { get; init; } =
        new(EstiloTexto.FamiliaPadrao, 5f, Negrito: true);

    public EstiloTexto Item { get; init; } = new(EstiloTexto.FamiliaPadrao, 6f);

    public EstiloTexto ItemAdicional { get; init; } =
        new(EstiloTexto.FamiliaPadrao, 5.5f, Italico: true);

    public EstiloTexto Total { get; init; } = new(EstiloTexto.FamiliaPadrao, 6.5f);

    public EstiloTexto TotalDestacado { get; init; } =
        new(EstiloTexto.FamiliaPadrao, 7.5f, Negrito: true);

    public EstiloTexto Consumidor { get; init; } = new(EstiloTexto.FamiliaPadrao, 6.5f);

    /// <summary>A chave em onze blocos de quatro, que o consumidor digita.</summary>
    public EstiloTexto Chave { get; init; } =
        new(EstiloTexto.FamiliaPadrao, 7f, Negrito: true);

    /// <summary>Contingencia e homologacao, que o manual manda "em destaque".</summary>
    public EstiloTexto Destaque { get; init; } =
        new(EstiloTexto.FamiliaPadrao, 8f, Negrito: true);

    public EstiloTexto Mensagem { get; init; } = new(EstiloTexto.FamiliaPadrao, 5.5f);
}

/// <summary>
/// DANFE NFC-e: a representacao grafica do modelo 65.
///
/// <para><b>O documento e outro, nao um DANFE estreito.</b> O DANFE do modelo
/// 55 tem tabela de coordenadas no Anexo II do MOC e quadros com moldura; o
/// DANFE NFC-e tem um manual proprio - "Manual de Especificacoes Tecnicas do
/// DANFE NFC-e e QR Code", v6.0 de marco/2025 - que organiza o cupom em nove
/// <i>divisoes</i> empilhadas, sem moldura, numa bobina. Sao dois layouts
/// diferentes pela norma, e por isso sao dois arquivos.</para>
///
/// <para><b>O que o manual fixa e o que nao fixa.</b> Fixa a ordem e o
/// conteudo das divisoes, a redacao literal de varios textos (ver
/// <see cref="TextosNfce"/>), a largura minima do papel (56 mm), a margem
/// lateral minima (2 mm) e o tamanho minimo do QR Code (25 x 25 mm). <b>Nao</b>
/// fixa posicao de campo, corpo de fonte nem largura de coluna: "nao sao
/// reguladas as posicoes das informacoes dos detalhes de produtos/servicos e
/// forma de sua impressao, mas sao obrigatorias, no minimo, as seguintes
/// informacoes". O criterio aqui e o mesmo do DACTE e do DAMDFE: todas as
/// informacoes presentes, na ordem do manual, legiveis e sem corte.</para>
///
/// <para><b>Duas escolhas de desenho que o manual permite.</b> Primeira: o
/// QR Code vai <i>centralizado</i> (figura 5) e nao a esquerda das divisoes VI
/// e VII (figura 4) - numa bobina de 80 mm, um QR de 30 mm ao lado deixaria
/// menos de 45 mm para o protocolo de autorizacao. Segunda: os acrescimos e o
/// desconto saem em linhas separadas, cada uma com o valor que esta no
/// arquivo, em vez de um "Acrescimos/Desconto" somado - o MOC 3.1 proibe
/// imprimir o que nao consta do XML, e uma soma nao consta.</para>
///
/// <para><b>Papel.</b> Bobina de <see cref="LarguraBobinaMm"/> com altura
/// igual a do conteudo: um cupom nao tem "folha", tem comprimento. Quando o
/// conteudo passa de <see cref="AlturaMaximaMm"/> - a altura de uma A4 - o
/// documento passa a paginar, porque o manual permite imprimir o DANFE NFC-e
/// em A4 e uma pagina de um metro e meio nao entra em impressora nenhuma de
/// folha solta.</para>
/// </summary>
public static class DanfeNfce
{
    /// <summary>
    /// Largura da bobina. O manual exige no minimo 56 mm; 80 mm e a bobina
    /// mais comum no varejo e o que as impressoras termicas de balcao usam.
    /// </summary>
    public const float LarguraBobinaMm = 80f;

    /// <summary>Manual, item 3.1: "as margens laterais deverao ter, no minimo, 2mm".</summary>
    private const float MargemLateralMm = 3f;

    private const float MargemSuperiorMm = 4f;

    /// <summary>Folga no pe, antes do corte da bobina.</summary>
    private const float MargemInferiorMm = 6f;

    /// <summary>
    /// A partir daqui o cupom pagina, em vez de virar uma folha unica
    /// impossivel de imprimir. E a altura da A4, que o proprio manual admite
    /// como papel do DANFE NFC-e.
    /// </summary>
    private const float AlturaMaximaMm = 297f;

    /// <summary>
    /// Lado do quadrado do QR Code, margem clara inclusa.
    ///
    /// <para>O manual exige no minimo 25 x 25 mm, "sendo 22mm de conteudo para
    /// 3mm de margem segura". A caixa e maior que o minimo por causa da
    /// quantizacao: o renderizador arredonda a largura do modulo <b>para
    /// baixo</b>, em pontos inteiros do dispositivo, para nunca estourar a
    /// caixa - e a 300 dpi isso custa ate um ponto por modulo. Com 34 mm, o
    /// simbolo impresso fica em torno de 25 mm de conteudo mesmo depois da
    /// perda, e continua acima dos 22 mm que o manual pede.</para>
    /// </summary>
    private const float LadoQrMm = 34f;

    /// <summary>Respiro entre uma divisao e a linha divisoria seguinte.</summary>
    private const float RespiroMm = 1.1f;

    /// <summary>
    /// Folga horizontal dentro de cada celula do detalhe. Sem ela o numero do
    /// item, alinhado a direita, encosta no codigo, alinhado a esquerda, e os
    /// dois viram um numero so.
    /// </summary>
    private const float RecuoColunaMm = 0.45f;

    public static ConjuntoPaginas Construir(
        NfeDocumento nfe,
        IMedidorTexto medidor,
        EstilosNfce? estilos = null,
        float larguraPapelMm = LarguraBobinaMm)
    {
        ArgumentNullException.ThrowIfNull(nfe);
        ArgumentNullException.ThrowIfNull(medidor);

        var desenho = new Desenho(nfe, medidor, estilos ?? new EstilosNfce(), larguraPapelMm);
        return desenho.Montar();
    }

    /// <summary>
    /// Uma unidade indivisivel do cupom. Cada uma sabe se desenhar a partir de
    /// um y, e devolve o y seguinte.
    ///
    /// <para>Medir e <b>desenhar num descarte</b>: a altura sai do mesmo
    /// codigo que produz o desenho, entao as duas nao podem divergir. E o
    /// mesmo truque que o <c>MontadorDocumento</c> usa no cabecalho.</para>
    /// </summary>
    private sealed record Unidade(Func<Campo, float, float> Desenhar, bool EhItem = false);

    private sealed class Desenho
    {
        private readonly NfeDocumento _nfe;
        private readonly IMedidorTexto _medidor;
        private readonly EstilosNfce _e;
        private readonly EstilosDanfe _estilosCampo = new();
        private readonly float _largura;
        private readonly float _esquerda;
        private readonly float _direita;

        internal Desenho(
            NfeDocumento nfe, IMedidorTexto medidor, EstilosNfce estilos, float larguraPapelMm)
        {
            _nfe = nfe;
            _medidor = medidor;
            _e = estilos;
            _largura = larguraPapelMm;
            _esquerda = MargemLateralMm;
            _direita = larguraPapelMm - MargemLateralMm;
        }

        private float Util => _direita - _esquerda;

        internal ConjuntoPaginas Montar()
        {
            Unidade cabecalho = new(Cabecalho);
            Unidade cabecalhoTabela = new(CabecalhoTabela);

            List<Unidade> corpo = MontarCorpo(cabecalhoTabela);

            float alturaCabecalho = Medir(cabecalho);
            float alturaCabecalhoTabela = Medir(cabecalhoTabela);
            float limite = AlturaMaximaMm - MargemInferiorMm;

            // Passo 1: distribuir. Nada e desenhado ainda.
            var plano = new List<List<Unidade>>();
            var atual = new List<Unidade>();
            float y = MargemSuperiorMm + alturaCabecalho;

            foreach (Unidade u in corpo)
            {
                float altura = Medir(u);

                if (y + altura > limite && atual.Count > 0)
                {
                    plano.Add(atual);
                    atual = [];
                    y = MargemSuperiorMm + alturaCabecalho;

                    // Tabela que continua na pagina seguinte repete o
                    // cabecalho das colunas - sem ele a continuacao vira uma
                    // lista de numeros sem titulo.
                    if (u.EhItem)
                    {
                        atual.Add(cabecalhoTabela);
                        y += alturaCabecalhoTabela;
                    }
                }

                atual.Add(u);
                y += altura;
            }

            plano.Add(atual);

            // Passo 2: a altura do papel. Uma pagina so significa bobina, e
            // bobina tem o comprimento do conteudo.
            float alturaPapel = plano.Count == 1
                ? y + MargemInferiorMm
                : AlturaMaximaMm;

            var paginas = new List<Pagina>(plano.Count);

            for (int i = 0; i < plano.Count; i++)
            {
                var saida = new List<Primitiva>(256);
                var campo = new Campo(saida, _estilosCampo);

                float cursor = Cabecalho(campo, MargemSuperiorMm);

                foreach (Unidade u in plano[i])
                {
                    cursor = u.Desenhar(campo, cursor);
                }

                if (plano.Count > 1)
                {
                    Folha(campo, alturaPapel, i + 1, plano.Count);
                }

                paginas.Add(new Pagina(i + 1, saida));
            }

            return new ConjuntoPaginas(
                paginas,
                new TamanhoPapel(_largura, alturaPapel),
                RetanguloMm.DeCantos(
                    _esquerda, MargemSuperiorMm, _direita, alturaPapel - MargemInferiorMm));
        }

        /// <summary>Altura de uma unidade, obtida desenhando-a num descarte.</summary>
        private float Medir(Unidade u) => u.Desenhar(new Campo([], _estilosCampo), 0f);

        private List<Unidade> MontarCorpo(Unidade cabecalhoTabela)
        {
            var corpo = new List<Unidade> { cabecalhoTabela };

            // Divisao II - detalhes de produtos/servicos.
            foreach (ItemNfe item in _nfe.Itens)
            {
                ItemNfe capturado = item;
                corpo.Add(new Unidade((c, y) => Item(c, y, capturado), EhItem: true));
            }

            corpo.Add(new Unidade(Divisoria));

            // Divisoes III a IX, na ordem do manual. O QR Code (divisao V) vem
            // depois da VI e da VII porque esta centralizado - o manual pede a
            // identificacao do consumidor "a direita ou antes da divisao V".
            corpo.Add(new Unidade(Totais));
            corpo.Add(new Unidade(Divisoria));
            corpo.Add(new Unidade(ConsultaPorChave));
            corpo.Add(new Unidade(Divisoria));
            corpo.Add(new Unidade(Consumidor));
            corpo.Add(new Unidade(Divisoria));
            corpo.Add(new Unidade(Identificacao));
            corpo.Add(new Unidade(QrCodeDoCupom));
            corpo.Add(new Unidade(MensagemFiscal));
            corpo.Add(new Unidade(MensagemContribuinte));

            return corpo;
        }

        // =================================================== divisao I

        /// <summary>
        /// Divisao I: CNPJ ou CPF, razao social, endereco sem o pais, e o texto
        /// de identificacao do documento. Repete-se em toda pagina, quando o
        /// cupom pagina.
        /// </summary>
        private float Cabecalho(Campo c, float y)
        {
            Emitente emit = _nfe.Emitente;

            y = Paragrafo(c, y, emit.RazaoSocial, _e.EmitenteNome, AlinhamentoH.Centro);

            var identificacao = new List<string>(2);

            if (!string.IsNullOrWhiteSpace(emit.Documento))
            {
                string rotulo = string.IsNullOrWhiteSpace(emit.Cnpj) ? "CPF" : "CNPJ";
                identificacao.Add($"{rotulo}: {Formatos.CnpjOuCpf(emit.Documento)}");
            }

            if (!string.IsNullOrWhiteSpace(emit.InscricaoEstadual))
            {
                identificacao.Add($"IE: {emit.InscricaoEstadual}");
            }

            y = Paragrafo(
                c, y, string.Join("   ", identificacao), _e.EmitenteDados, AlinhamentoH.Centro);

            y = Paragrafo(
                c, y, TextosNfce.LinhaDeEndereco(emit.Endereco),
                _e.EmitenteDados, AlinhamentoH.Centro);

            y += RespiroMm;
            y = Paragrafo(
                c, y, TextosNfce.IdentificacaoDanfe, _e.Identificacao, AlinhamentoH.Centro);

            y = Divisoria(c, y);

            // Manual, divisao VIII: em contingencia o aviso sai em dois
            // lugares, e este e o primeiro - logo abaixo do cabecalho.
            if (_nfe.ContingenciaOffline)
            {
                y = Contingencia(c, y);
                y = Divisoria(c, y);
            }

            return y;
        }

        private float Contingencia(Campo c, float y)
        {
            y += RespiroMm;
            y = Paragrafo(c, y, TextosNfce.ContingenciaLinha1, _e.Destaque, AlinhamentoH.Centro);
            y = Paragrafo(c, y, TextosNfce.ContingenciaLinha2, _e.Destaque, AlinhamentoH.Centro);

            return y + RespiroMm;
        }

        // =================================================== divisao II

        /// <summary>
        /// Larguras das colunas do detalhe, em milimetros, para a util de
        /// 74 mm da bobina de 80. Numa bobina mais estreita ou mais larga as
        /// larguras sao reescaladas proporcionalmente.
        /// </summary>
        private static readonly float[] ColunasBase = [4.0f, 10.0f, 26.0f, 7.0f, 5.0f, 10.5f, 11.5f];

        private static readonly string[] TitulosColunas =
            ["#", "CÓDIGO", "DESCRIÇÃO", "QTDE", "UN", "VL UNIT.", "VL TOTAL"];

        private static readonly AlinhamentoH[] AlinhamentosColunas =
        [
            AlinhamentoH.Direita, AlinhamentoH.Esquerda, AlinhamentoH.Esquerda,
            AlinhamentoH.Direita, AlinhamentoH.Centro, AlinhamentoH.Direita,
            AlinhamentoH.Direita,
        ];

        private float[] Colunas()
        {
            float soma = 0f;
            foreach (float l in ColunasBase)
            {
                soma += l;
            }

            float fator = Util / soma;
            var larguras = new float[ColunasBase.Length];

            for (int i = 0; i < ColunasBase.Length; i++)
            {
                larguras[i] = ColunasBase[i] * fator;
            }

            return larguras;
        }

        private float CabecalhoTabela(Campo c, float y)
        {
            float[] larguras = Colunas();
            float altura = _medidor.AlturaLinhaMm(_e.ColunaItem) + 0.6f;
            float x = _esquerda;

            for (int i = 0; i < larguras.Length; i++)
            {
                c.Texto(
                    Celula(x, y, larguras[i], altura),
                    TitulosColunas[i],
                    _e.ColunaItem,
                    AlinhamentosColunas[i],
                    AlinhamentoV.Base);

                x += larguras[i];
            }

            y += altura;
            c.Linha(_esquerda, y, _direita, y, 0.1f);

            return y + 0.5f;
        }

        private float Item(Campo c, float y, ItemNfe item)
        {
            float[] larguras = Colunas();
            float alturaLinha = _medidor.AlturaLinhaMm(_e.Item);

            // A descricao e a unica coluna que quebra; a altura da linha e a
            // dela. O resto do item alinha pelo topo.
            IReadOnlyList<string> descricao = _medidor.Quebrar(
                item.Descricao ?? string.Empty, _e.Item, larguras[2] - (2 * RecuoColunaMm));

            int linhas = Math.Max(1, descricao.Count);

            string[] valores =
            [
                item.Numero.ToString(System.Globalization.CultureInfo.InvariantCulture),
                item.Codigo ?? string.Empty,
                string.Empty,
                Formatos.Quantidade(item.Quantidade),
                item.Unidade ?? string.Empty,
                Formatos.ValorUnitario(item.ValorUnitario),
                Formatos.Moeda(item.ValorTotal),
            ];

            float x = _esquerda;

            for (int i = 0; i < larguras.Length; i++)
            {
                if (i == 2)
                {
                    float yd = y;

                    foreach (string linha in descricao)
                    {
                        c.Texto(
                            Celula(x, yd, larguras[i], alturaLinha),
                            linha,
                            _e.Item,
                            AlinhamentoH.Esquerda);

                        yd += alturaLinha;
                    }
                }
                else
                {
                    c.Texto(
                        Celula(x, y, larguras[i], alturaLinha),
                        valores[i],
                        _e.Item,
                        AlinhamentosColunas[i]);
                }

                x += larguras[i];
            }

            y += linhas * alturaLinha;

            // infAdProd do item, quando existe, sai logo abaixo dele - mesma
            // regra do DANFE (MOC 3.1.7), e aqui ocupa a largura toda.
            if (item.TemInformacaoAdicional)
            {
                y = Paragrafo(
                    c, y, item.InformacaoAdicional, _e.ItemAdicional, AlinhamentoH.Esquerda);
            }

            return y + 0.3f;
        }

        // =================================================== divisao III

        private float Totais(Campo c, float y)
        {
            TotaisIcms t = _nfe.Totais;

            y += RespiroMm;

            y = Linha(
                c, y, "Qtde. total de itens",
                _nfe.Itens.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
                _e.Total);

            y = Linha(c, y, "Valor total R$", Formatos.Moeda(t.ValorTotalProdutos), _e.Total);

            // Acrescimos e desconto, um por linha e cada um com o valor que
            // esta no arquivo. O manual descreve uma linha unica somada; somar
            // seria imprimir um numero que o XML nao tem.
            bool houveAjuste = false;

            houveAjuste |= Ajuste(c, ref y, "Frete R$", t.ValorFrete);
            houveAjuste |= Ajuste(c, ref y, "Seguro R$", t.ValorSeguro);
            houveAjuste |= Ajuste(c, ref y, "Outras despesas R$", t.OutrasDespesas);
            houveAjuste |= Ajuste(c, ref y, "Desconto R$", t.ValorDesconto);

            // "Valor a Pagar R$ ... deve ser impresso apenas se existir
            // acrescimo ou desconto" - sem eles seria repetir o valor total.
            if (houveAjuste)
            {
                y = Linha(
                    c, y, "Valor a pagar R$", Formatos.Moeda(t.ValorTotalNota), _e.TotalDestacado);
            }

            Pagamentos pag = _nfe.Pagamentos;

            if (pag.Formas.Count > 0)
            {
                y += RespiroMm;
                y = Linha(c, y, "FORMA DE PAGAMENTO", "VALOR PAGO R$", _e.Total);

                foreach (Pagamento p in pag.Formas)
                {
                    y = Linha(c, y, p.Rotulo, Formatos.Moeda(p.Valor), _e.Total);
                }
            }

            if (pag.Troco is { } troco)
            {
                y = Linha(c, y, "Troco R$", Formatos.Moeda(troco), _e.Total);
            }

            return y + RespiroMm;
        }

        /// <summary>
        /// Linha de acrescimo ou desconto. So aparece quando o valor existe e
        /// nao e zero - o manual manda imprimir "apenas se existir acrescimo
        /// ou desconto".
        /// </summary>
        private bool Ajuste(Campo c, ref float y, string rotulo, decimal? valor)
        {
            if (valor is not { } v || v == 0m)
            {
                return false;
            }

            y = Linha(c, y, rotulo, Formatos.Moeda(v), _e.Total);
            return true;
        }

        // =================================================== divisao IV

        private float ConsultaPorChave(Campo c, float y)
        {
            InformacoesSuplementares? supl = _nfe.Suplementares;

            y += RespiroMm;

            if (!string.IsNullOrWhiteSpace(supl?.UrlConsultaChave))
            {
                y = Paragrafo(
                    c, y, TextosNfce.ConsultaPorChave, _e.Consumidor, AlinhamentoH.Centro);

                y = Paragrafo(
                    c, y, supl!.UrlConsultaChave, _e.Consumidor, AlinhamentoH.Centro);
            }
            else
            {
                // Arquivo sem urlChave e irregular no modelo 65 - o campo e
                // obrigatorio. Em vez de inventar o endereco da Sefaz, rotula-se
                // a chave e segue-se em frente.
                y = Paragrafo(c, y, "CHAVE DE ACESSO", _e.Consumidor, AlinhamentoH.Centro);
            }

            y += 0.4f;
            y = Paragrafo(c, y, _nfe.Chave?.Formatada, _e.Chave, AlinhamentoH.Centro);

            return y + RespiroMm;
        }

        // =================================================== divisao V

        private float QrCodeDoCupom(Campo c, float y)
        {
            string? conteudo = _nfe.Suplementares?.QrCode;

            if (string.IsNullOrWhiteSpace(conteudo))
            {
                return y;
            }

            MatrizQr matriz = QrCode.Codificar(conteudo);

            float lado = Math.Min(LadoQrMm, Util);
            var caixa = new RetanguloMm(_esquerda + ((Util - lado) / 2f), y + RespiroMm, lado, lado);

            c.Qr(caixa, matriz);

            return caixa.Base + RespiroMm;
        }

        // =================================================== divisao VI

        private float Consumidor(Campo c, float y)
        {
            Destinatario dest = _nfe.Destinatario;

            y += RespiroMm;
            y = Paragrafo(
                c, y, TextosNfce.RotuloConsumidor(dest), _e.Consumidor, AlinhamentoH.Centro);

            if (!string.IsNullOrWhiteSpace(dest.RazaoSocial))
            {
                y = Paragrafo(c, y, dest.RazaoSocial, _e.Consumidor, AlinhamentoH.Centro);
            }

            // Endereco do consumidor, obrigatorio em entrega em domicilio.
            string endereco = TextosNfce.LinhaDeEndereco(dest.Endereco);

            if (endereco.Length > 0)
            {
                y = Paragrafo(c, y, endereco, _e.Mensagem, AlinhamentoH.Centro);
            }

            return y + RespiroMm;
        }

        // =================================================== divisao VII

        private float Identificacao(Campo c, float y)
        {
            y += RespiroMm;

            string numero = Formatos.NumeroNota(_nfe.Ide.Numero);
            string serie = Formatos.Serie(_nfe.Ide.Serie);
            string emissao = Formatos.DataHora(_nfe.Ide.DataHoraEmissao);

            var linha = $"NFC-e nº {numero}   Série {serie}   {emissao}";

            // O manual so cria a distincao entre vias na emissao em
            // contingencia, onde a 2a via fica com o estabelecimento.
            if (_nfe.ContingenciaOffline)
            {
                linha += $"   {TextosNfce.ViaConsumidor}";
            }

            y = Paragrafo(c, y, linha, _e.Consumidor, AlinhamentoH.Centro);

            // "No caso de emissao em contingencia a informacao sobre o
            // protocolo de autorizacao sera suprimida."
            if (!_nfe.ContingenciaOffline && _nfe.Protocolo?.Numero is { } protocolo)
            {
                string recebimento = Formatos.DataHora(_nfe.Protocolo.DataHoraRecebimento);

                y = Paragrafo(
                    c, y,
                    $"Protocolo de autorização: {protocolo}   {recebimento}".TrimEnd(),
                    _e.Consumidor,
                    AlinhamentoH.Centro);
            }

            // Segundo lugar exigido pelo manual para o aviso de contingencia.
            if (_nfe.ContingenciaOffline)
            {
                y = Contingencia(c, y);
            }

            return y + RespiroMm;
        }

        // =================================================== divisoes VIII e IX

        /// <summary>
        /// Divisao VIII: mensagens de interesse do Fisco (infAdFisco) e, em
        /// homologacao, o aviso que o manual manda imprimir "nesta area, de
        /// forma centralizada e em caixa alta".
        /// </summary>
        private float MensagemFiscal(Campo c, float y)
        {
            string? fisco = _nfe.InfoAdicionais?.FiscoInteresse;
            bool temHomologacao = _nfe.ExigeSemValorFiscal;

            if (string.IsNullOrWhiteSpace(fisco) && !temHomologacao)
            {
                return y;
            }

            y = Divisoria(c, y);
            y += RespiroMm;

            if (temHomologacao)
            {
                y = Paragrafo(c, y, TextosNfce.Homologacao, _e.Destaque, AlinhamentoH.Centro);
                y += RespiroMm;
            }

            if (!string.IsNullOrWhiteSpace(fisco))
            {
                y = Paragrafo(c, y, fisco, _e.Mensagem, AlinhamentoH.Esquerda);
            }

            return y + RespiroMm;
        }

        /// <summary>
        /// Divisao IX: mensagem de interesse do contribuinte (infCpl) e, por
        /// faculdade que o manual concede a esta divisao, a carga tributaria
        /// da Lei 12.741/2012 - que sai do campo vTotTrib, nao de conta feita
        /// aqui.
        /// </summary>
        private float MensagemContribuinte(Campo c, float y)
        {
            string? contribuinte = _nfe.InfoAdicionais?.Complementares;
            decimal? tributos = _nfe.Totais.ValorTotalTributos;

            if (string.IsNullOrWhiteSpace(contribuinte) && tributos is null)
            {
                return y;
            }

            y = Divisoria(c, y);
            y += RespiroMm;

            if (tributos is { } v)
            {
                y = Paragrafo(
                    c, y, TextosNfce.TributosTotais(v), _e.Mensagem, AlinhamentoH.Centro);
            }

            if (!string.IsNullOrWhiteSpace(contribuinte))
            {
                y += 0.4f;
                y = Paragrafo(c, y, contribuinte, _e.Mensagem, AlinhamentoH.Esquerda);
            }

            return y + RespiroMm;
        }

        // =================================================== utilitarios

        /// <summary>Celula do detalhe, ja com a folga horizontal.</summary>
        private static RetanguloMm Celula(float x, float y, float largura, float altura) =>
            new(x + RecuoColunaMm, y, largura - (2 * RecuoColunaMm), altura);

        private float Divisoria(Campo c, float y)
        {
            c.Linha(_esquerda, y, _direita, y, 0.1f);
            return y + 0.1f;
        }

        /// <summary>Rotulo a esquerda, valor a direita, na mesma linha.</summary>
        private float Linha(Campo c, float y, string rotulo, string valor, EstiloTexto estilo)
        {
            float altura = _medidor.AlturaLinhaMm(estilo);

            c.Texto(new RetanguloMm(_esquerda, y, Util * 0.62f, altura), rotulo, estilo);

            c.Texto(
                new RetanguloMm(_esquerda + (Util * 0.62f), y, Util * 0.38f, altura),
                valor,
                estilo,
                AlinhamentoH.Direita);

            return y + altura;
        }

        /// <summary>
        /// Texto que ocupa a largura util, quebrado linha a linha pelo medidor.
        /// A quebra e decidida aqui, e nao pelo GDI, porque a altura resultante
        /// e o que pagina o cupom.
        /// </summary>
        private float Paragrafo(
            Campo c, float y, string? texto, EstiloTexto estilo, AlinhamentoH alinhamento)
        {
            if (string.IsNullOrWhiteSpace(texto))
            {
                return y;
            }

            float altura = _medidor.AlturaLinhaMm(estilo);

            foreach (string linha in _medidor.Quebrar(texto, estilo, Util))
            {
                c.Texto(new RetanguloMm(_esquerda, y, Util, altura), linha, estilo, alinhamento);
                y += altura;
            }

            return y;
        }

        /// <summary>
        /// "FOLHA n/m" no pe, e so quando o cupom paginou. Fica fora do fluxo
        /// de proposito: colocar no cabecalho exigiria conhecer o total antes
        /// de paginar, e o total so existe depois.
        /// </summary>
        private void Folha(Campo c, float alturaPapel, int pagina, int total)
        {
            float altura = _medidor.AlturaLinhaMm(_e.Mensagem);

            c.Texto(
                new RetanguloMm(_esquerda, alturaPapel - MargemInferiorMm, Util, altura),
                $"FOLHA {pagina:00}/{total:00}",
                _e.Mensagem,
                AlinhamentoH.Direita);
        }
    }
}
