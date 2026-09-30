<img src="ICON.png" alt="" width="96">

# FiscalDoc

Visualizador e impressor de documentos fiscais eletrônicos brasileiros.
Abre o XML, desenha a representação gráfica e imprime. Local, sem rede, sem
estado, sem nada em segundo plano.

| Documento | Representação | Situação |
|---|---|---|
| NF-e modelo 55 | DANFE retrato e paisagem, A4 | MOC 7.00 Anexo II, tabela §3.8.1 |
| NFC-e modelo 65 | DANFE NFC-e, bobina de 80 mm | Manual do DANFE NFC-e v6.0; ver *Fidelidade* |
| CT-e modelo 57 (3.00 e 4.00) | DACTE, A4 | Blocos do MOC; ver *Fidelidade* |
| MDF-e modelo 58 (3.00) | DAMDFE, A4 | Blocos do MOC; ver *Fidelidade* |
| Eventos de NF-e, CT-e e MDF-e | Desenho próprio, A4 | Não há norma; ver *Fidelidade* |

## Baixar

O instalador está em **[Releases](https://github.com/xp3z41x/FiscalDoc/releases)**.

**O instalador não é assinado.** O SmartScreen mostra *"O Windows protegeu o seu
PC"* e o UAC diz **Editor: Desconhecido**. Para prosseguir: *Mais informações* ›
*Executar assim mesmo*. Isso não é descuido — é o que custa um certificado de
assinatura de código, e o instalador já está preparado para usar um (veja o
bloco `#ifdef Assinar` em `installer/FiscalDoc.iss`). Enquanto não há,
cada release publica o SHA-256 do arquivo; confira antes de executar:

```
Get-FileHash .\FiscalDoc-1.1-instalador.exe -Algorithm SHA256
```

## Requisito

**.NET 10 Desktop Runtime (x64).** O aplicativo é framework-dependent de
propósito: sobre o runtime compartilhado a partida a frio é mais rápida do que
com uma cópia privada, porque esses arquivos já estão no cache do Windows
Defender, aquecidos por qualquer outro aplicativo .NET da máquina.

Download: https://dotnet.microsoft.com/download/dotnet/10.0

## Compilar

```
dotnet build -c Release
dotnet test tests/FiscalDoc.Tests -c Release
```

Instalador (requer [Inno Setup](https://jrsoftware.org/isdl.php) 6 ou 7):

```
iscc installer\FiscalDoc.iss
```

## Instalação

**Por máquina, exclusivamente.** Instala em `%ProgramFiles%\FiscalDoc` e serve
todos os usuários. Instalar e desinstalar exigem administrador; o instalador
não oferece caminho alternativo por usuário, para não produzir duas
instalações divergentes na mesma máquina.

Program Files ser somente-leitura para usuário padrão não é problema: o
FiscalDoc **não grava nada** na pasta de instalação em tempo de execução — sem
configuração, sem log, sem histórico, sem cache. Ele lê o XML que recebe e
desenha. Por isso nunca pede elevação depois de instalado.

Todo o registro vai para `HKLM`, de modo que a oferta apareça para todos os
usuários. O FiscalDoc se registra como **capaz** de abrir `.xml` e **não** toma
a associação existente: nada é escrito no valor padrão de `.xml` nem em
`UserChoice`.

Vale reparar que isso não muda com a instalação por máquina — mesmo rodando
como administrador, o aplicativo padrão continua sendo escolha de **cada
usuário**, em Configurações › Aplicativos padrão. É uma decisão que o Windows
não permite a instalador nenhum tomar desde o Windows 8.

Desinstalar remove tudo que foi criado e nada além disso. As chaves
compartilhadas (`OpenWithProgids`, `RegisteredApplications`) perdem apenas o
valor do FiscalDoc, nunca a chave.

## Fidelidade — leia antes de comparar com outro visualizador

Os cinco documentos **não têm o mesmo grau de norma**, e isso muda o que se
pode prometer:

- **DANFE**: o MOC 7.00 Anexo II publica tabela de coordenadas campo a campo,
  em centímetros, e tamanhos mínimos de fonte. O layout a segue. Os valores
  publicados não fecham ao milímetro entre si (há sobreposições de até 1,5 mm na
  própria tabela), então alturas e larguras são normativas e as posições
  verticais são empilhadas. Documentado em `DanfeMetricas.cs`.

- **DANFE NFC-e**: tem manual próprio — *Manual de Especificações Técnicas do
  DANFE NFC-e e QR Code*, versão 6.0, março de 2025 — e ele é bem mais
  específico que os do CT-e e do MDF-e: fixa as nove divisões, a ordem delas, a
  redação literal de vários textos, a largura mínima do papel (56 mm), a margem
  lateral mínima (2 mm) e o tamanho mínimo do QR Code (25 × 25 mm, nível M,
  UTF-8). **Não** fixa coordenada, corpo de fonte nem largura de coluna — diz
  expressamente que as posições do detalhe de produtos "não são reguladas". Ver
  a seção *NFC-e* abaixo.

- **DACTE e DAMDFE**: os manuais **não trazem coordenadas, fontes nem
  margens** — apenas figuras. "Conforme o MOC" não é afirmação verificável
  aqui. O que se garante é: todos os blocos do modelo oficial, na ordem do
  modelo oficial, legíveis.

  No DACTE, porém, o **conteúdo** é normativo: a seção 3 do Anexo II do MOC
  CT-e 4.00 diz qual tag vai em qual quadro, e o layout a segue. Os documentos
  originários saem como o manual pede — tipo, CNPJ/CPF do emitente e
  série/número da nota —, com a chave de acesso ao lado; o QR Code fica no
  cabeçalho de toda folha; o quadro do tomador sai sempre. Quando o arquivo os
  traz, o DACTE mostra também cobrança (fatura e duplicatas), previsão de
  entrega, características adicionais e ordens de coleta, que o manual não
  exige, e IBS/CBS no mesmo formato do DANFE. O canhoto, opcional no manual,
  não é impresso: serve a quem entrega a carga, não a quem recebe o CT-e.

  No DAMDFE, a única medida que o manual fixa é a do QR Code: no mínimo
  25 × 25 mm (MOC MDF-e 3.00b, Anexo II, 2.3 e 2.6.2), e os oito modelos
  oficiais o põem no canto superior direito. É onde ele sai, em toda folha,
  com o conteúdo de `qrCodMDFe` — inclusive em contingência, quando a URL
  carrega a assinatura da chave.

- **Eventos**: não existe representação gráfica obrigatória em MOC nenhum. O
  desenho é próprio. A Carta de Correção tem texto legal de redação fixa
  (`xCondUso`), mas isso é obrigação do *arquivo*, não de um impresso.

### NFC-e

O DANFE NFC-e sai em **bobina de 80 mm**, com a altura do conteúdo — um cupom
não tem folha, tem comprimento. Um cupom que passe de 29,7 cm passa a paginar
em folhas desse tamanho, porque o próprio manual admite imprimir o DANFE NFC-e
em A4 e uma página de um metro não entra em impressora de folha solta.

Na impressão, o tamanho do papel é procurado na lista do driver: primeiro um de
mesma largura (a bobina, numa térmica de balcão), depois A4, depois o menor em
que o cupom caiba. Numa laser comum o cupom sai **1:1 no alto da folha**, não
esticado.

As nove divisões do manual saem na ordem dele: emitente, detalhe dos itens,
totais e formas de pagamento, consulta pela chave de acesso, consumidor,
identificação da NFC-e e protocolo, QR Code, mensagem fiscal e mensagem do
contribuinte. Três pontos merecem nota:

- **O QR Code é gerado aqui**, em nível M e UTF-8, como o manual manda, a
  partir do campo `qrCode` do arquivo — nunca recalculado. O símbolo é entregue
  ao desenho como **matriz de módulos**, e não como imagem: quem decide quantos
  pontos da impressora vale um módulo é o renderizador, que conhece o DPI real.
  Um QR rasterizado e depois reescalado sai com módulos de larguras desiguais,
  e é exatamente isso que faz leitor de celular recusar.

- **Acréscimos e desconto saem em linhas separadas** — frete, seguro, outras
  despesas e desconto, cada um com o valor que está no arquivo. O manual
  descreve uma linha única somada; essa soma não existe no XML, e o MOC §3.1
  proíbe imprimir o que não conste dele. Pela mesma razão, a linha
  "Valor a pagar R$" só aparece quando de fato houve acréscimo ou desconto —
  regra explícita do manual, que evita repetir o valor total duas vezes.

- **A frase "Não permite aproveitamento de crédito de ICMS" não é impressa.**
  Ela saiu do manual a pedido da Sefaz do Paraná, por induzir a erro quanto ao
  programa Nota Paraná. Continua em muito cupom por aí; aqui não.

Em contingência offline (`tpEmis` 9), o aviso **EMITIDA EM CONTINGÊNCIA /
Pendente de autorização** sai nos dois lugares que o manual exige, o protocolo
é suprimido, e a data de emissão recebe a identificação da via. A segunda via,
que fica à disposição do Fisco, é obrigação do emitente no momento da venda —
não de quem abre o arquivo depois.

### IBS, CBS e IS (Reforma Tributária)

O DANFE imprime um bloco **TRIBUTOS DA REFORMA TRIBUTÁRIA (LC 214/2025)**,
logo após CÁLCULO DO IMPOSTO, em retrato e em paisagem:

| Campo | Origem no XML | Quando aparece |
|---|---|---|
| BASE DE CÁLCULO IBS/CBS | `IBSCBSTot/vBCIBSCBS` | sempre |
| IBS ESTADUAL | `IBSCBSTot/gIBS/gIBSUF/vIBSUF` | sempre |
| IBS MUNICIPAL | `IBSCBSTot/gIBS/gIBSMun/vIBSMun` | sempre |
| **(+) IBS R$** | `IBSCBSTot/gIBS/vIBS` | sempre |
| **(+) CBS R$** | `IBSCBSTot/gCBS/vCBS` | sempre |
| **(+) IS R$** | `ISTot/vIS` | só em operação sujeita a Imposto Seletivo |
| **TOTAL COM IBS/CBS/IS** | `vNFTot` | só quando diverge de `vNF` |

O bloco inteiro some quando o documento não traz nenhum desses grupos, e os
12,7 mm voltam para o quadro de produtos.

**Não existe layout normativo para isto.** A NT 2025.002-RTC v1.51 (jul/2026)
tem uma seção "9. DANFE" cujo corpo inteiro é: *"Alterações no DANFE para
exibir informações relativas aos novos tributos estão em estudo, e serão
publicadas em uma nova versão desta Nota Técnica."* O MOC 7.00 Anexo II segue
o de outubro de 2020, sem uma única menção a IBS, CBS ou Imposto Seletivo.

Logo, este bloco é **convenção**, não norma. Duas decisões nele merecem
explicação:

- **Os rótulos não foram inventados.** A NT 2026.003, que especifica o DANFE
  Simplificado Tipo 2, criou a "Divisão III-A – Informações dos novos impostos
  IBS/CBS" com a redação `(+) CBS R$`, `(+) IBS R$` e `(+) IS R$`. É a única
  redação que um fisco brasileiro publicou para imprimir esses três tributos
  num documento auxiliar de NF-e, então é a adotada aqui. O `(+)` carrega a
  semântica certa: os três são cobrados **por fora** e somam ao valor da nota.

- **`vNFTot` só aparece quando diverge de `vNF`.** Em 2026 os dois vêm iguais
  em todo documento real, porque o art. 348 da LC 214/2025 dispensa o
  recolhimento no ano de teste. Repetir o mesmo número em dois campos seria
  ruído; escondê-lo quando divergir seria omitir o valor que o destinatário
  realmente deve.

Só valores do arquivo são impressos. Não há soma IBS + CBS calculada, ainda
que fosse conveniente: esse total não existe no XML, e o MOC §3.1 proíbe
imprimir informação que não conste dele. Há um teste que falha se alguém
acrescentar.

**Os valores por item não vão na tabela de produtos.** Ela já ocupa os
205,7 mm disponíveis com as doze colunas que o MOC §3.1.7 proíbe suprimir mais
IPI — e o IPI carrega dado real em metade do corpus de teste. Acrescentar
colunas de IBS/CBS exigiria estreitar a descrição do produto. Vale notar que o
único precedente publicado, a Divisão III-A do Simplificado Tipo 2, também é
**totalizador**, não por item.

Quando a SEFAZ publicar o layout oficial, o ajuste é trocar rótulos e reordenar
campos em `DanfeRetrato.DesenharIbsCbs` — não há geometria inventada espalhada
pelo código.

### CNPJ alfanumérico

Desde julho de 2026 a Receita Federal emite CNPJ com letras (IN RFB nº
2.229/2024): as doze primeiras posições — raiz e ordem — admitem letra
maiúscula, e as duas do dígito verificador continuam numéricas. A máscara é a
de sempre: `12.ABC.345/01DE-35`.

A letra entra também na chave de acesso, nas posições do CNPJ do emitente
(`[0-9]{6}[A-Z0-9]{12}[0-9]{26}`). O tratamento segue a **NT Conjunta
2025.001** (ENCAT/RFB), que vale para NF-e, NFC-e, CT-e e MDF-e, e as NTs de
schema de cada projeto (NF-e 2026.004, CT-e 2025.001, MDF-e 2025.001):

- **Dígito verificador.** O da chave e o do CNPJ continuam sendo módulo 11,
  mas cada caractere vale o seu código ASCII menos 48: o dígito vale ele
  mesmo, e `A` vale 17, `B` 18… `Z` 42.
- **Código de barras.** O CODE-128C só codifica dígitos. Com letra na chave, o
  código passa ao modelo híbrido da NT: começa no subconjunto C e troca para o
  A diante das letras. A chave só de dígitos sai exatamente como antes.
- **CNPJ e CPF se distinguem pela forma, e não pela contagem de dígitos.** Um
  CNPJ com três letras tem onze dígitos; contá-los imprimia no campo
  "CNPJ / CPF" um CPF que não está no arquivo.

**A largura mínima de 11,5 cm da NT não é aplicada.** A NT reproduz o texto do
MOC 7.00 Anexo II §2 trocando a largura mínima do código, em impressora não
matricial, de 6 cm para 11,5 cm — tirada de um pior caso de 539 módulos, um
caractere por símbolo. O quadro que o MOC reserva ao código no DANFE retrato
tem 8,03 cm, e a NT não o altera. Pelas próprias regras de codificação da NT,
uma chave de 44 posições chega a no máximo 385 módulos, que no módulo mínimo
de 0,02 cm — mantido pela NT — ocupam 7,7 cm e cabem no quadro do MOC. O
FiscalDoc garante esse módulo mínimo em todos os documentos auxiliares e mantém a
geometria do MOC; no DACTE e no DAMDFE, cuja geometria é livre, a coluna da
chave cresce o necessário.

## Impressão

Agnóstica de impressora. O tamanho do papel vem da lista do próprio driver, a
margem física é lida em tempo de execução, e a escala é calculada contra a área
imprimível daquele equipamento: **1:1 sempre que a impressora permitir**, e
menos apenas na medida exata que o hardware exigir.

Isso é necessário porque o MOC posiciona o conteúdo do DANFE de 0,25 cm a
20,82 cm, deixando 1,8 mm de margem à direita numa folha de 21 cm — menos do
que a margem física de uma laser comum. Quando a redução acontece, o aplicativo
avisa antes de imprimir.

O papel procurado é o do próprio documento: A4 para DANFE, DACTE, DAMDFE e
eventos; bobina para o DANFE NFC-e. Ver a seção *NFC-e*.

Se a saída medir errado, o driver está reduzindo a página por conta própria:
procure "ajustar à página" / "fit to page" nas opções avançadas e desligue.

## Estrutura

```
src/FiscalDoc.Core        leitura de XML e modelo        (net10.0)
src/FiscalDoc.Layout      modelo -> páginas em mm        (net10.0)
src/FiscalDoc.Render.Wpf    páginas -> DirectWrite (tela) e XPS (papel)  (net10.0-windows)
src/FiscalDoc.App         janela, zoom, impressão        (net10.0-windows)
```

Core e Layout têm alvo `net10.0` puro **de propósito**: não conseguem
referenciar `System.Drawing` nem que alguém tente. A separação entre leitura,
layout e desenho é restrição de compilação, não convenção.

## Corpus de teste

A suíte tem duas origens de amostra, e só uma delas está no repositório.

**`tests/Amostras/` — versionadas e anonimizadas.** Cobrem o que documento
comum não tem: contingência (SVC-AN, FS-DA e offline da NFC-e), homologação,
ausência de protocolo, ISSQN, destinatário pessoa física, arquivo em Latin-1,
Imposto Seletivo, cupom de 120 itens, NFC-e sem `infNFeSupl`, CT-e dos seis
modais — de complemento, substituto e de subcontratação inclusive —, MDF-e,
eventos e documentos de emitente com CNPJ alfanumérico.

As de CT-e, MDF-e e evento são montadas do zero a partir da estrutura dos
schemas oficiais. As de NF-e e NFC-e **derivam de documentos reais por
transformação** — é assim que os valores continuam realistas e só a dimensão
sob teste muda. Por isso **toda** saída dos geradores passa por
[`tools/anonimizar.py`](tools/anonimizar.py), que troca CNPJ, CPF, razão
social, endereço, telefone, inscrição estadual, protocolo e descrição de
produto por dado fictício, e **recalcula os dígitos verificadores** de CNPJ,
CPF e chave de acesso — trocar o CNPJ sem refazer o DV da chave produziria
arquivo que os próprios testes recusam. CNPJ alfanumérico é reconhecido e
trocado por outro alfanumérico: reconhecer só dígitos deixaria passar intactos
o CNPJ e a chave de um documento real com letras.

A chamada mora dentro do `escrever` de cada gerador, e não em cada amostra,
justamente para que uma amostra nova não possa esquecer o passo. O resultado é
determinístico: gerar duas vezes produz bytes idênticos.

**`Exemplos XML/` — reais, fora do repositório.** Vinte e dois documentos
emitidos de verdade, que são a referência de fidelidade do projeto. **Não são
publicados**: trazem CPF e nome completo de oito pessoas físicas e CNPJ,
endereço, telefone e dado comercial de vinte e duas empresas. Esse dado é de
terceiros e não é nosso para publicar.

Numa cópia sem essa pasta a suíte **passa**: os 62 testes que exigem documento
real se declaram *ignorados*, com o motivo, em vez de falhar — falha quer dizer
defeito, e não há defeito nenhum em não ter o corpus. Os outros 268 rodam
normalmente, e as PNGs de conferência visual continuam sendo geradas a partir
das amostras sintéticas.

Para usar um corpus próprio, basta pôr os XML numa pasta `Exemplos XML/` na
raiz: os testes descobrem os arquivos por leitura de `ide/mod`, nunca pelo
nome. Note que `gerar-amostras-sinteticas.py` precisa dessa pasta para rodar,
já que deriva dali — as amostras que ele produz, no entanto, já estão
versionadas e anonimizadas, então ninguém precisa rodá-lo para compilar ou
testar. O gerador de transporte não depende de nada e roda sempre.

## Ferramentas

```
python tools/gerar-amostras-sinteticas.py    variações de NF-e e NFC-e
python tools/gerar-amostras-transporte.py    CT-e, MDF-e e eventos
python tools/gerar-icone.py                  ICON.png → os dois .ico e o assistente
pwsh tools/bench/measure-startup.ps1         medição de partida
```

A suíte de testes grava PNGs de todos os documentos em `tests/Saida` para
conferência visual.

## Licença

**MIT.** Copyright © 2026 Bernardo Graunke. O texto completo está em
[`LICENSE`](LICENSE), e o instalador o copia para a pasta de instalação ao lado
do executável — a licença exige que o aviso acompanhe todas as cópias, e quem
recebe a máquina já configurada nunca viu a tela do instalador.

Pode usar, copiar, alterar, distribuir e vender, inclusive em produto fechado,
desde que o aviso de copyright acompanhe. Sem garantia de espécie alguma: o
FiscalDoc desenha o que está no arquivo, e conferir se o documento está correto
continua sendo de quem emite.

O aplicativo não depende de nenhum pacote de terceiros em tempo de execução —
só do .NET 10. As únicas dependências externas do repositório (xunit,
coverlet, Microsoft.NET.Test.Sdk) são de teste, não são distribuídas, e são
licenciadas sob Apache-2.0 e MIT.
