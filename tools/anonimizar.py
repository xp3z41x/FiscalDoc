"""
Anonimizacao das amostras de teste.

As amostras de `tests/Amostras` VAO PARA O REPOSITORIO PUBLICO, e as de NF-e e
NFC-e sao derivadas de documentos reais - notas emitidas de verdade, com CPF e
nome de pessoas fisicas e CNPJ, endereco e telefone de empresas. Publicar esse
dado seria expor informacao de terceiro que nao e nossa para publicar.

Por isso nada sai dos geradores sem passar por `anonimizar`, que e chamada
dentro do `escrever` de cada script - e nao em cada gerador individualmente,
justamente para que ninguem consiga acrescentar uma amostra nova e esquecer o
passo.

Duas propriedades importam mais do que a aparencia do resultado:

1. **O comprimento de cada campo de texto e aproximado.** Nao e estetica: boa
   parte da suite mede quebra de linha, largura de coluna e paginacao, e um
   nome com metade dos caracteres deixaria de exercitar a quebra que a amostra
   existe para exercitar. Por isso os pools trazem nomes de varios
   comprimentos e a escolha prefere o mais proximo do original - em vez de
   completar ate o tamanho exato, que produzia nome cortado no meio de uma
   palavra bem no meio das PNGs de conferencia visual.

2. **Os digitos verificadores sao recalculados** - de CNPJ, de CPF e da chave
   de acesso. Trocar o CNPJ sem refazer o DV da chave produziria um arquivo
   que os proprios testes recusam, com razao.
"""

import re

# ---------------------------------------------------------------------
#  Digitos verificadores
# ---------------------------------------------------------------------


def _dv_modulo11(digitos: str, pesos: list[int]) -> str:
    soma = sum(int(d) * p for d, p in zip(digitos, pesos))
    resto = soma % 11
    return "0" if resto < 2 else str(11 - resto)


def cnpj_ficticio(indice: int) -> str:
    """CNPJ ficticio com DV valido. Raiz 11222333, 12222333, ..."""
    base = f"{11 + indice:02d}" + "222333" + "0001"
    d1 = _dv_modulo11(base, [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2])
    d2 = _dv_modulo11(base + d1, [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2])
    return base + d1 + d2


def cpf_ficticio(indice: int) -> str:
    """CPF ficticio com DV valido. Raiz 111444777, 112444777, ..."""
    base = f"{111 + indice:03d}" + "444777"
    s1 = sum(int(base[i]) * (10 - i) for i in range(9))
    r1 = (s1 * 10) % 11
    d1 = "0" if r1 == 10 else str(r1)
    s2 = sum(int((base + d1)[i]) * (11 - i) for i in range(10))
    r2 = (s2 * 10) % 11
    d2 = "0" if r2 == 10 else str(r2)
    return base + d1 + d2


def dv_chave(chave43: str) -> str:
    """Modulo 11 com pesos 2..9 ciclicos a partir da direita (MOC Anexo III)."""
    pesos = [2, 3, 4, 5, 6, 7, 8, 9]
    soma = sum(int(c) * pesos[i % 8] for i, c in enumerate(reversed(chave43)))
    resto = soma % 11
    return "0" if resto in (0, 1) else str(11 - resto)


# ---------------------------------------------------------------------
#  Texto
# ---------------------------------------------------------------------

# Nomes claramente ficticios. O ponto nao e parecer real: e ser legivel e nao
# pertencer a ninguem.
_EMPRESAS = [
    "LOJA ARCO LTDA",
    "COMERCIAL AURORA LTDA",
    "INDUSTRIA MERIDIANO SA",
    "ATACADO BOREAL LTDA ME",
    "MERCADO PENINSULA LTDA",
    "DISTRIBUIDORA VERTICE LTDA",
    "TRANSPORTES QUADRANTE LTDA",
    "INDUSTRIA E COMERCIO ZENITE SA",
    "ATACADISTA PONTO CARDEAL LTDA ME",
    "COMERCIO DE PECAS HORIZONTE LTDA EPP",
    "DISTRIBUIDORA NACIONAL PARALELO LTDA EPP",
]
_PESSOAS = [
    "ANA LIMA",
    "JOAO BATISTA REIS",
    "ANA PAULA MARTINS",
    "MARCOS ANTONIO DIAS",
    "CARLOS EDUARDO RAMOS",
    "FERNANDA SOUZA ROCHA",
    "JULIANA FERREIRA LIMA",
    "RICARDO ALVES PEREIRA",
    "PATRICIA GONCALVES MONTEIRO",
    "EDUARDO HENRIQUE NOGUEIRA CAMPOS",
]
_LOGRADOUROS = [
    "RUA DAS ACACIAS",
    "AVENIDA DOS IPES",
    "TRAVESSA DO MIRANTE",
    "RUA PROJETADA UM",
    "ALAMEDA DAS PALMEIRAS",
]
_BAIRROS = ["CENTRO", "JARDIM NOVO", "VILA UNIAO", "DISTRITO INDUSTRIAL"]
_PRODUTOS = [
    "ARTIGO DE AMOSTRA",
    "PRODUTO GENERICO",
    "ITEM DE TESTE",
    "MERCADORIA MODELO",
    "COMPONENTE PADRAO",
]

def _estavel(texto: str, pool: list[str]) -> str:
    """
    Escolha deterministica - `hash` do Python varia entre execucoes, e amostra
    que muda a cada geracao produz diff sem motivo.

    <para>Entre os candidatos, prefere o de comprimento mais proximo do
    original: o resultado e cortado ou completado ate o tamanho exato, e quanto
    menor o ajuste menos o nome sai truncado no meio de uma palavra.</para>
    """
    soma = sum(ord(c) for c in texto)
    alvo = len(texto)
    ordenado = sorted(pool, key=lambda c: (abs(len(c) - alvo), c))
    perto = [c for c in ordenado if abs(len(c) - alvo) <= 4] or ordenado[:2]
    return perto[soma % len(perto)]


def _trocar_texto(xml: str, tags: list[str], escolher) -> str:
    """Troca o conteudo de todas as ocorrencias das tags, preservando tamanho."""
    for tag in tags:

        def troca(m, tag=tag):
            original = m.group(1)
            if not original.strip():
                return m.group(0)
            return f"<{tag}>{escolher(original)}</{tag}>"

        xml = re.sub(rf"<{tag}>([^<]*)</{tag}>", troca, xml)

    return xml


def _trocar_digitos(xml: str, tags: list[str]) -> str:
    """Zera identificadores numericos mantendo a quantidade de digitos."""
    for tag in tags:

        def troca(m, tag=tag):
            n = len(m.group(1))
            return f"<{tag}>" + ("9" * (n - 1) + "0") + f"</{tag}>"

        xml = re.sub(rf"<{tag}>(\d+)</{tag}>", troca, xml)

    return xml


def _nome(original: str) -> str:
    """
    Pessoa fisica e juridica saem do mesmo par de tags <xNome>, entao a escolha
    olha o proprio conteudo: designacao societaria indica empresa.
    """
    alto = original.upper()
    eh_empresa = any(
        s in alto for s in (" LTDA", "S/A", " S.A", " SA", " ME", " EPP", " CIA", "MEI")
    )
    return _estavel(original, _EMPRESAS if eh_empresa else _PESSOAS)


def anonimizar(xml: str) -> str:
    """Troca toda identificacao de pessoa e empresa por dado ficticio."""
    # --- CNPJ e CPF: mapa estavel, para o mesmo numero virar sempre o mesmo.
    # O nome da tag varia conforme o grupo: CNPJ, CNPJDest, CNPJEmit, CNPJCPF,
    # CNPJForn... Procurar so por <CNPJ> deixava passar as outras - foi assim
    # que o <CNPJDest> de um evento sobreviveu a primeira versao disto.
    mapa_cnpj: dict[str, str] = {}
    for antigo in dict.fromkeys(
        m.group(2) for m in re.finditer(r"<(\w*CNPJ\w*)>(\d{14})</\1>", xml)
    ):
        mapa_cnpj[antigo] = cnpj_ficticio(len(mapa_cnpj))

    # Um CNPJ pode aparecer SO dentro de uma chave de acesso e em nenhuma tag
    # <CNPJ> - e o caso do MDF-e, que referencia as NF-e de outros emitentes
    # por <chNFe> e mais nada. Sem colher daqui, esses ficavam intactos.
    for chave in dict.fromkeys(re.findall(r"\d{44}", xml)):
        embutido = chave[6:20]
        if embutido not in mapa_cnpj:
            mapa_cnpj[embutido] = cnpj_ficticio(len(mapa_cnpj))

    mapa_cpf: dict[str, str] = {}
    for antigo in dict.fromkeys(
        m.group(2) for m in re.finditer(r"<(\w*CPF\w*)>(\d{11})</\1>", xml)
    ):
        mapa_cpf[antigo] = cpf_ficticio(len(mapa_cpf))

    # --- Chave de acesso: o CNPJ ocupa as posicoes 6..20, e o DV e refeito.
    #     A chave aparece no Id do envelope, em <chNFe> e dentro do <qrCode>.
    def nova_chave(chave: str) -> str:
        cnpj = chave[6:20]
        corpo = chave[:6] + mapa_cnpj.get(cnpj, cnpj) + chave[20:43]
        return corpo + dv_chave(corpo)

    mapa_chave = {c: nova_chave(c) for c in dict.fromkeys(re.findall(r"\d{44}", xml))}

    # Ordem importa: a chave CONTEM o CNPJ, entao a chave e trocada primeiro.
    for antiga, nova in mapa_chave.items():
        xml = xml.replace(antiga, nova)

    for antigo, novo in mapa_cnpj.items():
        xml = xml.replace(antigo, novo)

    for antigo, novo in mapa_cpf.items():
        xml = xml.replace(antigo, novo)

    # cDV e o mesmo digito do fim da chave; tem de acompanhar.
    if mapa_chave:
        primeira = next(iter(mapa_chave.values()))
        xml = re.sub(r"<cDV>\d{1,2}</cDV>", f"<cDV>{primeira[43]}</cDV>", xml, count=1)

    # --- Textos.
    #
    # <xNome> nao e so nome de participante: no CT-e o mesmo nome de tag
    # identifica cada COMPONENTE do valor da prestacao ("FRETE PESO", "PEDAGIO",
    # "GRIS"), que e vocabulario do documento e nao identificacao de ninguem.
    # Trocar aquilo destruia conteudo util - e um teste, com razao, reclamou.
    # Os blocos <Comp> saem de cena durante a troca e voltam intactos.
    comps: list[str] = []

    def _guardar(m: re.Match[str]) -> str:
        comps.append(m.group(0))
        return f"@@COMP{len(comps) - 1}@@"

    xml = re.sub(r"<Comp>.*?</Comp>", _guardar, xml, flags=re.DOTALL)

    xml = _trocar_texto(xml, ["xNome", "xFant"], _nome)
    xml = _trocar_texto(xml, ["xLgr"], lambda o: _estavel(o, _LOGRADOUROS))
    xml = _trocar_texto(xml, ["xBairro"], lambda o: _estavel(o, _BAIRROS))
    xml = _trocar_texto(xml, ["xProd"], lambda o: _estavel(o, _PRODUTOS))
    xml = _trocar_texto(xml, ["xCpl"], lambda o: "COMPLEMENTO")
    xml = _trocar_texto(xml, ["email"], lambda o: "contato@exemplo.invalid")
    xml = _trocar_texto(
        xml,
        ["infCpl", "infAdProd", "xObs", "xJust", "xCorrecao", "xPed", "xMotivo"],
        lambda o: "TEXTO DE AMOSTRA",
    )

    # --- Identificadores numericos que nao tem DV a preservar.
    xml = _trocar_digitos(xml, ["IE", "IEST", "IM", "fone", "CEP", "nProt"])

    for i, bloco in enumerate(comps):
        xml = xml.replace(f"@@COMP{i}@@", bloco)

    # O numero do protocolo tambem mora no atributo Id do <infProt>, e atributo
    # nao passa por _trocar_digitos. Deixa-lo para tras produziria um arquivo
    # incoerente - Id de um protocolo, <nProt> de outro - alem de continuar
    # apontando para uma autorizacao real da SEFAZ.
    def _id_protocolo(m: re.Match[str]) -> str:
        return f'Id="Id{"9" * (len(m.group(1)) - 1)}0"'

    xml = re.sub(r'Id="Id(\d+)"', _id_protocolo, xml)

    return xml
