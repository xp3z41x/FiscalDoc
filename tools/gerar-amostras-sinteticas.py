"""
Gera amostras sinteticas a partir das NF-e e NFC-e reais de "Exemplos XML".

As 10 NF-e reais cobrem retrato, paisagem, paginacao, infAdProd e descricoes
longas, mas nao cobrem: contingencia, homologacao, documento sem protocolo,
ISSQN, destinatario pessoa fisica, encoding ISO-8859-1 nem documento sem
envelope. As 12 NFC-e reais sao todas normais, autorizadas, em producao, com
uma forma de pagamento so e sem desconto - faltam contingencia offline,
homologacao, varias formas de pagamento, desconto e acrescimo, consumidor
estrangeiro, cupom longo o bastante para paginar e arquivo sem infNFeSupl.

Estes casos existem no mundo real e o layout precisa deles, entao sao
derivados aqui por transformacao das reais - assim os valores continuam
realistas e so a dimensao sob teste muda.

Tambem gera os arquivos que devem ser RECUSADOS, para provar que a recusa e
limpa (plano 2.9).

Uso:  python tools/gerar-amostras-sinteticas.py
"""

import decimal
import pathlib
import re
import sys

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))

from anonimizar import anonimizar  # noqa: E402

RAIZ = pathlib.Path(__file__).resolve().parent.parent
ORIGEM = RAIZ / "Exemplos XML"
DESTINO = RAIZ / "tests" / "Amostras"

# As bases sao escolhidas POR CARACTERISTICA, e nao por nome de arquivo.
#
# O nome do arquivo de uma NF-e e a chave de acesso, que e credencial de
# consulta no portal da SEFAZ - e este script vai para um repositorio publico.
# Escolher por caracteristica tambem faz o script funcionar sobre qualquer
# corpus, e nao so sobre o desta maquina.


def _modelo(xml: str) -> str:
    m = re.search(r"<mod>(\d+)</mod>", xml)
    return m.group(1) if m else ""


def _corpus() -> list[tuple[pathlib.Path, str]]:
    saida = []
    for caminho in sorted(ORIGEM.glob("*.xml")):
        try:
            saida.append((caminho, caminho.read_text(encoding="utf-8")))
        except UnicodeDecodeError:
            saida.append((caminho, caminho.read_text(encoding="iso-8859-1")))
    return saida


def _escolher(criterio, ordem, descricao: str) -> str:
    candidatos = [(c, x) for c, x in _corpus() if criterio(x)]
    if not candidatos:
        raise SystemExit(f"nenhuma amostra real serve como {descricao}")
    return max(candidatos, key=lambda par: ordem(par[0], par[1]))[1]


def base_pequena() -> str:
    """A menor NF-e do corpus: o conteudo nao importa, so a estrutura."""
    return _escolher(
        lambda x: _modelo(x) == "55",
        lambda c, x: -c.stat().st_size,
        "NF-e menor",
    )


def base_acentos() -> str:
    """A NF-e com mais caracteres acentuados - a que expoe erro de encoding."""
    return _escolher(
        lambda x: _modelo(x) == "55",
        lambda c, x: (sum(1 for ch in x if ord(ch) > 127), c.stat().st_size),
        "NF-e com acentos",
    )


def base_reforma() -> str:
    """
    A menor NF-e que ja traz o grupo IBS/CBS - e sobre ela que se acrescenta o
    Imposto Seletivo e o vNFTot divergente, que nenhuma real tem.
    """
    return _escolher(
        lambda x: _modelo(x) == "55" and "<IBSCBSTot>" in x,
        lambda c, x: -c.stat().st_size,
        "NF-e com IBS/CBS",
    )


def base_nfce() -> str:
    """
    NFC-e de consumidor identificado por CPF, pagamento em dinheiro e com o
    grupo suplementar (QR Code). Entre as que servem, a menor: e a base de
    todas as variacoes, e quanto menor menos ruido nos arquivos gerados.
    """
    return _escolher(
        lambda x: (
            _modelo(x) == "65"
            and "<CPF>" in x
            and "<infNFeSupl>" in x
            and "<tPag>01</tPag>" in x
        ),
        lambda c, x: -c.stat().st_size,
        "NFC-e com CPF, dinheiro e QR Code",
    )


def trocar_tag(xml: str, tag: str, valor: str) -> str:
    """Substitui o conteudo da primeira ocorrencia de <tag>...</tag>."""
    novo, n = re.subn(rf"<{tag}>[^<]*</{tag}>", f"<{tag}>{valor}</{tag}>", xml, count=1)
    if n != 1:
        raise SystemExit(f"tag <{tag}> nao encontrada")
    return novo


def inserir_depois(xml: str, tag: str, trecho: str) -> str:
    """Insere trecho logo apos o fechamento da primeira ocorrencia de tag."""
    alvo = f"</{tag}>"
    i = xml.index(alvo) + len(alvo)
    return xml[:i] + trecho + xml[i:]


def escrever(nome: str, conteudo: str, encoding: str = "utf-8") -> None:
    caminho = DESTINO / nome

    # Ninguem escreve sem anonimizar: a chamada esta AQUI, e nao em cada
    # gerador, para que uma amostra nova nao possa esquecer o passo.  As bases
    # sao documentos reais; o que sai desta pasta vai para o repositorio.
    caminho.write_text(anonimizar(conteudo), encoding=encoding)
    print(f"  {nome}  ({caminho.stat().st_size:,} bytes, {encoding})")


def repetir_itens(xml: str, total: int) -> str:
    """Repete o primeiro <det> ate o documento ter `total` itens, renumerando."""
    m = re.search(r"<det nItem=\"1\">.*?</det>", xml, flags=re.DOTALL)
    if not m:
        raise SystemExit("<det> nao encontrado")

    modelo = m.group(0)
    existentes = re.findall(r"<det nItem=", xml)

    extras = "".join(
        re.sub(r'<det nItem="1">', f'<det nItem="{n}">', modelo, count=1)
        for n in range(len(existentes) + 1, total + 1)
    )

    # Entra logo depois do ultimo <det>, que e onde <total> comeca.
    return xml.replace("<total>", extras + "<total>", 1)


def gerar_nfce() -> None:
    """
    Variacoes de NFC-e. As 12 reais sao todas iguais na dimensao que o DANFE
    NFC-e trata de forma diferente - todas normais, autorizadas, em producao e
    com uma forma de pagamento so.
    """
    base = base_nfce()
    print("\nNFC-e:")

    # --- Contingencia offline (tpEmis 9) -------------------------------------
    # O manual manda imprimir "EMITIDA EM CONTINGENCIA / Pendente de
    # autorizacao" em dois lugares e SUPRIMIR o protocolo - que de fato ainda
    # nao existe, por isso o protNFe sai fora.
    cont = trocar_tag(base, "tpEmis", "9")
    cont = re.sub(r"<protNFe.*?</protNFe>", "", cont, flags=re.DOTALL)
    escrever("nfce-contingencia-offline.xml", cont)

    # --- Homologacao ---------------------------------------------------------
    # Exige "EMITIDA EM AMBIENTE DE HOMOLOGACAO - SEM VALOR FISCAL" na area de
    # mensagem fiscal (divisao VIII).
    escrever("nfce-homologacao.xml", trocar_tag(base, "tpAmb", "2"))

    # --- Desconto e acrescimo ------------------------------------------------
    # Nenhuma real tem. Sem desconto nem acrescimo o manual manda NAO imprimir
    # a linha "Valor a Pagar"; com eles, a linha aparece.
    ajustes = trocar_tag(base, "vDesc", "5.00")
    ajustes = trocar_tag(ajustes, "vFrete", "7.50")
    ajustes = trocar_tag(ajustes, "vOutro", "1.25")
    ajustes = trocar_tag(ajustes, "vNF", "43.75")
    ajustes = trocar_tag(ajustes, "vPag", "43.75")
    escrever("nfce-desconto-e-acrescimo.xml", ajustes)

    # --- Varias formas de pagamento e troco ----------------------------------
    # O manual: "podem ocorrer mais de uma forma de pagamento, devendo nesse
    # caso ser indicado o montante parcial do pagamento para a respectiva
    # forma".
    pagamentos = re.sub(
        r"<pag>.*?</pag>",
        "<pag>"
        "<detPag><indPag>0</indPag><tPag>01</tPag><vPag>30.00</vPag></detPag>"
        "<detPag><indPag>0</indPag><tPag>03</tPag><vPag>15.00</vPag>"
        "<card><tpIntegra>1</tpIntegra><tBand>02</tBand></card></detPag>"
        "<detPag><indPag>0</indPag><tPag>99</tPag><xPag>Vale-troca da loja</xPag>"
        "<vPag>5.00</vPag></detPag>"
        "<vTroco>10.00</vTroco>"
        "</pag>",
        base,
        flags=re.DOTALL,
    )
    escrever("nfce-varios-pagamentos.xml", pagamentos)

    # --- Consumidor estrangeiro ----------------------------------------------
    # Terceiro rotulo da divisao VI: "CONSUMIDOR Id. Estrangeiro:".
    estrangeiro = re.sub(
        r"<dest>.*?</dest>",
        "<dest><idEstrangeiro>PA-AB123456</idEstrangeiro>"
        "<xNome>MARIA FERNANDA COSTA</xNome><indIEDest>9</indIEDest></dest>",
        base,
        flags=re.DOTALL,
    )
    escrever("nfce-consumidor-estrangeiro.xml", estrangeiro)

    # --- Cupom longo ---------------------------------------------------------
    # Bobina com mais conteudo do que cabe numa A4: o cupom passa a paginar e o
    # cabecalho das colunas tem de se repetir.
    escrever("nfce-muitos-itens.xml", repetir_itens(base, 120))

    # --- Sem infNFeSupl ------------------------------------------------------
    # Arquivo irregular: no modelo 65 o grupo e obrigatorio. Sem ele nao ha
    # QR Code nem endereco de consulta, e o cupom nao pode inventar nenhum dos
    # dois nem quebrar.
    sem_supl = re.sub(r"<infNFeSupl>.*?</infNFeSupl>", "", base, flags=re.DOTALL)
    escrever("nfce-sem-suplementares.xml", sem_supl)


def main() -> None:
    if not ORIGEM.is_dir():
        raise SystemExit(f"pasta nao encontrada: {ORIGEM}")

    # NAO apagar a pasta inteira: ela e compartilhada com
    # gerar-amostras-transporte.py, e um rmtree aqui destruiria as amostras de
    # CT-e, MDF-e e evento. Cada arquivo e sobrescrito pelo proprio nome.
    DESTINO.mkdir(parents=True, exist_ok=True)

    base = base_pequena()
    print("Aceitaveis:")

    # --- Encoding ISO-8859-1 -------------------------------------------------
    # Nenhuma amostra real usa Latin-1, mas o requisito exige suporte. Converte
    # uma nota com acentos de verdade, trocando tambem a declaracao.
    acentuada = base_acentos()
    latin1 = acentuada.replace('encoding="UTF-8"', 'encoding="ISO-8859-1"', 1)
    if 'encoding="ISO-8859-1"' not in latin1:
        latin1 = latin1.replace("<?xml version=\"1.0\"?>",
                                "<?xml version=\"1.0\" encoding=\"ISO-8859-1\"?>", 1)
    escrever("encoding-iso88591.xml", latin1, encoding="iso-8859-1")

    # --- Latin-1 mal declarado ----------------------------------------------
    # Declara UTF-8 mas grava bytes Latin-1: emissor desleixado, caso real.
    # Exercita o fallback do XmlSource.
    escrever("encoding-latin1-declarado-utf8.xml", acentuada, encoding="iso-8859-1")

    # --- Sem protocolo de autorizacao ---------------------------------------
    # Em contingencia FS/FS-DA o DANFE e impresso antes da autorizacao.
    sem_prot = re.sub(r"<protNFe.*?</protNFe>", "", base, flags=re.DOTALL)
    escrever("sem-protocolo.xml", sem_prot)

    # --- Sem envelope nfeProc ------------------------------------------------
    # <NFe> avulso, sem <nfeProc>. Comum em arquivo pre-autorizacao.
    m = re.search(r"(<NFe\b.*?</NFe>)", base, flags=re.DOTALL)
    if not m:
        raise SystemExit("<NFe> nao encontrado")
    avulso = '<?xml version="1.0" encoding="UTF-8"?>\n' + m.group(1)
    escrever("sem-envelope.xml", avulso)

    # --- Homologacao ---------------------------------------------------------
    # Exige a frase SEM VALOR FISCAL (MOC Anexo II secao 3).
    escrever("homologacao.xml", trocar_tag(base, "tpAmb", "2"))

    # --- Contingencias -------------------------------------------------------
    just = ("<dhCont>2026-03-12T08:15:00-03:00</dhCont>"
            "<xJust>Indisponibilidade do servico de autorizacao da SEFAZ origem</xJust>")

    # FS-DA (5): "DANFE em Contingencia - impresso em decorrencia de problemas
    # tecnicos", 2 vias, e xJust/dhCont impressos.
    fsda = inserir_depois(trocar_tag(base, "tpEmis", "5"), "verProc", just)
    escrever("contingencia-fsda.xml", fsda)

    # EPEC (4): legenda propria e protocolo do EPEC no lugar do de autorizacao.
    escrever("contingencia-epec.xml", trocar_tag(base, "tpEmis", "4"))

    # SVC-AN (6): sem legenda propria, mas xJust e dhCont sao obrigatorios.
    svc = inserir_depois(trocar_tag(base, "tpEmis", "6"), "verProc", just)
    escrever("contingencia-svcan.xml", svc)

    # --- ISSQN ---------------------------------------------------------------
    # Nenhuma amostra real tem; o quadro precisa aparecer quando existe e
    # sumir quando nao existe.
    issqn = inserir_depois(
        base, "ICMSTot",
        "<ISSQNtot><vServ>1500.00</vServ><vBC>1500.00</vBC><vISS>75.00</vISS>"
        "<dCompet>2026-03-12</dCompet></ISSQNtot>")
    escrever("com-issqn.xml", issqn)

    # --- Destinatario pessoa fisica -----------------------------------------
    # Todas as reais sao CNPJ. CPF muda a formatacao do documento no DANFE.
    cpf = re.sub(r"<dest>\s*<CNPJ>\d+</CNPJ>",
                 "<dest><CPF>12345678909</CPF>", base, count=1)
    cpf = cpf.replace("<indIEDest>1</indIEDest>", "<indIEDest>9</indIEDest>", 1)
    escrever("destinatario-cpf.xml", cpf)

    # --- Reforma tributária completa ----------------------------------------
    # Nenhuma nota real do corpus traz Imposto Seletivo, e em todas o vNFTot
    # vem igual ao vNF (o art. 348 da LC 214/2025 dispensa o recolhimento no
    # ano de teste). Os dois campos só aparecem no DANFE quando existem de
    # fato, então precisam de uma amostra que os tenha.
    base_rt = base_reforma()

    vis = decimal.Decimal("235.14")

    # ISTot é irmão de ICMSTot dentro de <total>, não filho de IBSCBSTot.
    rt = inserir_depois(base_rt, "IBSCBSTot", f"<ISTot><vIS>{vis}</vIS></ISTot>")

    # vNFTot = vNF + IBS + CBS + IS, porque os três são cobrados "por fora".
    # Os valores saem do próprio arquivo em vez de virem escritos aqui: a base
    # é escolhida por característica, então o total tem de acompanhar.
    def valor(tag: str) -> decimal.Decimal:
        m = re.search(rf"<{tag}>([\d.]+)</{tag}>", rt)
        return decimal.Decimal(m.group(1)) if m else decimal.Decimal(0)

    total_rt = valor("vNF") + valor("vIBS") + valor("vCBS") + vis

    if "<vNFTot>" in rt:
        rt = trocar_tag(rt, "vNFTot", str(total_rt))
    else:
        rt = inserir_depois(rt, "ISTot", f"<vNFTot>{total_rt}</vNFTot>")

    escrever("reforma-com-is-e-total.xml", rt)

    gerar_nfce()

    print("\nDevem ser recusados:")

    # --- XML valido, nao fiscal ---------------------------------------------
    escrever("recusar-nao-fiscal.xml",
             '<?xml version="1.0" encoding="UTF-8"?>\n'
             "<Project Sdk=\"Microsoft.NET.Sdk\">\n"
             "  <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>\n"
             "</Project>\n")

    # --- XML malformado ------------------------------------------------------
    escrever("recusar-xml-truncado.xml", base[: len(base) // 2])

    # --- Arquivo vazio -------------------------------------------------------
    escrever("recusar-vazio.xml", "")

    print(f"\nTotal: {len(list(DESTINO.glob('*.xml')))} arquivos em {DESTINO}")


if __name__ == "__main__":
    main()
