# SmoothDraw

Transparent overlay for drawing on the screen while recording tutorials: pen, highlighter,
laser, eraser, shapes, text, pasted images, undo/redo, confetti, a stopwatch, a thinking timer,
a board and the cursor ring. WPF / .NET 8, Wacom pen and touch screen support. Omri calls it
"Smooth Draw". Runs on the home PC (where Omri records, under OBS) and the laptop; nothing runs
on the VPS.

Until 2026-09-28 it was **SmoothAnnotate**, inside the `smoothzoom` repo. SmoothZoom was retired
on 2026-10-18 and its cursor ring moved here. **History** (dated incidents, decisions, test runs,
installs): `CHANGELOG.md`, not auto-loaded; the end of it holds the text moved out of this file
on 2026-11-15.

## Repo layout
```
src/SmoothDraw/            the app (Structure below)
src/Shared/ControlPipe.cs  the control pipe; the SAME file is in the retired smoothzoom repo
deploy/                    publish.ps1 · install.ps1 · make_icons.py ("Install / update")
assets/smoothdraw.ico      drawn by deploy/make_icons.py (white pencil on a red tile)
tools/touch-test.ps1       touch / pen / undo / key checks without hands
THIRD-PARTY-NOTICES.md     Tabler Icons (MIT), the toolbar's icons
```

## Hotkeys
⚠️ **Only F9, F5, F6 and Ctrl+Alt+H act in every program. Every other key below acts only while
already drawing** (`KeyboardHookService.IsDrawing`). Caught everywhere, they hijacked ordinary
keys in other programs (Ctrl+1 switched the pen on, Ctrl+V pasted a screenshot onto the screen).

⚠️ **A key that acts while drawing is kept from the program underneath**: the key-down AND its
key-up (`_swallowUp`; Ctrl / Shift / Alt always go through). **Exception: while a text box on the
overlay is being typed in** (`IsTyping`) nothing is kept, and Ctrl+Z / Ctrl+Y / Ctrl+V belong to
the text box. The hook only decides; the work is queued with `Dispatcher.BeginInvoke`, never run
inside Windows' hook call (Esc is the exception: whether it is kept depends on its answer).

⚠️ **F8 belongs to OBS** (zoom-to-mouse, OBS-dashboard repo; swapped with F9 on 2026-10-13) and
F7 too (camera full / small): never bind them here. F9 is passed on to other programs.

| Key | Action |
|-----|--------|
| F6 | **Thinking timer, 10 s, from anywhere.** Shift+F6 = 30 s, Ctrl+F6 = 60 s, Ctrl+Shift+F6 = clear the drawings. The same length again stops it. Kept from other programs |
| F5 | **The board, from anywhere**: on (and the pen) / off. Kept from other programs; Ctrl+F5 still reaches them |
| PageDown / → , PageUp / ← | **On the board only:** next / previous page (past the last = a new page) |
| F9 | Toggle draw mode, from anywhere. Not kept from other programs |
| F10 | Clear all (undoable) |
| F11 | Laser on/off; pressed again it goes back to the mouse, never to the pen |
| Esc | Stop drawing. While typing text, the first Esc finishes the text. Kept only when it stopped something |
| F12 | Stopwatch start/pause (double-tap = reset) |
| Ctrl+Z / Ctrl+Y, Ctrl+Shift+Z | Undo / redo (never with Alt) |
| Ctrl+0 … Ctrl+8 | Mouse, Pen, Highlighter, Laser, Eraser, Arrow, Rectangle, Circle, Text |
| Ctrl+V | Paste image from clipboard |
| Ctrl+Alt+A / R / O / X | Arrow / Rectangle / Circle / Text tool |
| Ctrl+Alt+H | **The cursor ring on/off, from anywhere** (the pad's ◎ types it). Kept from other programs |
| Ctrl+Alt+T | Stopwatch show/hide |
| Ctrl+Alt+1-5 | Colours: Red, Blue, Green, White, Yellow |

## The thinking timer and the board
Both are on the REAL screen (the overlay), so OBS's Display Capture and a Zoom screen share both
show them. OBS's F8 zoom crops them like everything else.
- **The keys** (`KeyboardHookService.GlobalKey`): F6 / Shift+F6 / Ctrl+F6 / Ctrl+Shift+F6 / F5 act
  from any program, drawing or not, and are KEPT from it (F5 no longer refreshes a browser; Omri
  accepted that). With Alt they are left alone, and so is Ctrl+F5. A held key acts once (repeats
  are swallowed until key-up). The modifiers also count in their plain codes (VK_SHIFT 0x10 …):
  a key typed by a program (the pad's `press_key.py`, `keybd_event`) may come that way.
- **The pad:** page 4 of the AKP03E's "Default" profile (davinci-automation `generate_deck.py`
  `TEACH_KEYS`): ⏳ 10 / 30 / 60 · 🖊 board · ✏ drawing (F9) · 🧹 clear (Ctrl+Shift+F6, never F10:
  F10 acts only while drawing). Change those keys in both repos together.
- **The timer** (`Views/CountdownHud.cs`, in the overlay's `CountdownHost`): a dark disc, an amber
  ring from 12 o'clock that shrinks as time runs out (drawn on `CompositionTarget.Rendering`, real
  elapsed time), the seconds in the middle (m:ss from a minute up). The last 3 turn red and pop; at
  zero it flashes twice and fades (~1.1 s). **Never a sound** (it would land in the recording).
  The same length again = fades out at once; another length = starts over. Logged `Countdown 10s:
  started / done`.
  - **Where:** the place picked on the OBS remote's ⚙️ (pipe `countdown place <pos>`), saved in
    `%APPDATA%\SmoothDraw\countdown.json` (`{"place": "..."}`, `Services/CountdownPlaceService.cs`,
    written atomically), effective at once, also on a running timer. No file = settings.json's
    `CountdownPosition`, else top-right. 36 DIP from the edges, above the taskbar (work area);
    `CountdownSize` 170 DIP. The overlay moves to the cursor's screen first unless drawing or
    drawings are left on the screen. The place is NOT in settings.json on purpose (a saved
    settings.json pins every value, Settings below).
  - **Follows the OBS take:** the OBS dashboard sends `countdown pause` (frozen, disc dims to 0.45
    with a ⏸ under the number), `countdown resume` (the paused span is added to the start) and
    `countdown stop` (quick fade, no flash) when the take is paused / resumed / restarted 🔄,
    cancelled 🗑 or a part redone ↩️. Each is a no-op when there is nothing to do. While paused,
    the same length stops it and another starts it fresh. Logged `Countdown 30s: paused / …`.
- **The board** (`OverlayWindow.ToggleBoard`): a `BoardColor` (#121417) rectangle under the ink over
  the whole monitor, and the pen on. F5 again = the screen and its drawings come back, and drawing
  stops if it was off before. Stopping drawing any other way (Esc, the toolbar's mouse, F9) takes
  the board down too: the overlay is click-through then.
  - **Two surfaces:** the board's drawings and the screen's are apart, each with its own undo
    history (`Surface`, `SwapSurface`). The canvas keeps the SAME collections (history steps point
    at them); only their contents change. A text being typed is finished and a shape or drag under
    way dropped first, as Clear does; laser strokes go. Clear and undo act on the one on show.
  - **Pages:** a list of `Surface`s (`_pages`, `_page`); `_screen` holds the screen's while the board
    is up. `TurnPage(±1)`: PageDown / → / PageUp / ← while the board is up
    (`KeyboardHookService.IsBoardOn`; a text box being typed in keeps the arrows), the toolbar's
    ◀ n/m ▶ (`PageGroup`, board only, before ✕), the pipe's `page next` / `page prev`. Past the last
    = a new page, **unless the page on show is empty**. Before the first = nothing. F5 off/on opens
    on the page you left. Pages live until SmoothDraw quits. The bar grows (~113 DIP) and is pulled
    back inside the screen (`KeepInsideWorkArea`).
  - ⏳ Never checked: the board's drawings coming back on a second F5 after a real stroke.

## The cursor ring (Ctrl+Alt+H)
`Services/CursorRingService.cs`, ported from SmoothZoom's `CursorHighlightService`: the ring only,
not the zoom and not its OBS watcher. **The ring never comes on by itself.**
- **On / off:** Ctrl+Alt+H from any program (`GlobalKey`, kept from the app in front; the AKP03E's ◎
  types it, so keep that key), the tray's "Cursor ring (Ctrl+Alt+H)", the pipe's `ring toggle` /
  `ring on` / `ring off` (the OBS remote's ◎). Every reply carries `ring`. Logged `Ring on/off`.
- **What it is:** a yellow ring (`#DCFFE632`, 70 DIP, 4 thick, fill `#30FFE632`, soft glow) in a small
  topmost window that follows the mouse every 16 ms. Click-through (`WS_EX_TRANSPARENT`), never
  activated (`ShowActivated` false, `WS_EX_NOACTIVATE`). **Not** hidden from capture: OBS records it.
  Once a second it goes back on top.
- **Click ripple:** left = the ring's colour, right = red; grows 2× and fades in 350 ms. Its low-level
  mouse hook exists only while the ring is on. Injected clicks are skipped.
- **Settings:** `RingSize`, `RingThickness`, `RingColor`, `RingFill`, `ClickRipple`. Each unset =
  SmoothZoom's value in `%APPDATA%\SmoothZoom\settings.json` (`HighlightRingSize` …), else the
  defaults above (that is how the laptop has 60). The log's `Ring: size …` line says which.
- A machine still running SmoothZoom would catch Ctrl+Alt+H in both apps.

## The toolbar
- **Light bar:** white, 1 px `#D6D6E0` border, radius 14, soft shadow. Dark `#3B3B46` line icons from
  **Tabler** (MIT), as WPF geometries in `Views/Icons.xaml` (source names and version at its top).
  Active tool = `#E9E8FF` pill with a `#3C34A8` icon; hover `#F0F0F5`. Order: grip · Mouse, Select ·
  Pen, Highlighter, Laser, Eraser · Arrow, Rectangle, Circle, Text · **options slot** · 5 colours ·
  Undo, Redo · Confetti (amber), Clear (bin red on hover) · ⏳ (+ 10 30 60 when open) · (board only:
  ◀ page n/m ▶) · ✕.
- **Never use emoji for toolbar icons:** WPF draws emoji in one colour (they were invisible, black on
  a dark bar).
- **⏳** (tooltip "טיימר חשיבה (F6)"): a click opens 10 · 30 · 60 **inside the bar**
  (`CountdownGroup`), never a popup (a popup is its own window: recorded, and not a hole in the hit
  layer). A pick = `StartCountdown`, exactly F6 / Shift+F6 / Ctrl+F6. The running one is lit
  (`SetCountdown`, from `CountdownHud.Ended`). It stays open until ⏳ again or the bar hides.
- **Options slot:** thickness for Pen / Arrow / Rectangle / Circle, text size for Text. Click to
  cycle. Its buttons are `Hidden`, never `Collapsed`: **the bar keeps one width** so no button slides
  under the pointer.
- **The ink colour shows everywhere:** the swatch (violet ring), a badge on Pen and on the dot. White
  and yellow get a `#D6D6E0` ring.
- **Tooltips:** name + shortcut ("Pen · Ctrl+1 (F9)"), after 700 ms. A .NET 8 tooltip stays open
  while the pointer rests on its button.
- 🎥 **Hidden from screen capture** (`OverlayService.HideFromCapture`, `WDA_EXCLUDEFROMCAPTURE`): the
  toolbar, its dot, the mode labels (`ToastWindow`) and **the tooltips**. Omri sees them; OBS,
  screenshots and screen shares don't. **Never the overlay:** its ink is what the video is for. The
  log says `Toolbar: hidden from screen capture` / `Mode label: …` at start and `Tooltip: hidden from
  screen capture` at the first tooltip, or `… FAILED`. A screenshot can never show the toolbar, so
  the only check is OBS's own frame (`GetSourceScreenshot`) compared with drawing off.
  - ⚠️ **A tooltip opens in a window of its own**, which the toolbar's exclusion does not cover. A
    class handler on `ToolTip.OpenedEvent` (App) excludes that window, makes it click-through, and
    only then fades it in (it opens at opacity 0, App.xaml's ToolTip style). **Any new popup (a
    menu, a flyout) needs the same treatment.**
- **✕ stops drawing**, exactly like the mouse button (`OnToolbarToolSelected(None)`). It must never
  quit the app (drawing would be out of reach until a restart). Quitting = the tray's "Quit" only. If
  "SmoothDraw isn't running", check it was quit from the tray, then the install gotchas below.
- **Snapshots:** the pipe's `snapshot` renders the toolbar to `%LOCALAPPDATA%\SmoothDraw\toolbar-*.png`
  (the live window, plus the dot and the bar in several states from a never-shown copy). The only
  way to look at the toolbar.

### The dot, and where the bar opens
- ⚠️ **Both machines hide the dot** (`HideToolbarWhenIdle: true`, Settings below): stopping drawing
  hides the toolbar completely; it shows while drawing, where it was last dragged. The rest of this
  section is how the dot works when it is on (code default false).
- **No hover brings the bar.** Removed 2026-10-24 at Omri's request ("annoying"). Don't bring it back.
- **Mouse button / Ctrl+0** exits drawing and the toolbar shrinks to a **42 px dot** (white, dark
  pencil, ink-colour badge). **Click the dot = pen, drag it = move it.**
- **The bar opens where the dot is:** at the dot's vertical centre, growing toward the middle of that
  screen (dot in the right half: right edges line up, else left edges), kept inside that screen's work
  area. Closing shrinks it back into the dot. **Dragging the bar moves the dot's home** by the same
  rule in reverse.
  - No visible jump: the window goes to opacity 0, swaps dot and bar, is placed only after layout
    (`BeginInvoke`, Loaded priority), then shown. It starts collapsed and invisible in XAML.
  - **Placement is Win32 pixels throughout** (`GetWindowRect`, `MonitorFromRect`, `rcWork`,
    `SetWindowPos(NOSIZE|NOZORDER|NOACTIVATE)`), which agree whatever the DPI mode. WPF's
    `Left/Top` and `SystemParameters.WorkArea` (main screen only) are not used.
  - Dot and bar each have 10 px of transparent room for the shadow (`ShadowRoom`). The hit layer's
    hole and the hover check use what you see (`VisibleScreenRect`), not the room.
  - ⚠️ **Windows' minimum window width (136 px at 100 %) applies while a window is created.** The
    `Loaded` handler runs a second size-to-content pass, which is not held to it.
  - ⚠️ **With `SizeToContent`, WPF raises `Loaded` during `Show`'s first layout, BEFORE
    `SourceInitialized`.** So the toolbar reads its handle lazily (`Hwnd`).
  - Logged: `Toolbar open: dot (…) -> bar (…) (left edges), work (…)` · `Toolbar closed: dot at (…)`
    · `Toolbar moved: bar (…) -> dot home (…)` · `Dot moved to (…)`.
- **`%APPDATA%\SmoothDraw\dot-position.json`** = the dot's home, `{"X","Y","Units":"px"}` (window
  top-left). SmoothAnnotate's old file in WPF units is read once and rewritten in pixels (logged
  `Dot position converted from WPF units`). A place on no screen (`MONITOR_DEFAULTTONULL`) =
  top-centre of the main screen.

## Undo / redo (`Services/DrawingHistory.cs`)
**Commands, not snapshots:** each step keeps the REAL strokes and elements. Laser strokes share
`DrawCanvas.Strokes` and `LaserService` holds references to them, and `_arrowPairs` is keyed by the
elements, so a copy put back would be a stranger to both. At most 100 steps; a new change empties
redo. Undo / Redo dim when there is nothing to do (`Changed`). Toolbar, Ctrl+Z / Ctrl+Y /
Ctrl+Shift+Z (while drawing), pipe `undo` / `redo`.

| What changes | One undo step | Recorded in |
|---|---|---|
| Pen stroke + its shadow | `InkAdded` (both, with indices) | `OnStrokeCollected`, after the shadow `Insert` |
| Highlighter stroke | `InkAdded` | `OnStrokeCollected` |
| Laser | never recorded (it fades) | — |
| Eraser drag (also the pen's eraser end) | `InkRemoved` per stroke, one group per drag | `StrokeErasing` (cancelled for laser strokes); the group closes on `LostMouseCapture` / `LostStylusCapture`, and on any push, undo or tool change |
| Moving / resizing selected ink | `InkTransformed` (points before / after, applied as clones) | `SelectionMoving` / `Moved`, `SelectionResizing` / `Resized` |
| Arrow (line + head), rectangle, circle | `ElementsAdded` | `OnShapeMouseUp`; a preview under 3 px (a click) is dropped |
| Text | `ElementsAdded` | `CommitActiveTextBox` |
| Pasted image | `ElementsAdded` | `PasteImageFromClipboard` |
| Dragging a shape | `ElementsMoved(dx, dy)` | mouse-up, the total from `_dragStartPoint` |
| Clear (toolbar, F10, pipe) | `Cleared` (every non-laser stroke and shape, with indices) | `ClearAllStrokes`; nothing to clear = nothing recorded |

- **Z-order:** removing goes by descending index, putting back by ascending index at
  `Math.Min(i, Count)`. Laser strokes in between can shift something by one: harmless.
- **`_arrowPairs` is never emptied**, not even by Clear, so an arrow brought back still moves as one.
- **Guards:** undo / redo do nothing mid-gesture (mouse or stylus captured) and log it; they clear the
  ink selection first; a stroke is removed only if the canvas still `Contains` it. From the toolbar
  or the pipe an open text box is finished first; the keys leave Ctrl+Z to the text box.
- **Clear with a text box open** finishes the box first (else it stays "active" and keeps the mouse).

## Pen, colour, thickness (`%APPDATA%\SmoothDraw\pen.json`)
- **Thickness** Thin / Medium / Thick = `PenSize` × 0.6 / 1 / 1.8, for the pen and the shapes'
  outline (not highlighter or laser). Medium = `PenSize`.
- **`pen.json`** (`Models/PenState.cs`, `Services/PenStateService.cs`) remembers colour, thickness and
  text size across restarts. Kept apart from `settings.json` on purpose (that file is written by
  hand only). With no pen.json yet, the colour is settings.json's `PenColor` if it is one of the five.

## Structure (`src/SmoothDraw/`)
- `App.xaml(.cs)`: entry point, tray icon, service wiring, the pipe, tooltip capture exclusion,
  taking over from SmoothAnnotate. `AppIdentity.cs`: every outside name (mutex, pipe, AppData
  folders, the old name). `GlobalUsings.cs`: WPF/WinForms type ambiguities.
- `Models/`: `AnnotationSettings` (settings POCO), `AnnotationTool` (tool enum), `InkPalette` (the 5
  colours), `PenState` (pen.json). `Native/`: `User32` / `Kernel32` P/Invoke.
- `Services/`: `KeyboardHookService` (every hotkey; what to keep from other programs),
  `DrawingHistory`, `LaserService` (fade-out), `CursorRingService`, `StopwatchService`,
  `ConfettiService`, `OverlayService` (click-through, z-order, capture exclusion), `PenStateService`,
  `CountdownPlaceService`, `SettingsService`.
- `Views/`: `OverlayWindow` (fullscreen overlay: Board + InkCanvas + ShapeCanvas + CountdownHost +
  ConfettiCanvas; the board's swap; most of the logic), `CountdownHud` (the timer), `ArrowShape`
  (shaft + head, tip on the end point), `PenInkCanvas` (ignores touch near the pen), `ToolbarWindow`
  (toolbar, dot, placement, snapshots), `ToolbarProps`, `Icons.xaml` (Tabler geometries +
  IconTemplate), `ToastWindow` (mode label).

## Key technical patterns
- **Click-through overlay:** `WS_EX_TRANSPARENT` toggled via `SetWindowLong`. The window background
  is always `Transparent`. While drawing, input is caught by `_hitLayer`, an alpha-1 (`#01000000`)
  fill at the bottom of the overlay's Grid (a layered window takes input only where pixels aren't
  fully transparent).
- **Toolbar clickable in draw mode: two mechanisms, both needed.**
  - **Holes in the hit layer** under the toolbar and the OBS remote (`UpdateHitLayer`). **This is
    what makes finger taps work:** a finger doesn't hover, so the timer never sees it coming.
  - **50 ms `DispatcherTimer`** checks `GetCursorPos` and makes the overlay click-through while over
    the toolbar (what you see, `GetScreenBounds`, +12 px) or the remote. It re-cuts the remote's hole
    when the remote moves; the toolbar's follows `LocationChanged` / `SizeChanged` / `IsVisibleChanged`.
- **Toolbar in mouse mode:** `ShowIdleToolbar()`, called by `ExitDrawMode()` (every way out of
  drawing ends there). `MinimalButton_Click` runs `DragMove()`: moved more than 4 px = a drag (place
  saved), else a click (pen). `Bar_MouseLeftButtonDown` works the same way.
- **Single-monitor overlay:** `MonitorFromPoint` + `GetMonitorInfo` puts the overlay on the cursor's
  monitor when drawing starts.
- **WS_EX_NOACTIVATE** on the overlay so the toolbar keeps focus.
- **Arrow pairing:** `_arrowPairs` maps Line↔Polygon so Select moves both.
- **Delegate pinning:** hook delegates are class fields so the GC never collects them.

## DPI
⚠️ **SmoothDraw runs System-DPI-aware, not per-monitor.** The csproj's `ApplicationHighDpiMode` only
feeds WinForms' startup code; a WPF app takes its DPI mode from the manifest, and `app.manifest`
declares none.
- Home PC (main screen 100 %): every Win32 call answers in main-screen pixels. The 150 % left screen
  is stretched by DWM (slightly blurry there); the main screen, where OBS records, is sharp.
- Laptop (one screen, 200 %): System-aware and per-monitor come out the same.
- The code works in either mode (Win32 pixels). Switching to PerMonitorV2 is a separate change:
  `RepositionToCurrentMonitor` converts pixels with the DPI of the screen it is leaving.

## Settings (`%APPDATA%\SmoothDraw\settings.json`)
**Both machines have one written by hand with ONE key: `{"HideToolbarWhenIdle": true}`** (since
2026-10-10; Omri doesn't want the dot on screen). A missing key keeps its code default. **A file the
app itself saved would pin every value**, so never let the app save it; edit by hand. It is read only
at start: after a change, `Start-ScheduledTask SmoothDraw` (stop it first). On the laptop write it
through a one-off scheduled task (the MSIX gotcha below).
- `HideToolbarWhenIdle` (default false = the dot). With it on, drawing starts from F9: the pad's small
  right knob press (davinci-automation CLAUDE.md surface 6), the Start menu / the pen's top button
  (`--toggle`) or F9 itself.
- `CountdownPosition` (a fallback only: `countdown.json` wins), `CountdownSize` (170), `BoardColor`
  (#121417).
- `RingSize`, `RingThickness`, `RingColor`, `RingFill`, `ClickRipple` (the cursor ring above).
- `IgnoreTouchNearPen` (default true), `PassThroughWindowTitle` (Pen and touch below).
- NOT settings: the dot's place (`dot-position.json`), the toolbar's picks (`pen.json`), the timer's
  place (`countdown.json`).
- **Taken over from SmoothAnnotate** (`App.OnStartup`): each of `settings.json` / `dot-position.json`
  that `%APPDATA%\SmoothDraw` lacks is **copied** (never moved) from `%APPDATA%\SmoothAnnotate`
  (logged `Carried over …`). A running `SmoothAnnotate.exe` is stopped at start and again 60 s later
  (logged `Stopped the old SmoothAnnotate (pid …)`). Never two drawing layers on one screen.

## Debug log
`%LOCALAPPDATA%\SmoothDraw\debug.log`, cleared on start. A touch thrown away by palm rejection logs
`touch ignored, pen near (…)`: "my finger doesn't draw" is answered there first.

## Planned (Tier 1, not yet built)
1. **Filled shapes:** Outline / Tinted / Solid fill for Rect + Circle
2. **Export PNG** (Ctrl+E): annotations to the clipboard as a transparent PNG
3. **10 colours:** add Orange, Pink, Purple, Teal, Gray

## Control from other programs (OBS remote, Start menu, pen button, tests)
**The OBS dashboard's remote drives this app** (Omri: "I want it all from there"). Its row ◎ ring ·
✏️ pen · ➡️ arrow · 🔴 laser · ↶ undo · 🧹 clear goes through OBS-dashboard's `smooth.py` (`ring
toggle`, `tool pen|arrow|laser|none`, `undo`, `clear`), also from his phone over Tailscale. An old
SmoothDraw answers `tool …` with `unknown command`, shown as "SmoothDraw needs an update". While
drawing, clicks pass through to the remote window (`PassThroughWindowTitle`).

A **named pipe** (`src/Shared/ControlPipe.cs`, linked into the csproj): one text command in, one JSON
line back, every reply carrying the app's state. Same Windows user only
(`PipeOptions.CurrentUserOnly`). 4 instances, so a few callers at once never see "pipe busy".

- **Pipe:** `\\.\pipe\SmoothDraw.control`.
- **Commands:** `status` · `draw toggle` · `draw off` · `laser toggle` · `clear` · `undo` · `redo` ·
  `color <1-5>` · `snapshot` · `dot <x> <y>` · `board toggle` · `page next` · `page prev` ·
  `countdown <1-600>` · `countdown place <pos>` · `countdown pause` · `countdown resume` ·
  `countdown stop` · `tool <name>` · `ring toggle` · `ring on` · `ring off`.
- **Reply:** `{ok, drawing, tool, color, thickness, undo, redo, board, page, pages, countdown,
  countdown_paused, countdown_place, ring, files}`.
- Reply fields: `undo` / `redo` = steps available; `page` = the board's page on show (0 = no board),
  `pages` = how many; `countdown` = its seconds (0 = none); `countdown_paused` / `ring` = true/false;
  `countdown_place` = where the timer shows; `files` = what `snapshot` wrote, or where `dot` put it.
- `countdown place` takes `top-right` `top-center` `top-left` `bottom-right` `bottom-center`
  `bottom-left` `center`; anything else = `{ok:false, error}`.
- **`draw toggle` is on/off, not F9's cycle:** pen ↔ mouse; from the laser it goes to the pen
  (`ToggleDrawOnOff`). **`laser toggle` = F11** (`ToggleLaser`).
- **`tool <name>`** (`none` `pen` `highlighter` `laser` `eraser` `arrow` `rectangle` `circle` `text`)
  = that toolbar button or Ctrl+0-8 (`SelectToolByNumber`), drawing on; `tool none` = the mouse.
  Lower-case only; else `unknown command`. The reply's `tool` is the enum name (`Arrow`).
- **`color <n>`** sets and remembers the ink colour. **`dot <x> <y>`** = a new dot home in Win32
  pixels, kept inside that screen's work area (for tests).
- Commands run on the UI thread through `ControlPipeServer.OnUi`, which gives up after 1.5 s and
  replies `busy`. The reply is always written, with its own timer.
  ⚠️ **Never one timer across read + handle + write:** a slow UI moment cancels the write and the
  caller gets an EMPTY reply.
- ⚠️ **`ControlPipeServer.Send` runs on the thread pool** (`Task.Run`). Waiting on the async pipe calls
  from the UI thread (the second launch's `OnStartup`) deadlocks: `--toggle` would hang forever.
- ⚠️ **The server never calls `WaitForPipeDrain()`** (it blocks with no timeout; a caller that never
  reads would hold an instance for good). After replying it waits at most 2 s for the hang-up
  (`WaitForHangUpAsync`).
- A `--toggle` launched from an SSH session (session 0) never reaches the desktop copy. A real tap or
  an Interactive scheduled task works. Python's plain `open()` on the pipe works from SSH.
- Warm replies take 1–6 ms; the first calls after a start up to ~1 s, so a caller should wait 1.5 s.
- **Command-line flags:** `--toggle`: running = send `draw toggle` and exit quietly, else start and
  switch on (the Start-menu entry and the pen's top button run this). `--autostart`: running = exit
  quietly (the logon task). A plain second launch shows the "already running" box.

## Pen and touch (the laptop: HP OmniBook Ultra Flip 14)
- **A finger draws, except while the pen is near** (`IgnoreTouchNearPen`, default true,
  `Views/PenInkCanvas.cs`). Omri draws with his hand too: never go back to "only the pen draws".
  `false` = a finger always draws.
  - **"Near"** = the pen is in range over the overlay, or was less than 1 s ago. The pen is watched on
    the whole *window* with `handledEventsToo` (in-range / in-air-move / down / move = here;
    out-of-range / `StylusLeave` = gone), not on the canvas: a shape tool puts ShapeCanvas on top.
    `StylusLeave` counts because over the toolbar the out-of-range event goes to the toolbar.
  - **Each touch is judged once, at touch-down,** and keeps that verdict until lift-off.
  - **Two places make the call and must agree:** the routed stylus events (a touch marked handled =
    no stroke), and the `DynamicRenderer`, which draws live ink on WPF's pen thread BEFORE them
    (without its filter a palm leaves a trail). The pen thread can't query tablets, so touch
    digitizer ids are collected on the UI thread at load; `PenIsNear` reads only plain fields.
  - Shapes, text and select ignore a touch mouse-down while the pen is near (`IsFromTouch`). Only the
    *down* is filtered: filtering an up could leave the mouse captured.
  - Press-and-hold, flicks and tap feedback are off on the overlay (`Stylus.Set…Enabled`).
- **Out of drawing on the laptop:** Esc or the toolbar's mouse (finger taps work, the hit-layer holes).
  HP's top row is media keys, so F9 is most likely **Fn+F9** (not verified: BIOS needs admin).
- **The remote stays clickable while drawing** (`PassThroughWindowTitle`, default
  "מרכז השליטה של OBS"): a hole in the hit layer over that window (`PassThroughRect`, re-cut by the
  50 ms timer), mouse clicks let through over it (`FindWindow`, once a second), and entering draw
  mode raises it above the layer. How it was tested: OBS-dashboard CLAUDE.md.

### Testing without hands (`tools/touch-test.ps1`)
`powershell -ExecutionPolicy Bypass -File tools\touch-test.ps1 [-Steps finger,palm,laser,esc,toolbar,undo,keys]`
drives the **running** SmoothDraw: the pipe, simulated finger strokes (`InjectTouchInput`), a
simulated pen (`CreateSyntheticPointerDevice`) and keys. It checks the screen for red ink and the log
for `touch ignored`, and prints PASS/FAIL. It takes over the screen for ~40 s and presses Esc, so
**tell Omri before running it**.
- It sets the colour to red first (`color 1`) and puts the old one back. It finds the Mouse button by
  AutomationId (`BtnMouse`), never by tooltip text.
- **`undo`**: draw → undo → redo → clear → undo → Ctrl+4 + a finger drag → undo. The first stroke and
  the eraser drag are retried up to 4×.
- **`keys`**: a test window of the script's own holds the keyboard; while drawing, Ctrl+V / Ctrl+Z /
  Ctrl+Y and F11 twice must leave its text alone. It borrows the clipboard and puts it back. `esc`
  uses the same window.
- 🚨 **Never Notepad as the key target, and never type unless the script's own window is in front**
  (`Focus-TestWindow` checks `GetForegroundWindow` and gives up rather than type elsewhere). Starting
  Notepad reopened all of Omri's saved tabs and the keys went into them (2026-09-29, CHANGELOG).
  Never bring a window forward with the "hold Alt" trick (a lone Alt puts WinForms into menu mode, a
  modal loop).
- **palm and grace fail on the home PC** (the synthetic pen never registers as near there): judge
  them on the laptop.
- Quirks it works around (the script's header): until WPF's device list refreshes, injected touches
  are dropped silently (looks like an app bug; the warm-up strokes until one draws); ink over ink
  can't be counted (cleared first); long diagonal injected strokes get lost (keep them short and
  horizontal); a palm with no ink is proof only with a new `touch ignored` line; strokes stay clear
  of the OBS remote's hole.
- When editing it: `$null` to a `string` P/Invoke parameter arrives as `""` (use
  `[NullString]::Value`); `$r` and `$R` are one variable; `$home` is read-only; an exception on a
  background .NET thread kills the script. Keep it ASCII (Windows PowerShell reads a BOM-less
  `.ps1` as ANSI; the remote's Hebrew title is built from code points).

## Build & run
⚠️ **Run `hostname` first.** Both machines have the same `C:\Users\omrii\Projects\SmoothDraw` path
but different .NET SDKs (a session once took the home PC for the laptop and installed into the wrong
folder).
- **Laptop:** .NET 8 SDK 8.0.425 in `C:\Program Files\dotnet`, on PATH.
- **Home PC (DESKTOP-7HQM8GO):** `C:\Program Files\dotnet` is only the runtime. The SDK is portable at
  `E:\tools\dotnet` (8.0.425, not on PATH, nothing on the full C:).

```bash
dotnet build src/SmoothDraw/SmoothDraw.csproj      # laptop
start src/SmoothDraw/bin/Debug/net8.0-windows/SmoothDraw.exe
```
A Debug copy takes over like the installed one (stops SmoothAnnotate; the installed SmoothDraw can't
start while it runs: one copy per session).

### On the home PC (E:\tools\dotnet)
⚠️ **A plain `dotnet restore` fails there** (`Unable to find fallback package folder 'C:\Program Files
(x86)\Microsoft Visual Studio\Shared\NuGetPackages'`, from a leftover machine-wide
`C:\Program Files (x86)\NuGet\Config\Microsoft.VisualStudio.FallbackLocation.config`).
`RestoreFallbackFolders=clear` does NOT help. Use the nuget.config OUTSIDE the repo,
**`E:\tools\nuget-clean.config`** (nuget.org only, `<fallbackPackageFolders><clear />`,
`globalPackagesFolder` = `E:\tools\nuget-packages`), as `restore --configfile`, then `build` /
`publish --no-restore`. Keep the CLI state on E: too: `DOTNET_CLI_HOME=E:\tools\dotnet-home`.
- **Release, one command** (~20 s; `publish.ps1` restores with that config itself):
  `$env:DOTNET_CLI_HOME='E:\tools\dotnet-home'; powershell -File deploy\publish.ps1 -Dotnet E:\tools\dotnet\dotnet.exe -NuGetConfig E:\tools\nuget-clean.config`
- **Debug:** `E:\tools\dotnet\dotnet.exe restore src\SmoothDraw\SmoothDraw.csproj --configfile E:\tools\nuget-clean.config`
  once, then `E:\tools\dotnet\dotnet.exe build src\SmoothDraw\SmoothDraw.csproj --no-restore`.

### ⚠️ From a Claude desktop session: the MSIX redirect
A program started from a Claude Code session in the Claude desktop app runs inside Claude's MSIX
package: files it CREATES under AppData land in `%LOCALAPPDATA%\Packages\Claude_pzs8sxrjxfjjc\LocalCache\…`,
visible only inside the package (a stale copy then hides the real `%LOCALAPPDATA%\SmoothDraw`). So:
judge the installed app, read its real files through a one-off scheduled task (outside the package),
and delete any `LocalCache\Local\SmoothDraw` a Debug run left. Named pipes, processes, E:\ and the
Start menu are not redirected. Install on the laptop and write its settings the same way: through a
one-off scheduled task (smoothzoom CLAUDE.md → Install / update, "From a Claude desktop session").
⚠️ On battery, a task with default settings sits "Queued" and never starts: give it
`-AllowStartIfOnBatteries -DontStopIfGoingOnBatteries`.

## Install / update (laptop + home PC)
Its own folder, logon task and Start-menu entry. The build is self-contained (no .NET needed on the
target). Both machines build and install their own copy in place. **Merging to master deploys
nothing**: there is no CI and no server; a machine gets a new version only when someone runs these
steps on it.

| Machine | Installed at |
|---|---|
| Laptop | `%LOCALAPPDATA%\Programs\SmoothDraw` |
| Home PC (where Omri records) | `E:\apps\SmoothDraw` |

1. **Build**, from the repo root: `powershell -File deploy\publish.ps1` (home PC: the one command in
   "On the home PC"). It writes `publish\SmoothDraw\` (gitignored): `SmoothDraw.exe` + `install.ps1`.
   The icon is `assets/smoothdraw.ico` from `deploy/make_icons.py` (Pillow, run once, committed;
   re-running gives the same bytes). The tray icon is read back from the exe.
2. **Install:** `& publish\SmoothDraw\install.ps1 -Target E:\apps\SmoothDraw` (home PC) or
   `-Target "$env:LOCALAPPDATA\Programs\SmoothDraw"` (laptop).
3. **Pushing a build to the home PC over SSH** (smoothzoom CLAUDE.md → Install / update, step 3, with
   `SmoothDraw` in the names): an upload folder of your own (`E:/apps/SmoothDraw-incoming-<unique>`,
   checked not to exist first); its name never in an inline SSH command as a variable (that once ran
   `Remove-Item -Recurse -Force E:\apps`); `-LiteralPath` deletes only; 8 MB chunks checked by SHA-256
   (a single large `scp` stopped at ~52 MB and still exited 0).

What `deploy\install.ps1` does (safe to re-run; that is how you update):
- Stops SmoothDraw **and SmoothAnnotate**, waits for them to exit, copies the new files (retrying:
  Windows can hold the exe's lock for a moment).
- Registers the logon task **"SmoothDraw"**, running `SmoothDraw.exe --autostart` in the logged-on
  desktop (that is why it also works over SSH, where a direct launch lands in invisible session 0).
  - The task counts as running as long as the app does: no time limit (`ExecutionTimeLimit 0`) and
    both battery flags.
  - **Priority 4 = normal.** Task Scheduler's default (7) starts the app at BelowNormal.
  - A task registered over SSH can't be re-registered from a plain desktop shell ("Access is
    denied"): the script keeps the existing task when it runs the same exe, warns, and goes on. If
    the app is found stopped after an install: `Start-ScheduledTask SmoothDraw`.
- Writes the **Start-menu** entry `Start Menu\Programs\SmoothDraw.lnk` (the exe with `--toggle`), for
  the taskbar or the pen's top button. English name: WScript.Shell reads Hebrew-named `.lnk` files as
  empty.
- Ends by printing `running: SmoothDraw (pid …)`. Then `debug.log` should say `Toolbar: hidden from
  screen capture`.

Gotchas after an install:
- **A NEW build can start late, once, which is NOT a block.** An unknown unsigned exe is held for a
  Defender cloud check (cloud-protection events 2010): it starts 20 s–2 min late and the install
  prints `NOT running yet`. No 3077 = wait and check again. During the hold the file reads "in use",
  so an install run right after another can fail its copy: re-run it.
- **Smart App Control:** OFF on both machines (the laptop since 2026-10-27; it cannot be turned back on
  without resetting Windows). If an install ever will not run, check for a block: CodeIntegrity event
  3077, "did not meet the Enterprise signing level requirements" (`Get-WinEvent -LogName
  'Microsoft-Windows-CodeIntegrity/Operational'`). Never try to get around SAC: the only ways are
  Omri turning it off or a real code-signing certificate.
