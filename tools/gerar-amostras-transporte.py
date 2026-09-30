"""
Gera amostras sinteticas de CT-e, MDF-e e eventos em tests/Amostras.

O corpus fornecido tem apenas NF-e, e nao foi possivel obter exemplares
publicos de CT-e/MDF-e/evento (o unico encontrado esta atras de HTTP 403).
Estes arquivos sao montados a partir da estrutura dos schemas oficiais
(PL_CTe_400, PL_MDFe_300b, procEventoNFe_v1.00), com dados ficticios mas
estruturalmente fieis: mesmos elementos, mesma ordem, mesmos namespaces.

Servem para exercitar parser e layout. NAO substituem conferencia contra
documentos reais - quando houver CT-e e MDF-e de verdade, vale repassar.

Uso:  python tools/gerar-amostras-transporte.py
"""

import pathlib
import sys

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))

from anonimizar import anonimizar, cnpj_alfanumerico_ficticio  # noqa: E402

RAIZ = pathlib.Path(__file__).resolve().parent.parent
DESTINO = RAIZ / "tests" / "Amostras"

NS_CTE = "http://www.portalfiscal.inf.br/cte"
NS_MDFE = "http://www.portalfiscal.inf.br/mdfe"
NS_NFE = "http://www.portalfiscal.inf.br/nfe"


def dv(chave43: str) -> int:
    """
    Modulo 11 do MOC: pesos 2..9 ciclicos, da direita para a esquerda, sobre o
    valor ASCII - 48 de cada caractere - que e o proprio digito, e de 17 a 42
    para as letras do CNPJ alfanumerico (NT Conjunta 2025.001, item 5).
    """
    soma, peso = 0, 2
    for c in reversed(chave43):
        soma += (ord(c) - 48) * peso
        peso = 2 if peso == 9 else peso + 1
    resto = soma % 11
    return 0 if resto in (0, 1) else 11 - resto


def chave(cuf, aamm, cnpj, mod, serie, numero, tpemis, cnf) -> str:
    base = f"{cuf}{aamm}{cnpj}{mod}{serie:0>3}{numero:0>9}{tpemis}{cnf:0>8}"
    assert len(base) == 43, len(base)
    return base + str(dv(base))


def alfanumerico(xml: str, cnpjs: tuple[str, ...]) -> str:
    """
    Troca cada CNPJ numerico dado por um alfanumerico em todo o documento - na
    tag e dentro de toda chave de acesso que o carrega. O DV dessas chaves fica
    para a anonimizacao, que refaz o de toda chave que encontra.
    """
    for i, cnpj in enumerate(cnpjs):
        xml = xml.replace(cnpj, cnpj_alfanumerico_ficticio(i))
    return xml


def escrever(nome: str, conteudo: str) -> None:
    p = DESTINO / nome

    # Estas amostras sao montadas do zero, e ainda assim passam pela
    # anonimizacao: a montagem herdou CNPJ e razao social de empresas reais do
    # corpus, e um CT-e inventado nao deve dizer que fulano transportou nada.
    p.write_text(anonimizar(conteudo), encoding="utf-8")
    print(f"  {nome}  ({p.stat().st_size:,} bytes)")


# ---------------------------------------------------------------- CT-e ----

def cte(versao: str, nome: str, *, componentes: int, documentos: int,
        tpemis: str = "1", tpamb: str = "1") -> None:
    ch = chave("41", "2609", "11222333000181", "57", 1, 91723, tpemis, 74113920)

    comps = "".join(
        f"<Comp><xNome>{n}</xNome><vComp>{v}</vComp></Comp>"
        for n, v in [("FRETE PESO", "1850.00"), ("PEDAGIO", "132.40"),
                     ("GRIS", "96.20"), ("ADEME", "45.00"),
                     ("TAXA DE COLETA", "78.50")][:componentes])

    docs = "".join(
        f"<infNFe><chave>{chave('41', '2609', '12222333000148', '55', 1, 294000 + i, '1', 10000000 + i)}</chave></infNFe>"
        for i in range(documentos))

    # dhCont e xJust vem depois de toma3/toma4 - e nao ao lado de tpEmis,
    # onde o schema os recusa.
    cont = ""
    if tpemis in ("5", "7"):
        cont = ("<dhCont>2026-09-14T07:22:00-03:00</dhCont>"
                "<xJust>Indisponibilidade do ambiente autorizador da SEFAZ de origem</xJust>")

    # CRT e obrigatorio no emit do CT-e 4.00. No 3.00 so apareceu com o pacote
    # 3.00a (NT 2022.001), e opcional: a amostra 3.00 fica sem ele.
    crt = "<CRT>3</CRT>" if versao == "4.00" else ""

    escrever(nome, f"""<?xml version="1.0" encoding="UTF-8"?>
<cteProc versao="{versao}" xmlns="{NS_CTE}">
<CTe xmlns="{NS_CTE}"><infCte versao="{versao}" Id="CTe{ch}">
<ide><cUF>41</cUF><cCT>74113920</cCT><CFOP>6352</CFOP>
<natOp>PRESTACAO DE SERVICO DE TRANSPORTE</natOp>
<mod>57</mod><serie>1</serie><nCT>91723</nCT>
<dhEmi>2026-09-14T09:18:37-03:00</dhEmi><tpImp>2</tpImp><tpEmis>{tpemis}</tpEmis>
<cDV>{ch[-1]}</cDV><tpAmb>{tpamb}</tpAmb><tpCTe>0</tpCTe><procEmi>0</procEmi>
<verProc>4.0.7</verProc>
<cMunEnv>4106902</cMunEnv><xMunEnv>CURITIBA</xMunEnv><UFEnv>PR</UFEnv>
<modal>01</modal><tpServ>0</tpServ>
<cMunIni>4106902</cMunIni><xMunIni>CURITIBA</xMunIni><UFIni>PR</UFIni>
<cMunFim>3550308</cMunFim><xMunFim>SAO PAULO</xMunFim><UFFim>SP</UFFim>
<retira>1</retira><indIEToma>1</indIEToma>
<toma3><toma>3</toma></toma3>{cont}</ide>
<compl><xObs>Mercadoria acondicionada em paletes. Entrega somente em horario comercial.</xObs></compl>
<emit><CNPJ>11222333000181</CNPJ><IE>9058250705</IE>
<xNome>TRANSPORTADORA CAMINHO CERTO LTDA</xNome><xFant>CAMINHO CERTO</xFant>
<enderEmit><xLgr>RUA FRANCISCO DEROSSO</xLgr><nro>2986</nro><xBairro>XAXIM</xBairro>
<cMun>4106902</cMun><xMun>CURITIBA</xMun><CEP>81720000</CEP><UF>PR</UF>
<fone>4130916500</fone></enderEmit>{crt}</emit>
<rem><CNPJ>12222333000148</CNPJ><IE>0100007007</IE>
<xNome>RINALDI S/A IND. PNEUMATICOS</xNome><fone>5434557500</fone>
<enderReme><xLgr>RUA LUIZ ALEGRETTI</xLgr><nro>193</nro><xBairro>LICORSUL</xBairro>
<cMun>4302105</cMun><xMun>BENTO GONCALVES</xMun><CEP>95705860</CEP><UF>RS</UF>
<cPais>1058</cPais><xPais>BRASIL</xPais></enderReme></rem>
<dest><CNPJ>11222333000181</CNPJ><IE>999999990</IE>
<xNome>BMOTOS DISTRIBUIDORA LTDA</xNome><fone>2737264253</fone>
<enderDest><xLgr>RODOVIA RODOLFO HAESE</xLgr><nro>SN</nro><xCpl>GALPAO B</xCpl>
<xBairro>LAGINHA</xBairro><cMun>3550308</cMun><xMun>SAO PAULO</xMun>
<CEP>29755000</CEP><UF>SP</UF><cPais>1058</cPais><xPais>BRASIL</xPais></enderDest></dest>
<vPrest><vTPrest>2202.10</vTPrest><vRec>2202.10</vRec>{comps}</vPrest>
<imp><ICMS><ICMS00><CST>00</CST><vBC>2202.10</vBC><pICMS>12.00</pICMS>
<vICMS>264.25</vICMS></ICMS00></ICMS><vTotTrib>418.40</vTotTrib></imp>
<infCTeNorm>
<infCarga><vCarga>148320.55</vCarga><proPred>PNEUMATICOS</proPred>
<infQ><cUnid>01</cUnid><tpMed>PESO BRUTO</tpMed><qCarga>8420.0000</qCarga></infQ>
<infQ><cUnid>03</cUnid><tpMed>VOLUMES</tpMed><qCarga>184.0000</qCarga></infQ></infCarga>
<infDoc>{docs}</infDoc>
<infModal versaoModal="{versao}"><rodo><RNTRC>12345678</RNTRC></rodo></infModal>
</infCTeNorm>
</infCte>
<infCTeSupl><qrCodCTe>https://dfe-portal.svrs.rs.gov.br/cte/qrCode?chCTe={ch}&amp;tpAmb={tpamb}</qrCodCTe></infCTeSupl>
</CTe>
<protCTe versao="{versao}"><infProt><tpAmb>{tpamb}</tpAmb><verAplic>PR-v4_0_7</verAplic>
<chCTe>{ch}</chCTe><dhRecbto>2026-09-14T09:19:02-03:00</dhRecbto>
<nProt>141260378123456</nProt><digVal>abc123</digVal><cStat>100</cStat>
<xMotivo>Autorizado o uso do CT-e</xMotivo></infProt></protCTe>
</cteProc>
""")


# ------------------------------------------------------- CT-e: variantes ----
#
# O cte() acima cobre o caso comum - rodoviario, NF-e, tomador destinatario.
# As variantes abaixo cobrem o resto do que o DACTE imprime: cada tipo de CT-e
# (complemento, substituto), cada modal, cada forma de documento originario e
# os grupos operacionais (cobranca, previsao de entrega, ordens de coleta,
# documentos anteriores). Uma amostra por assunto, para que a PNG de
# conferencia mostre um quadro de cada vez.
#
# Ordem dos elementos: a do schema PL_CTe_400, grupo a grupo. infCTeSupl e
# irmao de infCte, dentro de CTe - e nao filho dele.

CNPJ_EMIT = "11222333000181"
CNPJ_REM = "12222333000148"
CNPJ_DEST = "13222333000102"
CNPJ_EXPED = "14222333000159"
CNPJ_RECEB = "15222333000103"
CNPJ_TOMA = "16222333000160"
CNPJ_ANTERIOR = "17222333000114"
CNPJ_OUTRA_NF = "18222333000170"

EMIT = """<emit><CNPJ>11222333000181</CNPJ><IE>9058250705</IE>
<xNome>TRANSPORTADORA CAMINHO CERTO LTDA</xNome><xFant>CAMINHO CERTO</xFant>
<enderEmit><xLgr>RUA FRANCISCO DEROSSO</xLgr><nro>2986</nro><xBairro>XAXIM</xBairro>
<cMun>4106902</cMun><xMun>CURITIBA</xMun><CEP>81720000</CEP><UF>PR</UF>
<fone>4130916500</fone></enderEmit><CRT>3</CRT></emit>"""

REM = f"""<rem><CNPJ>{CNPJ_REM}</CNPJ><IE>0100007007</IE>
<xNome>RINALDI S/A IND. PNEUMATICOS</xNome><fone>5434557500</fone>
<enderReme><xLgr>RUA LUIZ ALEGRETTI</xLgr><nro>193</nro><xBairro>LICORSUL</xBairro>
<cMun>4302105</cMun><xMun>BENTO GONCALVES</xMun><CEP>95705860</CEP><UF>RS</UF>
<cPais>1058</cPais><xPais>BRASIL</xPais></enderReme></rem>"""

EXPED = f"""<exped><CNPJ>{CNPJ_EXPED}</CNPJ><IE>2530011122</IE>
<xNome>ARMAZENS GERAIS DO SUL LTDA</xNome><fone>4733341200</fone>
<enderExped><xLgr>RODOVIA BR 101</xLgr><nro>KM 12</nro><xBairro>DISTRITO INDUSTRIAL</xBairro>
<cMun>4209102</cMun><xMun>JOINVILLE</xMun><CEP>89219500</CEP><UF>SC</UF></enderExped></exped>"""

RECEB = f"""<receb><CNPJ>{CNPJ_RECEB}</CNPJ><IE>116335512110</IE>
<xNome>CENTRO DE DISTRIBUICAO PAULISTA LTDA</xNome><fone>1135520900</fone>
<enderReceb><xLgr>AVENIDA DAS NACOES</xLgr><nro>4400</nro><xBairro>JARDIM NOVO</xBairro>
<cMun>3509502</cMun><xMun>CAMPINAS</xMun><CEP>13050000</CEP><UF>SP</UF></enderReceb></receb>"""


def dest(isuf: str = "") -> str:
    suframa = f"<ISUF>{isuf}</ISUF>" if isuf else ""
    return f"""<dest><CNPJ>{CNPJ_DEST}</CNPJ><IE>999999990</IE>
<xNome>BMOTOS DISTRIBUIDORA LTDA</xNome><fone>2737264253</fone>{suframa}
<enderDest><xLgr>RODOVIA RODOLFO HAESE</xLgr><nro>SN</nro><xCpl>GALPAO B</xCpl>
<xBairro>LAGINHA</xBairro><cMun>3550308</cMun><xMun>SAO PAULO</xMun>
<CEP>29755000</CEP><UF>SP</UF><cPais>1058</cPais><xPais>BRASIL</xPais></enderDest></dest>"""


TOMA4 = f"""<toma4><toma>4</toma><CNPJ>{CNPJ_TOMA}</CNPJ><IE>9876543210</IE>
<xNome>LOGISTICA INTEGRADA PARANA LTDA</xNome><fone>4133330000</fone>
<enderToma><xLgr>RUA DAS ACACIAS</xLgr><nro>100</nro><xBairro>CENTRO</xBairro>
<cMun>4106902</cMun><xMun>CURITIBA</xMun><CEP>80010000</CEP><UF>PR</UF>
<cPais>1058</cPais><xPais>BRASIL</xPais></enderToma></toma4>"""

ICMS00 = """<ICMS00><CST>00</CST><vBC>2202.10</vBC><pICMS>12.00</pICMS>
<vICMS>264.25</vICMS></ICMS00>"""

# ICMS cobrado por substituicao: base, aliquota e valor com nome proprio.
ICMS60 = """<ICMS60><CST>60</CST><vBCSTRet>2202.10</vBCSTRet><vICMSSTRet>264.25</vICMSSTRet>
<pICMSSTRet>12.00</pICMSSTRet></ICMS60>"""

# Reforma tributaria: vTotDFe = vTPrest + vIBS + vCBS. Diverge de vTPrest de
# proposito, para exercitar o campo de total que so aparece quando diverge.
IBSCBS = """<IBSCBS><CST>000</CST><cClassTrib>000001</cClassTrib><gIBSCBS><vBC>2202.10</vBC>
<gIBSUF><pIBSUF>0.1000</pIBSUF><vIBSUF>2.20</vIBSUF></gIBSUF>
<gIBSMun><pIBSMun>0.0000</pIBSMun><vIBSMun>0.00</vIBSMun></gIBSMun><vIBS>2.20</vIBS>
<gCBS><pCBS>0.9000</pCBS><vCBS>19.82</vCBS></gCBS></gIBSCBS></IBSCBS><vTotDFe>2224.12</vTotDFe>"""

CARGA = """<infCarga><vCarga>148320.55</vCarga><proPred>PNEUMATICOS</proPred>{xoutcat}
<infQ><cUnid>01</cUnid><tpMed>PESO BRUTO</tpMed><qCarga>8420.0000</qCarga></infQ>
<infQ><cUnid>03</cUnid><tpMed>VOLUMES</tpMed><qCarga>184.0000</qCarga></infQ></infCarga>"""


def nfe(numero: int, cnpj: str = CNPJ_REM) -> str:
    return chave("41", "2609", cnpj, "55", 1, numero, "1", 10000000 + numero % 1000)


def ct(numero: int, cnpj: str = CNPJ_EMIT) -> str:
    return chave("41", "2609", cnpj, "57", 1, numero, "1", 74113920 + numero % 1000)


def docs_nfe(*numeros: int) -> str:
    return "<infDoc>" + "".join(
        f"<infNFe><chave>{nfe(n)}</chave></infNFe>" for n in numeros) + "</infDoc>"


def rodo(occ: str = "") -> str:
    return f"""<infModal versaoModal="4.00"><rodo><RNTRC>12345678</RNTRC>{occ}</rodo></infModal>"""


def cte_montar(nome: str, *, numero: int, tpimp: str = "2", tpcte: str = "0",
               tpserv: str = "0", modal: str = "01", retira: str = "1",
               xdetretira: str = "", globalizado: bool = False,
               toma: str = "<toma3><toma>3</toma></toma3>", compl: str = "",
               participantes: str = "", vprest: str = "", imp: str = "",
               imp_extra: str = "", vtottrib: str = "418.40", corpo: str = "",
               cnpj_alfanumerico: tuple[str, ...] = ()) -> None:
    ch = ct(numero)
    det_retira = f"<xDetRetira>{xdetretira}</xDetRetira>" if xdetretira else ""
    glob = "<indGlobalizado>1</indGlobalizado>" if globalizado else ""

    escrever(nome, alfanumerico(f"""<?xml version="1.0" encoding="UTF-8"?>
<cteProc versao="4.00" xmlns="{NS_CTE}">
<CTe xmlns="{NS_CTE}"><infCte versao="4.00" Id="CTe{ch}">
<ide><cUF>41</cUF><cCT>{ch[35:43]}</cCT><CFOP>6352</CFOP>
<natOp>PRESTACAO DE SERVICO DE TRANSPORTE</natOp>
<mod>57</mod><serie>1</serie><nCT>{numero}</nCT>
<dhEmi>2026-09-14T09:18:37-03:00</dhEmi><tpImp>{tpimp}</tpImp><tpEmis>1</tpEmis>
<cDV>{ch[-1]}</cDV><tpAmb>1</tpAmb><tpCTe>{tpcte}</tpCTe><procEmi>0</procEmi>
<verProc>4.0.7</verProc>{glob}
<cMunEnv>4106902</cMunEnv><xMunEnv>CURITIBA</xMunEnv><UFEnv>PR</UFEnv>
<modal>{modal}</modal><tpServ>{tpserv}</tpServ>
<cMunIni>4106902</cMunIni><xMunIni>CURITIBA</xMunIni><UFIni>PR</UFIni>
<cMunFim>3550308</cMunFim><xMunFim>SAO PAULO</xMunFim><UFFim>SP</UFFim>
<retira>{retira}</retira>{det_retira}<indIEToma>1</indIEToma>
{toma}</ide>
{compl}
{EMIT}
{participantes or REM + dest()}
{vprest or '<vPrest><vTPrest>2202.10</vTPrest><vRec>2202.10</vRec><Comp><xNome>FRETE PESO</xNome><vComp>2202.10</vComp></Comp></vPrest>'}
<imp><ICMS>{imp or ICMS00}</ICMS><vTotTrib>{vtottrib}</vTotTrib>{imp_extra}</imp>
{corpo}
</infCte>
<infCTeSupl><qrCodCTe>https://dfe-portal.svrs.rs.gov.br/cte/qrCode?chCTe={ch}&amp;tpAmb=1</qrCodCTe></infCTeSupl>
</CTe>
<protCTe versao="4.00"><infProt><tpAmb>1</tpAmb><verAplic>PR-v4_0_7</verAplic>
<chCTe>{ch}</chCTe><dhRecbto>2026-09-14T09:19:02-03:00</dhRecbto>
<nProt>141260378123456</nProt><digVal>abc123</digVal><cStat>100</cStat>
<xMotivo>Autorizado o uso do CT-e</xMotivo></infProt></protCTe>
</cteProc>
""", cnpj_alfanumerico))


def cte_subcontratacao(nome: str) -> None:
    """
    Subcontratacao em retrato - o formato dos desenhos do manual. Junta o que
    um CT-e rodoviario pode trazer alem do basico: ICMS por substituicao,
    tomador "outros" (toma4), expedidor e recebedor, SUFRAMA, notas em papel,
    documento de transporte anterior, cobranca, ordens de coleta, previsao de
    entrega e de fluxo, caracteristicas adicionais, campos livres e IBS/CBS.
    """
    # xCaracAd vai ate 15 caracteres; xCaracSer, ate 30.
    compl = """<compl><xCaracAd>PALETIZADA</xCaracAd><xCaracSer>ENTREGA AGENDADA</xCaracSer>
<fluxo><xOrig>CWB</xOrig><pass><xPass>JVE</xPass></pass><pass><xPass>REG</xPass></pass><xDest>CPQ</xDest><xRota>R12</xRota></fluxo>
<Entrega><comData><tpPer>2</tpPer><dProg>2026-09-19</dProg></comData><noInter><tpHor>4</tpHor><hIni>08:00:00</hIni><hFim>12:00:00</hFim></noInter></Entrega>
<xObs>Mercadoria acondicionada em paletes.</xObs>
<ObsCont xCampo="PEDIDO"><xTexto>PC-4471</xTexto></ObsCont>
<ObsCont xCampo="COLETA"><xTexto>JANELA 07H-09H</xTexto></ObsCont>
<ObsFisco xCampo="ICMS"><xTexto>ICMS RETIDO POR SUBSTITUICAO TRIBUTARIA</xTexto></ObsFisco></compl>"""

    docs = """<infDoc><infNF><mod>04</mod><serie>1</serie><nDoc>812</nDoc><dEmi>2026-09-10</dEmi>
<vBC>0.00</vBC><vICMS>0.00</vICMS><vBCST>0.00</vBCST><vST>0.00</vST><vProd>18450.00</vProd>
<vNF>18450.00</vNF><nCFOP>6101</nCFOP><nPeso>2100.000</nPeso></infNF>
<infNF><mod>01</mod><serie>2</serie><nDoc>4471</nDoc><dEmi>2026-09-11</dEmi>
<vBC>9800.00</vBC><vICMS>1176.00</vICMS><vBCST>0.00</vBCST><vST>0.00</vST><vProd>9800.00</vProd>
<vNF>9800.00</vNF><nCFOP>6102</nCFOP></infNF></infDoc>"""

    doc_ant = f"""<docAnt><emiDocAnt><CNPJ>{CNPJ_ANTERIOR}</CNPJ><IE>2530099887</IE><UF>SC</UF>
<xNome>TRANSPORTES QUADRANTE LTDA</xNome>
<idDocAnt><idDocAntPap><tpDoc>11</tpDoc><serie>1</serie><nDoc>15520</nDoc><dEmi>2026-09-12</dEmi></idDocAntPap></idDocAnt>
<idDocAnt><idDocAntEle><chCTe>{ct(40221, CNPJ_ANTERIOR)}</chCTe></idDocAntEle></idDocAnt>
</emiDocAnt></docAnt>"""

    occ = f"""<occ><serie>1</serie><nOcc>55821</nOcc><dEmi>2026-09-12</dEmi><emiOcc><CNPJ>{CNPJ_REM}</CNPJ>
<cInt>COL-7</cInt><IE>0100007007</IE><UF>RS</UF><fone>5434557500</fone></emiOcc></occ>"""

    # Sem vDesc: os valores do fat sao TDec_1302Opc, que nao admite zero -
    # desconto que nao houve nao se informa.
    cobr = """<cobr><fat><nFat>91724</nFat><vOrig>2202.10</vOrig><vLiq>2202.10</vLiq></fat>
<dup><nDup>001</nDup><dVenc>2026-10-14</dVenc><vDup>1101.05</vDup></dup>
<dup><nDup>002</nDup><dVenc>2026-11-13</dVenc><vDup>1101.05</vDup></dup></cobr>"""

    cte_montar(
        nome, numero=91724, tpimp="1", tpserv="1", toma=TOMA4, compl=compl,
        participantes=REM + EXPED + RECEB + dest(isuf="210987654"),
        vprest="""<vPrest><vTPrest>2202.10</vTPrest><vRec>2202.10</vRec>
<Comp><xNome>FRETE PESO</xNome><vComp>1850.00</vComp></Comp>
<Comp><xNome>PEDAGIO</xNome><vComp>132.40</vComp></Comp>
<Comp><xNome>GRIS</xNome><vComp>219.70</vComp></Comp></vPrest>""",
        imp=ICMS60,
        imp_extra=IBSCBS,
        corpo=f"""<infCTeNorm>{CARGA.format(xoutcat='<xOutCat>FRAGIL - NAO EMPILHAR</xOutCat>')}
{docs}{doc_ant}{rodo(occ)}{cobr}</infCTeNorm>""")


def cte_complemento(nome: str) -> None:
    """tpCTe 1: nao ha infCTeNorm - so a lista dos CT-e complementados."""
    # Comp/xNome vai ate 15 caracteres - este usa todos.
    cte_montar(
        nome, numero=91801, tpcte="1",
        vprest="""<vPrest><vTPrest>180.00</vTPrest><vRec>180.00</vRec>
<Comp><xNome>DIFERENCA FRETE</xNome><vComp>180.00</vComp></Comp></vPrest>""",
        imp="""<ICMS00><CST>00</CST><vBC>180.00</vBC><pICMS>12.00</pICMS><vICMS>21.60</vICMS></ICMS00>""",
        vtottrib="34.20",
        corpo=f"""<infCteComp><chCTe>{ct(91723)}</chCTe></infCteComp>
<infCteComp><chCTe>{ct(91724)}</chCTe></infCteComp>""")


def cte_substituto(nome: str) -> None:
    """tpCTe 3, com alteracao de tomador."""
    cte_montar(
        nome, numero=91802, tpcte="3",
        corpo=f"""<infCTeNorm>{CARGA.format(xoutcat='')}{docs_nfe(294010, 294011)}{rodo()}
<infCteSub><chCte>{ct(91723)}</chCte><indAlteraToma>1</indAlteraToma></infCteSub></infCTeNorm>""")


def cte_aereo(nome: str) -> None:
    """Modal aereo, com artigo perigoso, manuseio e retirada no aeroporto."""
    docs = """<infDoc><infOutros><tpDoc>99</tpDoc><descOutros>ROMANEIO DE CARGA</descOutros>
<nDoc>R-2201</nDoc><dEmi>2026-09-13</dEmi><vDocFisc>5400.00</vDocFisc></infOutros>
<infOutros><tpDoc>00</tpDoc><nDoc>D-18</nDoc><dEmi>2026-09-13</dEmi></infOutros></infDoc>"""

    aereo = """<infModal versaoModal="4.00"><aereo><nMinu>000123456</nMinu><nOCA>12345678901</nOCA>
<dPrevAereo>2026-09-16</dPrevAereo><natCarga><xDime>120X080X060</xDime>
<cInfManu>02</cInfManu><cInfManu>10</cInfManu></natCarga>
<tarifa><CL>G</CL><cTar>0001</cTar><vTar>1850.00</vTar></tarifa>
<peri><nONU>3480</nONU><qTotEmb>4 CAIXAS</qTotEmb><infTotAP><qTotProd>12.5000</qTotProd><uniAP>1</uniAP></infTotAP></peri>
</aereo></infModal>"""

    cte_montar(
        nome, numero=91803, modal="02", retira="0",
        xdetretira="RETIRADA NO TERMINAL DE CARGAS DO AEROPORTO",
        compl="<compl><xCaracSer>ENTREGA URGENTE</xCaracSer></compl>",
        corpo=f"""<infCTeNorm>{CARGA.format(xoutcat='')}{docs}{aereo}</infCTeNorm>""")


def cte_aquaviario(nome: str) -> None:
    """Aquaviario, servico vinculado a multimodal, com conteineres e balsas."""
    aquav = f"""<infModal versaoModal="4.00"><aquav><vPrest>2202.10</vPrest><vAFRMM>176.17</vAFRMM>
<xNavio>MV ATLANTICO SUL</xNavio><balsa><xBalsa>BALSA 07</xBalsa></balsa><balsa><xBalsa>BALSA 11</xBalsa></balsa>
<nViag>2026091</nViag><direc>N</direc><irin>PP1234</irin>
<detCont><nCont>MSCU1234565</nCont><lacre><nLacre>LAC7781</nLacre></lacre><lacre><nLacre>LAC7782</nLacre></lacre>
<infDoc><infNFe><chave>{nfe(294020)}</chave></infNFe></infDoc></detCont>
<tpNav>1</tpNav></aquav></infModal>"""

    cte_montar(
        nome, numero=91804, modal="03", tpserv="4",
        corpo=f"""<infCTeNorm>{CARGA.format(xoutcat='')}{docs_nfe(294020, 294021, 294022)}{aquav}
<infServVinc><infCTeMultimodal><chCTeMultimodal>{ct(70001)}</chCTeMultimodal></infCTeMultimodal></infServVinc></infCTeNorm>""")


def cte_ferroviario(nome: str) -> None:
    """Ferroviario em trafego mutuo, levando veiculos novos."""
    ferrov = f"""<infModal versaoModal="4.00"><ferrov><tpTraf>1</tpTraf>
<trafMut><respFat>1</respFat><ferrEmi>1</ferrEmi><vFrete>2202.10</vFrete>
<chCTeFerroOrigem>{ct(60001)}</chCTeFerroOrigem>
<ferroEnv><CNPJ>{CNPJ_OUTRA_NF}</CNPJ><cInt>FER1</cInt><IE>116000111222</IE><xNome>FERROVIA DO ATLANTICO SA</xNome>
<enderFerro><xLgr>RUA DA ESTACAO</xLgr><nro>1</nro><xBairro>CENTRO</xBairro><cMun>3550308</cMun>
<xMun>SAO PAULO</xMun><CEP>01000000</CEP><UF>SP</UF></enderFerro></ferroEnv></trafMut>
<fluxo>FLX202644</fluxo></ferrov></infModal>"""

    veiculos = """<veicNovos><chassi>9BWZZZ377VT004251</chassi><cCor>01</cCor><xCor>BRANCO</xCor>
<cMod>001234</cMod><vUnit>98500.00</vUnit><vFrete>820.00</vFrete></veicNovos>
<veicNovos><chassi>9BWZZZ377VT004252</chassi><cCor>05</cCor><xCor>PRATA</xCor>
<cMod>001234</cMod><vUnit>98500.00</vUnit><vFrete>820.00</vFrete></veicNovos>"""

    cte_montar(
        nome, numero=91805, modal="04",
        corpo=f"""<infCTeNorm>{CARGA.format(xoutcat='')}{docs_nfe(294030, 294031)}{ferrov}{veiculos}</infCTeNorm>""")


def cte_dutoviario(nome: str) -> None:
    """Dutoviario globalizado, com documento "dutoviario" em infOutros."""
    duto = """<infModal versaoModal="4.00"><duto><vTar>0.012345</vTar><dIni>2026-09-01</dIni>
<dFim>2026-09-30</dFim><classDuto>1</classDuto><tpContratacao>0</tpContratacao>
<codPontoEntrada>PE01</codPontoEntrada><codPontoSaida>PS09</codPontoSaida>
<nContrato>CT20260042</nContrato></duto></infModal>"""

    cte_montar(
        nome, numero=91806, modal="05", globalizado=True,
        corpo=f"""<infCTeNorm>{CARGA.format(xoutcat='')}
<infDoc><infOutros><tpDoc>10</tpDoc><nDoc>9001</nDoc><dEmi>2026-09-01</dEmi></infOutros></infDoc>{duto}
<infGlobalizado><xObs>Procedimento efetuado conforme Resolucao/Portaria do regime especial.</xObs></infGlobalizado></infCTeNorm>""")


def cte_multimodal(nome: str) -> None:
    """Multimodal, com seguro."""
    multimodal = f"""<infModal versaoModal="4.00"><multimodal><COTM>COTM000123</COTM><indNegociavel>1</indNegociavel>
<seg><infSeg><xSeg>SEGURADORA EXEMPLO SA</xSeg><CNPJ>{CNPJ_OUTRA_NF}</CNPJ></infSeg>
<nApol>APL55120</nApol><nAver>AV99012</nAver></seg></multimodal></infModal>"""

    cte_montar(
        nome, numero=91807, modal="06",
        corpo=f"""<infCTeNorm>{CARGA.format(xoutcat='')}{docs_nfe(294040)}{multimodal}</infCTeNorm>""")


def cte_cnpj_alfanumerico(nome: str) -> None:
    """
    Emitente, remetente e destinatario com CNPJ alfanumerico (IN RFB
    2.229/2024), em retrato. A chave do CT-e e as das NF-e transportadas trazem
    letras nas posicoes do CNPJ (NT Conjunta 2025.001, item 5): o codigo de
    barras sai no modelo hibrido 128C/128A, e o CNPJ/CPF EMITENTE dos
    documentos originarios sai da chave de cada nota, com as letras.

    Retrato porque e onde a coluna da chave e mais estreita - e o codigo com
    letras, mais comprido que o so de digitos.
    """
    cte_montar(
        nome, numero=91808, tpimp="1",
        corpo=f"""<infCTeNorm>{CARGA.format(xoutcat='')}{docs_nfe(294050, 294051, 294052)}{rodo()}</infCTeNorm>""",
        cnpj_alfanumerico=(CNPJ_EMIT, CNPJ_REM, CNPJ_DEST))


# --------------------------------------------------------------- MDF-e ----

def mdfe(nome: str, *, documentos: int, tpemis: str = "1",
         cnpj_alfanumerico: tuple[str, ...] = ()) -> None:
    ch = chave("41", "2609", "11222333000181", "58", 1, 5512, tpemis, 33440077)

    municipios = ["SAO PAULO", "CAMPINAS", "RIBEIRAO PRETO"]
    blocos = []
    por_municipio = max(1, documentos // len(municipios))
    i = 0

    for idx, mun in enumerate(municipios):
        chaves = []
        for _ in range(por_municipio):
            chaves.append(
                f"<infNFe><chNFe>{chave('41', '2609', '12222333000148', '55', 1, 295000 + i, '1', 20000000 + i)}</chNFe></infNFe>")
            i += 1
        cod = ["3550308", "3509502", "3543402"][idx]
        blocos.append(
            f"<infMunDescarga><cMunDescarga>{cod}</cMunDescarga>"
            f"<xMunDescarga>{mun}</xMunDescarga>{''.join(chaves)}</infMunDescarga>")

    cont = ""
    if tpemis != "1":
        cont = ("<dhCont>2026-09-14T06:05:00-03:00</dhCont>"
                "<xJust>Falha de comunicacao com o ambiente autorizador</xJust>")

    escrever(nome, alfanumerico(f"""<?xml version="1.0" encoding="UTF-8"?>
<mdfeProc versao="3.00" xmlns="{NS_MDFE}">
<MDFe xmlns="{NS_MDFE}"><infMDFe versao="3.00" Id="MDFe{ch}">
<ide><cUF>41</cUF><tpAmb>1</tpAmb><tpEmit>1</tpEmit><tpTransp>1</tpTransp>
<mod>58</mod><serie>1</serie><nMDF>5512</nMDF><cMDF>33440077</cMDF>
<cDV>{ch[-1]}</cDV><modal>1</modal>
<dhEmi>2026-09-14T08:40:11-03:00</dhEmi><tpEmis>{tpemis}</tpEmis>
<procEmi>0</procEmi><verProc>3.0.2</verProc>{cont}
<UFIni>PR</UFIni><UFFim>SP</UFFim>
<infMunCarrega><cMunCarrega>4106902</cMunCarrega><xMunCarrega>CURITIBA</xMunCarrega></infMunCarrega>
<infPercurso><UFPer>SP</UFPer></infPercurso>
<dhIniViagem>2026-09-14T10:00:00-03:00</dhIniViagem></ide>
<emit><CNPJ>11222333000181</CNPJ><IE>9058250705</IE>
<xNome>TRANSPORTADORA CAMINHO CERTO LTDA</xNome><xFant>CAMINHO CERTO</xFant>
<enderEmit><xLgr>RUA FRANCISCO DEROSSO</xLgr><nro>2986</nro><xBairro>XAXIM</xBairro>
<cMun>4106902</cMun><xMun>CURITIBA</xMun><CEP>81720000</CEP><UF>PR</UF>
<fone>4130916500</fone></enderEmit></emit>
<infModal versaoModal="3.00"><rodo>
<infANTT><RNTRC>12345678</RNTRC></infANTT>
<veicTracao><cInt>001</cInt><placa>ABC1D23</placa><RENAVAM>00912345678</RENAVAM>
<tara>7800</tara><capKG>23000</capKG>
<condutor><xNome>JOAO BATISTA DE SOUZA</xNome><CPF>12345678909</CPF></condutor>
<condutor><xNome>MARCOS PEREIRA LIMA</xNome><CPF>98765432100</CPF></condutor>
<tpRod>06</tpRod><tpCar>02</tpCar><UF>PR</UF></veicTracao>
<veicReboque><cInt>R01</cInt><placa>XYZ9W87</placa><tara>5200</tara>
<capKG>18000</capKG><tpCar>02</tpCar><UF>PR</UF></veicReboque>
</rodo></infModal>
<infDoc>{''.join(blocos)}</infDoc>
<prodPred><tpCarga>05</tpCarga><xProd>PNEUMATICOS E ACESSORIOS</xProd></prodPred>
<tot><qNFe>{i}</qNFe><vCarga>148320.55</vCarga><cUnid>01</cUnid><qCarga>8420.0000</qCarga></tot>
<lacres><nLacre>LAC0099123</nLacre></lacres>
<lacres><nLacre>LAC0099124</nLacre></lacres>
<infAdic><infCpl>Viagem programada para dois dias. Conferir lacres na chegada.</infCpl></infAdic>
</infMDFe></MDFe>
<protMDFe versao="3.00"><infProt><tpAmb>1</tpAmb><verAplic>PR-v3_0_2</verAplic>
<chMDFe>{ch}</chMDFe><dhRecbto>2026-09-14T08:41:03-03:00</dhRecbto>
<nProt>141260399887766</nProt><digVal>def456</digVal><cStat>100</cStat>
<xMotivo>Autorizado o uso do MDF-e</xMotivo></infProt></protMDFe>
</mdfeProc>
""", cnpj_alfanumerico))


# ------------------------------------------------------------- eventos ----

COND_USO = (
    "A Carta de Correcao e disciplinada pelo paragrafo 1o-A do art. 7o do "
    "Convenio S/N, de 15 de dezembro de 1970 e pode ser utilizada para "
    "regularizacao de erro ocorrido na emissao de documento fiscal, desde que "
    "o erro nao esteja relacionado com: I - as variaveis que determinam o "
    "valor do imposto tais como: base de calculo, aliquota, diferenca de "
    "preco, quantidade, valor da operacao ou da prestacao; II - a correcao de "
    "dados cadastrais que implique mudanca do remetente ou do destinatario; "
    "III - a data de emissao ou de saida.")


def evento_nfe_cce(nome: str) -> None:
    ch = chave("41", "2609", "11222333000181", "55", 2, 39167, "1", 15203410)
    id_ev = f"ID110110{ch}01"

    escrever(nome, f"""<?xml version="1.0" encoding="UTF-8"?>
<procEventoNFe versao="1.00" xmlns="{NS_NFE}">
<evento versao="1.00"><infEvento Id="{id_ev}">
<cOrgao>41</cOrgao><tpAmb>1</tpAmb><CNPJ>11222333000181</CNPJ>
<chNFe>{ch}</chNFe><dhEvento>2026-09-18T14:32:05-03:00</dhEvento>
<tpEvento>110110</tpEvento><nSeqEvento>1</nSeqEvento><verEvento>1.00</verEvento>
<detEvento versao="1.00"><descEvento>Carta de Correcao</descEvento>
<xCorrecao>Onde se le "RUA FRANCISCO DEROSSO, 2986" leia-se "RUA FRANCISCO DEROSSO, 2968". Fica tambem corrigido o codigo do produto do item 12, de 3MTB100306 para 3MTB100360.</xCorrecao>
<xCondUso>{COND_USO}</xCondUso></detEvento></infEvento></evento>
<retEvento versao="1.00"><infEvento Id="ID110110{ch}0101">
<tpAmb>1</tpAmb><verAplic>PR-v1_0_0</verAplic><cOrgao>41</cOrgao>
<cStat>135</cStat><xMotivo>Evento registrado e vinculado a NF-e</xMotivo>
<chNFe>{ch}</chNFe><tpEvento>110110</tpEvento><xEvento>Carta de Correcao</xEvento>
<nSeqEvento>1</nSeqEvento><CNPJDest>13222333000114</CNPJDest>
<dhRegEvento>2026-09-18T14:32:41-03:00</dhRegEvento>
<nProt>141260400112233</nProt></infEvento></retEvento>
</procEventoNFe>
""")


def evento_nfe_cancelamento(nome: str, cnpj_alfanumerico: tuple[str, ...] = ()) -> None:
    ch = chave("41", "2609", "11222333000181", "55", 2, 39168, "1", 15203411)

    escrever(nome, alfanumerico(f"""<?xml version="1.0" encoding="UTF-8"?>
<procEventoNFe versao="1.00" xmlns="{NS_NFE}">
<evento versao="1.00"><infEvento Id="ID110111{ch}01">
<cOrgao>41</cOrgao><tpAmb>1</tpAmb><CNPJ>11222333000181</CNPJ>
<chNFe>{ch}</chNFe><dhEvento>2026-09-18T16:10:00-03:00</dhEvento>
<tpEvento>110111</tpEvento><nSeqEvento>1</nSeqEvento><verEvento>1.00</verEvento>
<detEvento versao="1.00"><descEvento>Cancelamento</descEvento>
<nProt>999999999999990</nProt>
<xJust>Pedido cancelado pelo cliente antes do despacho da mercadoria.</xJust>
</detEvento></infEvento></evento>
<retEvento versao="1.00"><infEvento Id="ID110111{ch}0101">
<tpAmb>1</tpAmb><verAplic>PR-v1_0_0</verAplic><cOrgao>41</cOrgao>
<cStat>135</cStat><xMotivo>Evento registrado e vinculado a NF-e</xMotivo>
<chNFe>{ch}</chNFe><tpEvento>110111</tpEvento><xEvento>Cancelamento</xEvento>
<nSeqEvento>1</nSeqEvento><dhRegEvento>2026-09-18T16:10:22-03:00</dhRegEvento>
<nProt>141260400998877</nProt></infEvento></retEvento>
</procEventoNFe>
""", cnpj_alfanumerico))


def evento_cte(nome: str) -> None:
    ch = chave("41", "2609", "11222333000181", "57", 1, 91723, "1", 74113920)

    escrever(nome, f"""<?xml version="1.0" encoding="UTF-8"?>
<procEventoCTe versao="4.00" xmlns="{NS_CTE}">
<eventoCTe versao="4.00"><infEvento Id="ID110160{ch}01">
<cOrgao>41</cOrgao><tpAmb>1</tpAmb><CNPJ>11222333000181</CNPJ>
<chCTe>{ch}</chCTe><dhEvento>2026-09-16T11:48:00-03:00</dhEvento>
<tpEvento>110160</tpEvento><nSeqEvento>1</nSeqEvento>
<detEvento versaoEvento="4.00"><evCECTe><descEvento>Comprovante de Entrega do CT-e</descEvento>
<dhEntrega>2026-09-16T11:40:00-03:00</dhEntrega>
<nDoc>12345678909</nDoc><xNome>CARLOS EDUARDO MOTA</xNome>
<latGPS>-23.550520</latGPS><longGPS>-46.633308</longGPS>
<hashEntrega>a1b2c3d4e5f6</hashEntrega></evCECTe></detEvento>
</infEvento></eventoCTe>
<retEventoCTe versao="4.00"><infEvento Id="ID110160{ch}0101">
<tpAmb>1</tpAmb><verAplic>PR-v4_0_7</verAplic><cOrgao>41</cOrgao>
<cStat>134</cStat><xMotivo>Evento registrado e vinculado ao CT-e</xMotivo>
<chCTe>{ch}</chCTe><tpEvento>110160</tpEvento><nSeqEvento>1</nSeqEvento>
<dhRegEvento>2026-09-16T11:48:31-03:00</dhRegEvento>
<nProt>141260401234567</nProt></infEvento></retEventoCTe>
</procEventoCTe>
""")


def cte_os(nome: str) -> None:
    """
    CT-e OS, modelo 67. Fora do escopo de proposito: a recusa tem de dizer
    QUAL modelo e o arquivo, em vez de um "nao suportado" sem informacao.
    O elemento raiz e CTeOS, e nao CTe - e por ai que o parser o reconhece.
    """
    ch = chave("41", "2603", "11222333000181", "67", 1, 771, "1", 55120843)

    escrever(nome, f"""<?xml version="1.0" encoding="UTF-8"?>
<cteOSProc versao="4.00" xmlns="{NS_CTE}"><CTeOS versao="4.00"><infCte versao="4.00" Id="CTe{ch}">
<ide><cUF>41</cUF><cCT>55120843</cCT><CFOP>5932</CFOP><natOp>TRANSPORTE DE PESSOAS</natOp>
<mod>67</mod><serie>1</serie><nCT>771</nCT><dhEmi>2026-03-12T09:20:00-03:00</dhEmi>
<tpImp>1</tpImp><tpEmis>1</tpEmis><cDV>{ch[-1]}</cDV><tpAmb>1</tpAmb><tpCTe>0</tpCTe>
<procEmi>0</procEmi><verProc>1.0</verProc><cMunEnv>4106902</cMunEnv><xMunEnv>Curitiba</xMunEnv>
<UFEnv>PR</UFEnv><modal>01</modal><tpServ>6</tpServ><indIEToma>1</indIEToma></ide>
<emit><CNPJ>11222333000181</CNPJ><IE>9012345678</IE><xNome>VIACAO EXEMPLO LTDA</xNome>
<enderEmit><xLgr>Rua das Oficinas</xLgr><nro>500</nro><xBairro>Centro</xBairro>
<cMun>4106902</cMun><xMun>Curitiba</xMun><CEP>80010000</CEP><UF>PR</UF></enderEmit></emit>
<vPrest><vTPrest>320.00</vTPrest><vRec>320.00</vRec></vPrest>
</infCte></CTeOS></cteOSProc>""")


def main() -> None:
    # Pasta compartilhada com gerar-amostras-sinteticas.py: sobrescrever por
    # nome, nunca limpar o diretorio.
    DESTINO.mkdir(parents=True, exist_ok=True)

    print("CT-e:")
    cte("4.00", "cte-400-rodoviario.xml", componentes=4, documentos=6)
    cte("3.00", "cte-300-rodoviario.xml", componentes=2, documentos=2)
    cte("4.00", "cte-400-contingencia.xml", componentes=3, documentos=3, tpemis="5")
    cte("4.00", "cte-400-homologacao.xml", componentes=2, documentos=2, tpamb="2")
    cte("4.00", "cte-400-muitos-documentos.xml", componentes=5, documentos=48)
    cte_subcontratacao("cte-400-subcontratacao.xml")
    cte_complemento("cte-400-complemento.xml")
    cte_substituto("cte-400-substituto.xml")
    cte_aereo("cte-400-aereo.xml")
    cte_aquaviario("cte-400-aquaviario.xml")
    cte_ferroviario("cte-400-ferroviario.xml")
    cte_dutoviario("cte-400-dutoviario.xml")
    cte_multimodal("cte-400-multimodal.xml")
    cte_cnpj_alfanumerico("cte-400-cnpj-alfanumerico.xml")

    print("MDF-e:")
    mdfe("mdfe-300-rodoviario.xml", documentos=9)
    mdfe("mdfe-300-muitos-documentos.xml", documentos=90)
    mdfe("mdfe-300-contingencia.xml", documentos=6, tpemis="2")
    # Emitente e emitente das NF-e com letras: a chave do MDF-e e todas as
    # chaves da lista de documentos saem alfanumericas.
    mdfe("mdfe-300-cnpj-alfanumerico.xml", documentos=6,
         cnpj_alfanumerico=("11222333000181", "12222333000148"))

    print("Eventos:")
    evento_nfe_cce("evento-nfe-cce.xml")
    evento_nfe_cancelamento("evento-nfe-cancelamento.xml")
    evento_cte("evento-cte-entrega.xml")
    # Autor e chave da NF-e cancelada com letras no CNPJ.
    evento_nfe_cancelamento("evento-nfe-cnpj-alfanumerico.xml",
                            cnpj_alfanumerico=("11222333000181",))

    print("Deve ser recusado:")
    cte_os("recusar-modelo67-cteos.xml")


if __name__ == "__main__":
    main()
