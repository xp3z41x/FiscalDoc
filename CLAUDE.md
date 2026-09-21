# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

FiscalDoc is a Windows desktop viewer/printer for Brazilian electronic fiscal
documents: it opens an XML (NF-e 55, NFC-e 65, CT-e 57, MDF-e 58, or an event)
and draws its legally-defined graphical representation (DANFE / DANFE NFC-e /
DACTE / DAMDFE) on screen and on paper — A4 for everything except the NFC-e,
which is a receipt roll. Local only — no network, no database, no state, no
background process. See `README.md` (Portuguese) for the user-facing
description.

**Code, comments, identifiers and user-facing strings are in Portuguese.**
Follow that — do not introduce English identifiers. User-facing strings carry
proper accents (`INSCRIÇÃO`, not `INSCRICAO`); they are a fiscal document.

## Commands

```bash
dotnet build -c Release                       # 0 warnings required (TreatWarningsAsErrors)
dotnet test tests/FiscalDoc.Tests -c Release  # 350 tests
```

Single test class / single test:

```bash
dotnet test tests/FiscalDoc.Tests -c Release --filter "FullyQualifiedName~Code128CTests"
dotnet test tests/FiscalDoc.Tests -c Release --filter "FullyQualifiedName=FiscalDoc.Tests.Code128CTests.Checksum_usa_modulo_103_com_peso_pela_posicao"
```

Run the app (a path argument opens that document directly):

```bash
./src/FiscalDoc.App/bin/Release/net10.0-windows/FiscalDoc.exe tests/Amostras/homologacao.xml
```

Tooling:

```bash
python tools/gerar-amostras-sinteticas.py   # NF-e + NFC-e variants (contingência, homologação, ISSQN, Latin-1, reforma, troco…)
python tools/gerar-amostras-transporte.py   # CT-e, MDF-e, events, CT-e OS (refused)
python tools/gerar-icone.py                 # ICON.png → the two .ico (10 sizes each) + the wizard .bmp
pwsh tools/bench/measure-startup.ps1 -Iterations 12
iscc installer\FiscalDoc.iss                # → installer/saida/ (Inno Setup 6/7)
```

## Architecture: one display list, one renderer, millimetres all the way down

The whole design exists to make **preview and print the same document by
construction**, and to keep millimetre fidelity verifiable.

```
XML file → FiscalDoc.Core      → modelo (POCOs, nullable-aware)
         → FiscalDoc.Layout    → ConjuntoPaginas: flat list of Primitiva in absolute mm
         → FiscalDoc.Render.Wpf→ DirectWrite measures it, DirectWrite draws it
         → FiscalDoc.App       → PaginaView (bitmap) / Impressao (XPS)
```

There is **one** renderer and **one** text metric. `RenderizadorWpf` draws
both destinations from the same `Pagina`; `MedidorTextoWpf` is the only thing
in the app that measures text, and it is what paginates. "FOLHA 2/3" in the
preview is "FOLHA 2/3" on paper not by discipline but because no second
metric exists.

### The renderer's two modes — rasterisation only, never geometry

| | `Modo.Tela` | `Modo.Papel` |
|---|---|---|
| Unit | device pixel | DIP (1/96") — XPS |
| Geometry | snapped to the pixel grid | exact millimetre |
| Stroke width | whole pixels, ≥ 1 | exact mm, ≥ one device dot |
| Who rasterises | WPF, into a bitmap | the printer's RIP, at its own DPI |

Snapping is a screen concession: at 4–5 px/mm the DANFE's 0,15 mm frame is
half a pixel, and unsnapped it becomes two rows of grey everywhere — that is
what "blurry preview" actually was. It must never *move* anything, which is
what `ParidadeTelaPapelTests` pins: strip the text from a real DANFE, render
the remaining geometry in both modes at the same density, and require every
inked pixel of one to find ink in the other within one pixel. It also draws
one of every `Primitiva` type through both modes, so adding a primitive and
handling only one mode fails the build.

**Why DirectWrite and not GDI+.** Measured, at 4–5 px/mm — a whole A4 on a
1440-line screen — GDI+ renders Times New Roman at the 5 and 6 pt the MOC
mandates as a grey, blocky mush; DirectWrite does not. And
`TextFormattingMode.Ideal` is **scale-invariant**: it does not grid-fit
glyphs, so the same line measures the same millimetre at 4 px/mm and at
600 dpi. That is what lets one measurement serve both outputs — the old
600 dpi offscreen GDI+ surface existed only to fake that property.

**Why XPS and not a printer `Graphics`.** The page reaches the driver as
vector; the RIP rasterises at its own resolution. The origin of an XPS page is
the **physical** corner of the sheet, which is exactly where the layout counts
its millimetres, so there is no hard-margin arithmetic at all — a whole class
of defects (margins that do not rotate with the page, "one device dot" derived
from the wrong DPI, a clip `Region` leaked per text primitive) simply has
nowhere to live. What survives is `EscalaImpressao.Calcular`, which shrinks
only as much as that printer's imageable area demands, anchored at the sheet
corner.

Cost of the migration, measured: **+12 ms** to window, **+4,9 MB** working
set. The installer gained nothing to require — `Microsoft.WindowsDesktop.App`,
which it already demands, ships WPF alongside WinForms.

Which layout a document gets is decided in `AberturaDocumento`, and for the
NF-e family it is decided by **`mod`, not `tpImp`**: model 65 always goes to
`DanfeNfce`, model 55 to `DanfeRetrato`/`DanfePaisagem` according to `tpImp`.
A model-65 file whose `tpImp` is 5 ("mensagem eletrônica") or out of range is
still a coupon.

### The layer boundary is enforced by the compiler

`FiscalDoc.Core` and `FiscalDoc.Layout` target plain **`net10.0`**, not
`net10.0-windows`. They therefore *cannot* reference `System.Drawing` even by
accident. **Do not change these TFMs.** When Layout needs text metrics it goes
through `IMedidorTexto`, implemented in Render.Wpf. `FiscalDoc.Render.Wpf`
does not reference `System.Drawing` at all. The only `System.Drawing` that
*draws* anything in the app is the WinForms surface that holds the preview
bitmap and blits it; the one other appearance is `MainForm.Icon`, which the
WinForms API types as `System.Drawing.Icon` and which paints no document.

### Why a display list instead of drawing directly

Pagination must be resolved **before** any primitive is emitted, because
`FOLHA nn/nn` on page 1 needs the total. `DanfeRetrato.Construir` measures every
item, distributes them into pages, and only then draws. Drawing straight to a
canvas would force a separate ghost "measure pass".

## Invariants that will bite you

These were each learned from a real bug. Changing them silently breaks output.

- **One text engine, everywhere.** `MedidorTextoWpf` measures and
  `RenderizadorWpf` draws, both DirectWrite, both `TextFormattingMode.Ideal`.
  Introducing a second engine anywhere — GDI+, `TextRenderer`, a bitmap font —
  re-creates the divergence this architecture exists to prevent: the layout
  would break a line one engine says fits and another says does not.
- **The millimetre → unit conversion is explicit, never a transform.** WPF
  sizes a glyph from the em in the context's own units *before* any transform,
  so hanging a millimetre scale on the `DrawingContext` and drawing in mm
  would treat a 1,76 mm em as 1,76 units and destroy the text. Every
  coordinate is multiplied on the way in.
- **Text measurement is resolution-free.** `TextFormattingMode.Ideal` does
  not grid-fit, so a line measures the same millimetre at any density and
  pagination is a property of the document, not of the device. Switching to
  `Display` would round every glyph advance to a whole pixel — measured, 3,5 %
  wider on the median and 11 % at worst — and pagination would start depending
  on where the document is being shown.
- **Barcodes carry widths in *modules*, not millimetres** (`Code128C` →
  `Primitiva.CodigoBarras`). The renderer quantises the module to a whole number
  of device dots, rounding **down** so it never overflows its box. The encoded
  list starts with the **quiet zone (a space)**, not a bar.
- **The QR Code follows the same rule** (`QrCode` → `MatrizQr` →
  `Primitiva.CodigoQr`): a matrix of modules, never a bitmap, quantised down at
  draw time. Below 4 device dots per module the renderer stops quantising and
  fills the box — that branch is the 96–150 dpi screen preview, where no
  scanner is reading anyway. Because rounding down shrinks the symbol, the box
  `DanfeNfce.LadoQrMm` is 34 mm and not the manual's 25 mm minimum: at 300 dpi
  the quantisation loss still leaves ~25 mm of content.
- **Never round coordinates to integers.** Everything stays `float` mm until the
  final transform.
- **`Modo` changes rasterisation, never geometry.** Screen snaps to the pixel
  grid, paper keeps the exact millimetre; both read the same coordinates from
  the same list. `ParidadeTelaPapelTests` fails if snapping ever moves
  something more than a pixel — and it asserts both bitmaps carry ink, because
  two blank pages used to agree perfectly.
- **On screen, hairlines are snapped to the pixel grid — position *and*
  thickness.** A stroke is centred on its coordinate, so it only covers whole
  rows if the thickness is a whole number of pixels *and* the centre lands
  mid-pixel (odd) or on the boundary (even). Snapping only the position is the
  bug this already had: 0,15 mm at 9,45 px/mm is 1,42 px, which spreads over
  three rows, two of them grey. The DANFE is almost all 0,15 mm frame, so that
  is what "blurry preview" actually was. `EdgeMode.Aliased` alone does **not**
  fix it; measured, only the explicit snap does. On paper the stroke keeps its
  exact millimetre.
- **The scale fits box into box, anchored by a translation — never edge into
  edge.** `EscalaImpressao.Calcular` returns an `AjusteImpressao` (scale *and*
  offset). Fitting only `conteudo.Direita` into `imprimivel.Direita` fixes the
  right edge and **worsens** the left: scaling about the origin pulls the
  content deeper into the strip the printer cannot mark. Measured on a 4,23 mm
  laser margin, the DANFE lost 1,76 mm off its left side — the outer frame and
  the first character of every CÓDIGO — while the on-screen warning read a
  reassuring 98,9 %. `EscalaImpressaoTests` pins every edge on four margin
  widths.
- **Every box that holds variable text is measured, never assumed.**
  `AlturaRodape` used to take the measurer and the styles and ignore both,
  returning the MOC's 30,7 mm constant — so anything past the eleventh line of
  `infCpl` (the schema allows 5000 characters) was clipped with no ellipsis and
  no continuation. It now measures, the quadro grows, and because `Paginar`
  reserves the footer on the last page the room appears by itself.
- **The preview is capped by megapixels, not by zoom.** Each draw allocates
  *two* full-page surfaces, and the WPF one is not disposable — its pixels are
  native memory behind a tiny managed object, so the GC barely notices. A4 at
  5× on a 192 dpi monitor is 89 Mpx: 356 MB each, 713 MB peak per wheel notch.
  `LimiteDeZoom` bounds the raster to 24 Mpx, which still leaves ≥12 px/mm on
  any monitor — four times what the smallest MOC text needs.
- **Input is bounded before it is parsed.** 128 MB on disk and 30 M characters
  in the document (`XmlSource`), roughly ten times the largest document the
  leiaute admits. Without them a 500 MB file — which arrives by e-mail like any
  other — was materialised whole by `XDocument.Load` at five to ten times its
  size on the LOH, and what the user got was a frozen window and then "the
  application needs to close".
- **Measure and draw must use the same width.** `infAdProd` was measured at
  204,5 mm and drawn into 185,7 mm, so the renderer re-broke a line the layout
  had already broken; the extra line had no reserved height and overprinted
  the next item. Any column that wraps at draw time must be measured at draw
  width — which is why only CÓDIGO and DESCRIÇÃO wrap, and both are handed to
  the renderer already split.
- **The device dot is a field of `Modo`, and it carries the print scale.**
  `Modo.Tela` sets `PorPonto = 1` (the unit *is* the pixel); `Modo.Papel` sets
  `96/dpi/escala`. The `/escala` is the part that bites: the module is
  quantised *before* the page-level `ScaleTransform`, so without it a 6-dot
  barcode module prints as 5,93 dots and the bars come out ±1 dot irregular —
  on the normal case, since any printer with a hard margin scales below 1.
- **On screen the text clip box gets exactly one pixel of slack.** Grid fitting
  rounds the line height up, and a 5 pt label in a tight box would lose its
  bottom row ("CHAVE DE ACESSO" sawn in half). One pixel absorbs the rounding
  and nothing more — invading the neighbouring field is still impossible.
  `NitidezTelaTests` pins both halves of that.
- **The preview bitmap is blitted with an explicit source rectangle in
  `GraphicsUnit.Pixel`.** `DrawImageUnscaled` draws by the image's *physical*
  size: let the bitmap's stored DPI drift from the destination `Graphics` DPI
  and GDI+ silently resamples the whole sheet. This is the one place
  `System.Drawing` still *draws*, and only to move pixels to the window.
- **Do not tune geometry by eye from the preview.** It rasterises at 96–192 dpi
  while print is 600+; hairlines and barcode modules look wrong there even when
  the paper is correct.
- **Print scale is measured, not fixed.** `EscalaImpressao.Calcular` runs
  against `PageImageableArea` read from the selected printer at run time —
  exactly 1:1 whenever the hardware allows, smaller only by as much as that
  printer demands, and anchored at the sheet corner (scaling about the centre
  after fitting against the origin clipped the frame on *both* sides).
- **The driver lists paper in portrait only; orientation is a separate axis.**
  A landscape document asks for 297 × 210, which no A4 entry can satisfy —
  `Impressao.EscolherMidia` therefore searches with the measurements rotated
  and sets `PageOrientation` separately. Getting this wrong is not subtle:
  every DACTE and every landscape DANFE fell through to the driver's default
  portrait A4 at ~70 % with the totals column off the sheet.
- **`MOC §3.1`: never print anything that is not in the XML.** No computed
  totals, no synthesised protocol numbers. `IbsCbsTests.Nada_e_calculado_pelo_aplicativo`
  fails if someone adds a convenient sum.

## The normative asymmetry — essential domain context

The four document types do **not** carry the same degree of specification, and
this governs how much fidelity can honestly be claimed:

| Document | Spec status |
|---|---|
| **DANFE** (NF-e) | MOC 7.00 Anexo II publishes a full field-by-field coordinate table in cm, plus minimum font sizes (§3.7) |
| **DANFE NFC-e** | Own manual (ENCAT, *Especificações Técnicas do DANFE NFC-e e QR Code*, v6.0 mar/2025): nine ordered **divisions**, verbatim wording, minimum paper width and QR size — but **no coordinates and no font sizes**, and it says outright that item-detail positions "não são reguladas" |
| **DACTE** (CT-e) | Manual has **figures only** — no coordinates, no fonts, no margins |
| **DAMDFE** (MDF-e) | Same: §2.7.1 says only "papel comum, retrato ou paisagem" |
| **Events** | **No mandatory graphical representation exists in any MOC** |
| **IBS/CBS** (Reforma) | NT 2025.002-RTC §9 says the DANFE changes are *"em estudo"*. Nothing published. |

Consequences baked into the code:

- `Danfe/DanfeMetricas.cs` is the transcription of MOC §3.8.1 and is **the only
  file containing DANFE coordinates**. The published values do not close to the
  millimetre (the table overlaps itself by up to 1,5 mm), so *heights and widths*
  are treated as normative and *vertical positions are stacked*. The produtos
  quadro is the elastic block that absorbs the remainder.
- DACTE, DAMDFE and events go through `Composition/MontadorDocumento.cs`, a
  block-stacking engine with automatic pagination — deliberately not
  pixel-coordinates, because there is nothing to be faithful *to*. The agreed
  bar is "a human reads the document correctly".
- **DANFE retrato and paisagem are two separate layouts**, not one rotated. In
  paisagem the canhoto becomes a vertical strip on the left edge, block titles
  become 5,1 mm vertical tabs, and row height drops from 8,5 to 6,4 mm.
- **The NFC-e is a third layout, not a narrow DANFE.** `Nfce/DanfeNfce.cs`
  stacks the manual's nine divisions on an 80 mm roll whose height is the
  content's; it paginates onto 297 mm pages only when the content outgrows an
  A4, which the manual explicitly allows as paper. The verbatim legal strings
  live in `Nfce/TextosNfce.cs`, the way `Danfe/TextosMoc.cs` holds the DANFE's.
- When adding a block to the DANFE, cite the MOC section in the doc comment, or
  state plainly that it is a house convention (see `DesenharIbsCbs`). The same
  applies to the NFC-e against its own manual — including one deliberate
  departure already recorded there: the manual describes a single summed
  "Acréscimos/Desconto" line, and the layout prints `vFrete`, `vSeg`, `vOutro`
  and `vDesc` separately because the sum is not in the XML and §3.1 forbids
  printing what is not.

## Test corpus

- `Exemplos XML/` — **10 real NF-e + 12 real NFC-e** from the user. **Not in
  the repository** (`.gitignore`): they carry 8 real people's CPF and full name
  plus 22 companies' CNPJ, address and phone. Tests that need them are marked
  `[FatoComCorpusReal]` / `[TeoriaComCorpusReal]` and report as *skipped*, not
  failed, on a clone without the folder — a plain `[Fact]` looping over
  `Amostras.ReaisNfe()` would go green over an empty list, which is why
  `Amostras.ExigirCorpusReal()` fails loudly if the attribute is missing. The fidelity
  reference. The NF-e cover retrato/paisagem, 2–99 items, `infAdProd`, 118-char
  descriptions, a file with no encoding declaration, and IBS/CBS in 9 of 10. The
  NFC-e cover 1–14 items, cash and PIX, troco, and three with no consumer
  identified. `Amostras.ReaisNfe()` / `ReaisNfce()` split them **by reading
  `ide/mod`**, not by filename — a test that wants one family must ask for it.
- `tests/Amostras/` — generated by the two Python scripts, for what the real
  corpus lacks (contingência, homologação, no protocol, ISSQN, CPF, Latin-1,
  CT-e, MDF-e, events, Imposto Seletivo; for the NFC-e: offline contingency,
  homologação, discount and surcharge, several payment methods with troco,
  foreign consumer, a 120-item roll, and a file with no `infNFeSupl`).
  **Both scripts write into this one folder — never `rmtree` it from either.**
  The NF-e/NFC-e samples are *derived from the real corpus by transformation*,
  not built from nothing — which is why every write goes through
  `tools/anonimizar.py`. It swaps CNPJ, CPF, name, address, phone, IE,
  protocol and product description for fictitious values and **recomputes the
  check digits** of CNPJ, CPF and the access key. The call sits inside each
  generator's `escrever`, never per-sample, so a new sample cannot skip it.
  Output is deterministic: generating twice yields identical bytes. Three
  tests pin values it produces (`TransporteTests`, `VariacoesTests`); change a
  name pool and they need updating.
- CT-e / MDF-e / event samples are **synthetic**, built from the official schema
  structure. Structurally faithful, but not a substitute for real documents.
- `tests/Saida/` — the suite renders every document to PNG here (via
  `GeradorVisual`) for visual inspection. Gitignored. Look at these after
  changing any layout; assertions do not catch a crooked quadro.

## Notes

- Framework-dependent on purpose: the shared .NET runtime has **faster cold
  start** than a self-contained copy, because Defender has already cached those
  files (dotnet/runtime#78379). Do not enable `PublishReadyToRun` (small apps
  gain nothing, binary grows 2–3×) or NativeAOT/trimming (SDK-blocked for
  WinForms, `NETSDK1175`).
- **`ICON.png` at the root is the icon's single source.** `tools/gerar-icone.py`
  reduces it to the ten sizes Windows asks for (16, 20, 24, 32, 40, 48, 64, 96,
  128, 256), writes both `.ico` files, and writes the installer wizard's
  `installer/imagens/assistente-{55,83,110}.bmp`. Three things in there are not
  decoration: the reduction runs in **premultiplied alpha**, or every stroke
  gets a dark fringe from the RGB hiding under transparent pixels; sizes
  ≤ 48 are written as **DIB**, ≥ 64 as **PNG**, because the classic Win32 paths
  still expect DIB in the small sizes; and the BMPs carry **premultiplied
  alpha with a plain 40-byte BI_RGB header**, which is the only form Inno's
  loader reads as 32-bit — paired with `WizardImageAlphaFormat=premultiplied`
  in the `.iss`, or the transparent background prints as a black square.
  Below 32 px the art's 7,8 % stroke falls under 2 px and area averaging
  leaves it washed out, so `adensar` gives the alpha back on a taper that
  reaches 1,0 exactly at 32. Do not hand-edit the generated files —
  regenerate them.
- **The icon has to be applied in two channels, not one.** `ApplicationIcon`
  gives the *file* a face (Explorer, Start menu, pinned shortcut); the
  *window* — title bar, Alt+Tab, taskbar button — reads `Form.Icon`, and
  WinForms fills that with its own generic `wfc.ico` unless something assigns
  it. So the same `FiscalDoc.ico` is also an `EmbeddedResource` and
  `MainForm.CarregarIcone` loads it whole, letting WinForms pick a real frame
  for each of the two sizes instead of downscaling one at run time. Shipping
  only the first channel is exactly the defect this file exists to prevent
  repeating: the app had two faces, the right one on the shortcut and the
  framework's on screen.
- DPI is `PerMonitorV2` via the `ApplicationHighDpiMode` **project property**,
  not the manifest — the WinForms analyzer `WFO0003` treats DPI in the manifest
  as an error.
- The installer is **per-machine only** (`PrivilegesRequired=admin`,
  `{autopf}`, HKLM). It registers FiscalDoc as *capable* of opening `.xml` via
  `OpenWithProgids` + `AllowSilentDefaultTakeOver`, and **never touches** the
  `.xml` default value or `UserChoice` — Windows has blocked programmatic
  default-app changes since Windows 8 (UCPD.sys).
- The app writes nothing to its install directory at run time — no config, no
  log, no cache. That is what makes read-only Program Files viable; keep it so.
- **Identity metadata lives in three files and they must agree.**
  `Directory.Build.props` holds `Version`, `Company`, `Product`, `Authors`,
  `Copyright` and `Description` — these become the Win32 version resource, i.e.
  what Explorer's Description column, Properties > Details and Control Panel
  read. `installer/FiscalDoc.iss` repeats the version in `#define AppVersion`
  and the publisher in `#define AppPublisher`. `src/FiscalDoc.App/app.manifest`
  repeats it a third time in `assemblyIdentity version`, which needs four
  parts. There is no build step linking them; bump one, bump all.
- **Licence: MIT, Bernardo Graunke.** `LICENSE` at the root, `LicenseFile` in
  the installer wizard, and a copy installed into `{app}` — MIT requires the
  notice to travel with every copy, and nobody who inherits a configured
  machine ever saw the wizard. No runtime third-party dependency exists; keep
  it that way, or the licence section of `README.md` stops being true.
- `~/.codex/config.toml` exists on this machine. To bring any of it across
  (MCP servers, slash commands, subagents, skills, instructions), reply
  `/import` to see what is importable, then `/import --yes=<digest>` to apply.
