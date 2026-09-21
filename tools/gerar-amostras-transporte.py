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

from anonimizar import anonimizar  # noqa: E402

RAIZ = pathlib.Path(__file__).resolve().parent.parent
DESTINO = RAIZ / "tests" / "Amostras"

NS_CTE = "http://www.portalfiscal.inf.br/cte"
NS_MDFE = "http://www.portalfiscal.inf.br/mdfe"
NS_NFE = "http://www.portalfiscal.inf.br/nfe"


def dv(chave43: str) -> int:
    """Modulo 11 do MOC: pesos 2..9 ciclicos, da direita para a esquerda."""
    soma, peso = 0, 2
    for c in reversed(chave43):
        soma += int(c) * peso
        peso = 2 if peso == 9 else peso + 1
    resto = soma % 11
    return 0 if resto in (0, 1) else 11 - resto


def chave(cuf, aamm, cnpj, mod, serie, numero, tpemis, cnf) -> str:
    base = f"{cuf}{aamm}{cnpj}{mod}{serie:0>3}{numero:0>9}{tpemis}{cnf:0>8}"
    assert len(base) == 43, len(base)
    return base + str(dv(base))


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

    cont = ""
    if tpemis in ("5", "7"):
        cont = ("<dhCont>2026-09-14T07:22:00-03:00</dhCont>"
                "<xJust>Indisponibilidade do ambiente autorizador da SEFAZ de origem</xJust>")

    escrever(nome, f"""<?xml version="1.0" encoding="UTF-8"?>
<cteProc versao="{versao}" xmlns="{NS_CTE}">
<CTe xmlns="{NS_CTE}"><infCte versao="{versao}" Id="CTe{ch}">
<ide><cUF>41</cUF><cCT>74113920</cCT><CFOP>6352</CFOP>
<natOp>PRESTACAO DE SERVICO DE TRANSPORTE</natOp>
<mod>57</mod><serie>1</serie><nCT>91723</nCT>
<dhEmi>2026-09-14T09:18:37-03:00</dhEmi><tpImp>2</tpImp><tpEmis>{tpemis}</tpEmis>
<cDV>{ch[-1]}</cDV><tpAmb>{tpamb}</tpAmb><tpCTe>0</tpCTe><procEmi>0</procEmi>
<verProc>4.0.7</verProc>{cont}
<cMunEnv>4106902</cMunEnv><xMunEnv>CURITIBA</xMunEnv><UFEnv>PR</UFEnv>
<modal>01</modal><tpServ>0</tpServ>
<cMunIni>4106902</cMunIni><xMunIni>CURITIBA</xMunIni><UFIni>PR</UFIni>
<cMunFim>3550308</cMunFim><xMunFim>SAO PAULO</xMunFim><UFFim>SP</UFFim>
<retira>1</retira><indIEToma>1</indIEToma>
<toma3><toma>3</toma></toma3></ide>
<compl><xObs>Mercadoria acondicionada em paletes. Entrega somente em horario comercial.</xObs></compl>
<emit><CNPJ>11222333000181</CNPJ><IE>9058250705</IE>
<xNome>TRANSPORTADORA CAMINHO CERTO LTDA</xNome><xFant>CAMINHO CERTO</xFant>
<enderEmit><xLgr>RUA FRANCISCO DEROSSO</xLgr><nro>2986</nro><xBairro>XAXIM</xBairro>
<cMun>4106902</cMun><xMun>CURITIBA</xMun><CEP>81720000</CEP><UF>PR</UF>
<fone>4130916500</fone></enderEmit></emit>
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
<infCTeSupl><qrCodCTe>https://dfe-portal.svrs.rs.gov.br/cte/qrCode?chCTe={ch}&amp;tpAmb={tpamb}</qrCodCTe></infCTeSupl>
</infCte></CTe>
<protCTe versao="{versao}"><infProt><tpAmb>{tpamb}</tpAmb><verAplic>PR-v4_0_7</verAplic>
<chCTe>{ch}</chCTe><dhRecbto>2026-09-14T09:19:02-03:00</dhRecbto>
<nProt>141260378123456</nProt><digVal>abc123</digVal><cStat>100</cStat>
<xMotivo>Autorizado o uso do CT-e</xMotivo></infProt></protCTe>
</cteProc>
""")


# --------------------------------------------------------------- MDF-e ----

def mdfe(nome: str, *, documentos: int, tpemis: str = "1") -> None:
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

    escrever(nome, f"""<?xml version="1.0" encoding="UTF-8"?>
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
""")


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


def evento_nfe_cancelamento(nome: str) -> None:
    ch = chave("41", "2609", "11222333000181", "55", 2, 39168, "1", 15203411)

    escrever(nome, f"""<?xml version="1.0" encoding="UTF-8"?>
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
""")


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

    print("MDF-e:")
    mdfe("mdfe-300-rodoviario.xml", documentos=9)
    mdfe("mdfe-300-muitos-documentos.xml", documentos=90)
    mdfe("mdfe-300-contingencia.xml", documentos=6, tpemis="2")

    print("Eventos:")
    evento_nfe_cce("evento-nfe-cce.xml")
    evento_nfe_cancelamento("evento-nfe-cancelamento.xml")
    evento_cte("evento-cte-entrega.xml")

    print("Deve ser recusado:")
    cte_os("recusar-modelo67-cteos.xml")


if __name__ == "__main__":
    main()
