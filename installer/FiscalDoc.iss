; =====================================================================
;  FiscalDoc - instalador
;
;  Inno Setup 6.x ou 7.x.  Compile com:
;      iscc installer\FiscalDoc.iss
;
;  Tres decisoes deste script merecem leitura antes de qualquer alteracao.
;
;  1. INSTALACAO EXCLUSIVAMENTE POR MAQUINA.  PrivilegesRequired=admin,
;     destino em %ProgramFiles%\FiscalDoc e registro somente em HKLM.  Uma
;     instalacao serve todos os usuarios da maquina; instalar e desinstalar
;     exigem elevacao.
;
;     Nao ha PrivilegesRequiredOverridesAllowed: o instalador nao oferece
;     caminho alternativo por usuario.  Sem privilegio de administrador ele
;     recusa e explica, em vez de cair silenciosamente em %LOCALAPPDATA% e
;     produzir duas instalacoes divergentes na mesma maquina.
;
;     Program Files e somente leitura para usuario padrao, e isso nao e
;     problema: o FiscalDoc nao grava NADA na pasta de instalacao em tempo de
;     execucao - sem configuracao, sem log, sem historico, sem cache.  Ele le
;     o XML que recebe e desenha.  Por isso nunca precisa de elevacao depois
;     de instalado.
;
;  2. O INSTALADOR NAO PERGUNTA SE QUER SER O PADRAO PARA .XML - e nao por
;     educacao, mas porque nao conseguiria cumprir um "sim".  Desde o
;     Windows 8 a escolha vive em ...\FileExts\.xml\UserChoice, que e
;     POR USUARIO, protegida por hash e, desde 2024, pelo driver de filtro
;     UCPD.sys.  A Microsoft e explicita: "Windows does not allow programmatic
;     changes to default apps without user interaction in system UI" e
;     "Registry-based changes are not supported for apps".  Instalador que
;     forja o hash dispara o aviso "Um aplicativo padrao foi redefinido" e
;     quebra a cada Patch Tuesday.
;
;     Vale reparar que isso NAO muda com a instalacao por maquina: mesmo
;     rodando como administrador, o padrao continua sendo escolha de cada
;     usuario, na tela do proprio Windows.  O que este instalador faz e
;     registrar o FiscalDoc como CAPAZ de abrir .xml, para todos os usuarios.
;
;  3. NADA EM SEGUNDO PLANO.  Nenhum servico, tarefa agendada, icone de
;     bandeja ou atualizador.  Nao ha entrada em Run, nem executavel alem do
;     proprio FiscalDoc.exe.  Foi por isto que Velopack e Squirrel foram
;     descartados: o stub Update.exe e parte estrutural daqueles formatos.
; =====================================================================

#define AppName        "FiscalDoc"
; MANTER EM DIA COM <Version> em Directory.Build.props.  Sao duas fontes para
; o mesmo numero; divergem no dia em que alguem sobe uma e esquece a outra, e
; ai o instalador mente sobre o que instala.
#define AppVersion     "0.1.0"
#define AppPublisher   "Bernardo Graunke"
#define AppExe         "FiscalDoc.exe"
#define RepoUrl        "https://github.com/xp3z41x/FiscalDoc"
#define ProgId         "FiscalDoc.Document.1"

; Chave do registro sob HKLM\Software.  Separada de AppPublisher porque o nome
; do editor tem espaco ("Bernardo Graunke"), e caminho de registro com espaco
; funciona mas envelhece mal.
#define PublisherKey   "BernardoGraunke"
#define BuildDir       "..\src\FiscalDoc.App\bin\Release\net10.0-windows"

; =====================================================================
; ASSINATURA DIGITAL
;
; Sem assinatura, o SmartScreen mostra "O Windows protegeu o seu PC" e o UAC
; diz "Editor: Desconhecido" - num instalador que pede elevacao, para um
; escritorio de contabilidade.  E o maior obstaculo pratico de implantacao que
; este projeto tem, e nao e um obstaculo tecnico: depende de comprar um
; certificado de assinatura de codigo (OV ou EV) de uma autoridade reconhecida.
;
; Com o certificado em maos, compile assim:
;
;   iscc /DAssinar /DCertSubject="Nome exato no certificado" installer\FiscalDoc.iss
;
; ou, com arquivo .pfx:
;
;   iscc /DAssinar /DCertFile=C:\caminho\cert.pfx /DCertPass=senha installer\FiscalDoc.iss
;
; A ferramenta "signtool" precisa estar registrada no Inno Setup
; (Ferramentas > Configurar assinatura) OU no PATH.  O carimbo de tempo e
; obrigatorio: sem ele a assinatura morre junto com o certificado, e os
; instaladores ja distribuidos voltam a ser "editor desconhecido".
; =====================================================================
#ifdef Assinar
  #ifdef CertFile
    #define AssinaturaCmd "signtool sign /f \"" + CertFile + "\" /p \"" + CertPass + "\" /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 $f"
  #else
    #define AssinaturaCmd "signtool sign /n \"" + CertSubject + "\" /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 $f"
  #endif
#endif

[Setup]
AppId={{8F3C21A4-6D9E-4C17-B0A5-2E7D9C4F8B31}

#ifdef Assinar
; Assina o proprio instalador E o desinstalador que ele grava em disco.
SignTool=fiscaldoc
SignedUninstaller=yes
#endif
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
VersionInfoVersion={#AppVersion}

; Por maquina, sem excecao.  A ausencia de
; PrivilegesRequiredOverridesAllowed e deliberada - ver nota 1 no topo.
PrivilegesRequired=admin
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
DisableDirPage=auto

UninstallDisplayName={#AppName}
UninstallDisplayIcon={app}\{#AppExe}

; Estes tres viram links clicaveis no Painel de Controle.  Ausentes, o painel
; simplesmente nao mostra a linha - que e melhor do que um link morto.
AppPublisherURL={#RepoUrl}
AppSupportURL={#RepoUrl}/issues
AppUpdatesURL={#RepoUrl}/releases

; A licenca aparece como pagina do assistente.  MIT e curta; ninguem e
; obrigado a ler, mas ninguem pode dizer que nao estava la.
LicenseFile=..\LICENSE

OutputDir=.\saida
OutputBaseFilename=FiscalDoc-{#AppVersion}-instalador
Compression=lzma2/max
SolidCompression=yes

; x64.  Com ArchitecturesInstallIn64BitMode, {autopf} resolve para
; "C:\Program Files" (e nao para o "(x86)"), e as escritas em HKLM vao para a
; visao de 64 bits do registro - que e a que o Explorer consulta.
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

WizardStyle=modern
ShowLanguageDialog=no

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Files]
Source: "{#BuildDir}\{#AppExe}";                DestDir: "{app}"; Flags: ignoreversion
; Lista explicita em vez de "*.dll".  O glob levava para dentro de
; Program Files qualquer coisa que estivesse na pasta de build - inclusive a
; sobra de uma referencia removida, que continuaria sendo instalada e
; carregada.  Quatro assemblies, quatro linhas.
Source: "{#BuildDir}\FiscalDoc.dll";             DestDir: "{app}"; Flags: ignoreversion
Source: "{#BuildDir}\FiscalDoc.Core.dll";        DestDir: "{app}"; Flags: ignoreversion
Source: "{#BuildDir}\FiscalDoc.Layout.dll";      DestDir: "{app}"; Flags: ignoreversion
Source: "{#BuildDir}\FiscalDoc.Render.Wpf.dll";  DestDir: "{app}"; Flags: ignoreversion
Source: "{#BuildDir}\*.json";                   DestDir: "{app}"; Flags: ignoreversion
Source: "{#BuildDir}\FiscalDocDocumento.ico";   DestDir: "{app}"; Flags: ignoreversion

; O MIT exige que o aviso de copyright e a permissao acompanhem "todas as
; copias".  Mostrar no assistente nao basta: quem recebe a maquina pronta nunca
; viu o assistente.  O arquivo fica em {app}, ao lado do executavel.
Source: "..\LICENSE";                          DestDir: "{app}"; Flags: ignoreversion

[Icons]
; Menu Iniciar de todos os usuarios, coerente com a instalacao por maquina.
Name: "{commonprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"

[Registry]
; ---------------------------------------------------------------------
;  Todo o registro vai para HKLM: a instalacao vale para a maquina inteira,
;  e HKCU seria visivel apenas para quem executou o instalador - que
;  frequentemente e a TI, nao quem vai usar o programa.
;
;  Chaves de HKCU continuam tendo precedencia sobre as de HKLM na visao
;  mesclada do HKEY_CLASSES_ROOT.  Isso e proposital e desejavel: um usuario
;  que ja tenha preferencia propria para .xml continua com ela.
; ---------------------------------------------------------------------

; ---------------------------------------------------------------------
;  ProgID: a classe do documento.  E para ca que o .xml aponta quando o
;  usuario escolhe o FiscalDoc.
; ---------------------------------------------------------------------
Root: HKLM; Subkey: "Software\Classes\{#ProgId}"; \
    ValueType: string; ValueName: ""; ValueData: "Documento Fiscal Eletrônico"; \
    Flags: uninsdeletekey

; A peca central do requisito "sem sequestrar a associacao existente".
; Documentacao da Microsoft: "Set this optional entry to signal that Windows
; should ignore this ProgID when determining a default handler for a public
; file type.  Regardless of whether this value is set, the ProgID continues to
; appear in the OpenWith shortcut menu and dialog."
; O nome le ao contrario: defini-lo significa NAO assumir o padrao em silencio.
; .xml e um public file type de manual.
;
; Nota de tipo: a Microsoft documenta esta entrada como REG_NONE, e o Inno
; Setup nao tem esse tipo - o "ValueType: none" dele significa "cria a chave
; SEM valor", que nao e a mesma coisa.  Um REG_BINARY vazio tem a mesma
; semantica de "presente, sem dado util", e o que o shell verifica e a
; PRESENCA da entrada.
; De todo modo, a garantia principal de nao sequestrar a associacao nao depende
; disto: esta no fato de este instalador nunca tocar no valor padrao de .xml
; nem no UserChoice.
Root: HKLM; Subkey: "Software\Classes\{#ProgId}"; \
    ValueType: binary; ValueName: "AllowSilentDefaultTakeOver"; ValueData: ""

Root: HKLM; Subkey: "Software\Classes\{#ProgId}\DefaultIcon"; \
    ValueType: expandsz; ValueName: ""; ValueData: "{app}\FiscalDocDocumento.ico"

; Sem ddeexec: DDE e legado, e a Microsoft aponta command/DropTarget como o
; caminho correto.  "%1" SEMPRE entre aspas - caminho com espaco chegaria
; picado em varios argumentos, e "C:\Program Files" tem um.
Root: HKLM; Subkey: "Software\Classes\{#ProgId}\shell\open\command"; \
    ValueType: expandsz; ValueName: ""; ValueData: """{app}\{#AppExe}"" ""%1"""

; ---------------------------------------------------------------------
;  OpenWithProgids: "capaz de abrir", sem tocar no padrao.
;  A documentacao e clara: adicionar aqui NAO muda o handler default, so faz
;  o aplicativo aparecer em "Abrir com".
;  Apenas o VALOR e removido na desinstalacao - jamais a chave, onde moram
;  os outros aplicativos da maquina.
; ---------------------------------------------------------------------
Root: HKLM; Subkey: "Software\Classes\.xml\OpenWithProgids"; \
    ValueType: string; ValueName: "{#ProgId}"; ValueData: ""; \
    Flags: uninsdeletevalue

; O valor padrao de .xml e o UserChoice NAO sao tocados em momento nenhum.

; ---------------------------------------------------------------------
;  Applications\<exe>: metadados do aplicativo.  FriendlyAppName mora aqui,
;  e nao no ProgID.
; ---------------------------------------------------------------------
Root: HKLM; Subkey: "Software\Classes\Applications\{#AppExe}"; \
    ValueType: string; ValueName: "FriendlyAppName"; ValueData: "{#AppName}"; \
    Flags: uninsdeletekey
Root: HKLM; Subkey: "Software\Classes\Applications\{#AppExe}\DefaultIcon"; \
    ValueType: expandsz; ValueName: ""; ValueData: "{app}\{#AppExe},0"
Root: HKLM; Subkey: "Software\Classes\Applications\{#AppExe}\SupportedTypes"; \
    ValueType: string; ValueName: ".xml"; ValueData: ""
Root: HKLM; Subkey: "Software\Classes\Applications\{#AppExe}\shell\open\command"; \
    ValueType: expandsz; ValueName: ""; ValueData: """{app}\{#AppExe}"" ""%1"""

; ---------------------------------------------------------------------
;  App Paths: permite invocar "FiscalDoc.exe" pelo Executar do Windows sem
;  caminho completo, e da ao shell um caminho canonico do binario.  Faz mais
;  sentido numa instalacao por maquina do que numa por usuario.
; ---------------------------------------------------------------------
Root: HKLM; Subkey: "Software\Microsoft\Windows\CurrentVersion\App Paths\{#AppExe}"; \
    ValueType: expandsz; ValueName: ""; ValueData: "{app}\{#AppExe}"; \
    Flags: uninsdeletekey
Root: HKLM; Subkey: "Software\Microsoft\Windows\CurrentVersion\App Paths\{#AppExe}"; \
    ValueType: expandsz; ValueName: "Path"; ValueData: "{app}"

; ---------------------------------------------------------------------
;  Capabilities + RegisteredApplications: e assim que Configuracoes >
;  Aplicativos padrao descobre o FiscalDoc.  Em HKLM a oferta aparece para
;  todos os usuarios da maquina - que e o ponto da instalacao por maquina.
;
;  ApplicationDescription e OBRIGATORIO: "If ApplicationDescription is not
;  provided, the application does not appear in UI lists of potential default
;  programs."
;
;  A subarvore do fornecedor e exclusivamente nossa, entao sai inteira.  O
;  uninsdeletekey precisa estar na RAIZ dela: posto so em ...\Capabilities, ele
;  removeria a folha e deixaria a chave do fornecedor orfa e vazia no
;  registro da maquina.
; ---------------------------------------------------------------------
Root: HKLM; Subkey: "Software\{#PublisherKey}"; Flags: uninsdeletekey

Root: HKLM; Subkey: "Software\{#PublisherKey}\{#AppName}\Capabilities"; \
    ValueType: string; ValueName: "ApplicationName"; ValueData: "{#AppName}"
Root: HKLM; Subkey: "Software\{#PublisherKey}\{#AppName}\Capabilities"; \
    ValueType: string; ValueName: "ApplicationDescription"; \
    ValueData: "Visualiza e imprime a representação gráfica de documentos fiscais eletrônicos (NF-e, NFC-e, CT-e e MDF-e)."
Root: HKLM; Subkey: "Software\{#PublisherKey}\{#AppName}\Capabilities\FileAssociations"; \
    ValueType: string; ValueName: ".xml"; ValueData: "{#ProgId}"

Root: HKLM; Subkey: "Software\RegisteredApplications"; \
    ValueType: string; ValueName: "{#AppName}"; \
    ValueData: "Software\{#PublisherKey}\{#AppName}\Capabilities"; \
    Flags: uninsdeletevalue

[Code]
const
  SHCNE_ASSOCCHANGED = $08000000;
  SHCNF_IDLIST       = $0000;
  SHCNF_FLUSH        = $1000;

procedure SHChangeNotify(wEventId: Integer; uFlags: Cardinal;
  dwItem1, dwItem2: Integer);
  external 'SHChangeNotify@shell32.dll stdcall';

{ O Windows so enxerga a nova associacao depois disto; sem a notificacao, a
  mudanca poderia valer apenas apos reiniciar.  A propria documentacao manda
  chamar tambem ao remover handlers, para invalidar o cache de icones. }
procedure NotificarShell();
begin
  try
    SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST or SHCNF_FLUSH, 0, 0);
  except
    { Falhar aqui nao justifica abortar a instalacao. }
  end;
end;

{ O aplicativo e framework-dependent: exige o .NET 10 Desktop Runtime.
  Foi uma escolha deliberada, e nao so de tamanho - o runtime compartilhado
  tem partida A FRIO mais rapida que uma copia privada, porque seus arquivos
  ja estao no cache do Windows Defender, aquecidos por qualquer outro
  aplicativo .NET da maquina. }
function RuntimeInstalado(): Boolean;
var
  Dir: String;
  Achados: TFindRec;
begin
  Result := False;
  Dir := ExpandConstant('{commonpf64}\dotnet\shared\Microsoft.WindowsDesktop.App');

  if not DirExists(Dir) then
    Exit;

  if FindFirst(Dir + '\10.*', Achados) then
  begin
    try
      Result := True;
    finally
      FindClose(Achados);
    end;
  end;
end;

function InitializeSetup(): Boolean;
begin
  Result := True;

  { Instalacao por maquina exige elevacao.  Com PrivilegesRequired=admin o
    Inno ja solicita o UAC antes daqui; esta verificacao existe para o caso de
    execucao em contexto onde a elevacao nao foi concedida (implantacao
    silenciosa por TI, por exemplo), para que a mensagem seja clara em vez de
    uma falha de acesso negado no meio da copia de arquivos. }
  if not IsAdminInstallMode() then
  begin
    MsgBox(
      'O FiscalDoc é instalado para todos os usuários da máquina, em ' +
      ExpandConstant('{autopf}') + '.' + #13#10#13#10 +
      'Execute o instalador como administrador.',
      mbCriticalError, MB_OK);
    Result := False;
    Exit;
  end;

  if RuntimeInstalado() then
    Exit;

  if MsgBox(
      'O FiscalDoc precisa do .NET 10 Desktop Runtime, que não foi encontrado nesta máquina.' + #13#10#13#10 +
      'Baixe em:  https://dotnet.microsoft.com/download/dotnet/10.0' + #13#10 +
      '(escolha "Desktop Runtime" para x64)' + #13#10#13#10 +
      'Deseja continuar a instalação mesmo assim?',
      mbConfirmation, MB_YESNO) = IDNO then
    Result := False;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
    NotificarShell();
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
    NotificarShell();
end;

{ Nota sobre a desinstalacao.

  As chaves criadas aqui saem todas, pelos flags uninsdeletekey e
  uninsdeletevalue - e sempre o VALOR, nunca a chave, em OpenWithProgids e em
  RegisteredApplications, que sao compartilhadas com outros aplicativos.

  O que NAO sai, e nao deve sair, e o UserChoice de cada usuario: ele e
  protegido pelo UCPD.sys, mora em HKCU (fora do alcance de um desinstalador
  por maquina) e a orientacao da Microsoft e deixar dado de associacao para
  tras, porque "Windows respects the Default value only if the ProgID found
  there is a registered ProgID.  If the ProgID is unregistered, it is ignored."

  Na pratica: se um usuario tiver tornado o FiscalDoc o padrao para .xml e a
  TI desinstalar, o ProgID deixa de existir e o Explorer daquele usuario cai no
  "Como voce quer abrir este arquivo?".  Isso e verificacao explicita da
  Fase 9, nao suposicao. }
