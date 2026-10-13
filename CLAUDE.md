# SmoothDraw

Transparent overlay for drawing on the screen while recording tutorials: pen, highlighter,
laser, eraser, shapes, text, pasted images, undo/redo, confetti, a timer. WPF / .NET 8,
Wacom pen and touch screen support. Omri calls it "Smooth Draw".

Until 2026-09-28 it was **SmoothAnnotate**, inside the `smoothzoom` repo next to the
cursor-ring app. It moved here with its history (`git filter-repo`); SmoothZoom stayed
there. Why things are the way they are (dated incidents and decisions): `CHANGELOG.md`,
not auto-loaded. Entries before 2026-09-28 there cover both apps.

## Repo layout
```
src/SmoothDraw/            the app (Structure below)
src/Shared/ControlPipe.cs  the control pipe ("Control from other programs") — the SAME file
                           is in the smoothzoom repo, compiled into SmoothZoom: change both
deploy/                    publish.ps1 · install.ps1 · make_icons.py ("Install / update")
assets/smoothdraw.ico      drawn by deploy/make_icons.py (white pencil on a red tile)
tools/touch-test.ps1       touch / pen / undo / key checks without hands ("Testing without hands")
THIRD-PARTY-NOTICES.md     Tabler Icons (MIT), the toolbar's icons
```

## Hotkeys
⚠️ **Only F9 works in every program (F8 until 2026-10-13, see the table). Every other key below acts only while you are
already drawing** (`KeyboardHookService.IsDrawing`, since 2026-09-28). Caught everywhere,
they were ordinary keys in other programs: Ctrl+1 (browser tab) switched the pen on, and
Ctrl+V with a screenshot on the clipboard pasted it onto the screen. The log showed both on
2026-09-28 (`PEN` with no `F8 pressed` or `control:` line, then `IMAGE PASTED`). Omri: "It
keeps just opening by itself". Esc was already safe (it acts only when it stops something).

⚠️ **A key that acts while drawing is kept from the program underneath** (since
2026-09-28): the key-down AND its key-up (`_swallowUp`; Ctrl / Shift / Alt always go
through, both ways). Before, F11 also put the browser full screen, Ctrl+V pasted there and
Ctrl+1 switched its tab. **Exception: while a text box on the overlay is being typed in**
(`IsTyping`), nothing is kept (the text box has the keyboard), and Ctrl+Z / Ctrl+Y / Ctrl+V
are the text box's own (undo typing, paste text) and do nothing else. The hook only
decides; the work is queued with `Dispatcher.BeginInvoke`, never run inside Windows' hook
call (Esc is the one exception: whether it is kept depends on its answer).

| Key | Action |
|-----|--------|
| F9 | Toggle draw mode, from anywhere (never F8 — that is OBS zoom-to-mouse since 2026-10-13; the two were swapped that day, F8 was this key before). Not kept from other programs |
| F10 | Clear all (undoable) |
| F11 | Laser pointer on/off — pressed again it goes back to the mouse, never to the pen (the screen would stay covered) |
| Esc | Stop drawing (back to the mouse). While typing text, the first Esc finishes the text. Kept from the app underneath only when it stopped something |
| F12 | Timer start/pause (double-tap = reset) |
| Ctrl+Z | Undo |
| Ctrl+Y, Ctrl+Shift+Z | Redo (never with Alt: Ctrl+Alt+Z is SmoothZoom's zoom) |
| Ctrl+0 | Mouse mode (click-through; the toolbar shrinks to its dot — see "The dot" below) |
| Ctrl+1 | Pen |
| Ctrl+2 | Highlighter |
| Ctrl+3 | Laser |
| Ctrl+4 | Eraser |
| Ctrl+5 | Arrow |
| Ctrl+6 | Rectangle |
| Ctrl+7 | Circle |
| Ctrl+8 | Text |
| Ctrl+V | Paste image from clipboard |
| Ctrl+Alt+A | Arrow tool |
| Ctrl+Alt+R | Rectangle tool |
| Ctrl+Alt+O | Circle/Oval tool |
| Ctrl+Alt+X | Text tool |
| Ctrl+Alt+T | Timer show/hide |
| Ctrl+Alt+1-5 | Colors: Red, Blue, Green, White, Yellow |

## The toolbar
- **Light bar** (since 2026-09-28, replacing the dark Excalidraw-style one): white, 1 px
  `#D6D6E0` border, radius 14, soft shadow. Dark `#3B3B46` line icons from **Tabler**
  (MIT), as WPF geometries in `Views/Icons.xaml` (source names and version at its top;
  `THIRD-PARTY-NOTICES.md`). The active tool is a `#E9E8FF` pill with a `#3C34A8` icon;
  hover `#F0F0F5`. Order: grip · Mouse, Select · Pen, Highlighter, Laser, Eraser · Arrow,
  Rectangle, Circle, Text · **options slot** · 5 colours · Undo, Redo · Confetti (amber),
  Clear (the bin turns red under the mouse) · ✕.
  - Why it changed: confetti and delete were invisible. They were emoji `TextBlock`s, and
    WPF draws emoji in one colour: with no Foreground set, black on the near-black bar.
    **Never use emoji for toolbar icons.**
- **Options slot:** thickness while Pen / Arrow / Rectangle / Circle is active, text size
  while Text is, empty otherwise. Click to cycle; no popups. Its buttons are `Hidden`,
  never `Collapsed`, so **the bar keeps one width** and no button slides under the pointer.
- **The ink colour shows everywhere:** the swatch (violet ring), a badge on the Pen button
  and on the dot. White and yellow get a `#D6D6E0` ring so they show on white.
- **Tooltips:** name + shortcut ("Pen · Ctrl+1 (F9)"), after 700 ms.
- 🎥 **Hidden from screen capture** (`OverlayService.HideFromCapture`, `WDA_EXCLUDEFROMCAPTURE`,
  since 2026-09-28): the toolbar, its dot, the mode labels (`ToastWindow`: "PEN",
  "MOUSE"…) and **the tooltips**. Omri sees them; OBS, screenshots and screen shares don't,
  the same as the OBS remote. **Never the overlay:** its ink is what the video is for. The
  debug log says `Toolbar: hidden from screen capture` / `Mode label: …` at start and
  `Tooltip: hidden from screen capture` at the first tooltip, or `… FAILED` if Windows
  refused. **Verified on the home PC 2026-09-28:** the dot was visible at (939,10)–(981,52)
  with affinity `0x11`. OBS's own frame of Display Capture (`GetSourceScreenshot`, that
  monitor, 9,581 colours overall) had only 2 dark colours there and none of the dot's white
  pen. A screenshot of any kind cannot show the dot, so that is the only way to check.
  **Verified again 2026-09-29 with the new bar and a tooltip up** (OBS frames with the bar
  open + the Pen tooltip showing, vs drawing off): in the bar's box 0.00 % of pixels
  differed and none of its colours (active pill, amber confetti) appeared; the tooltip's
  window had affinity `0x11` and its box differed 0.00 % with not one white pixel; the red
  mouse stroke drawn meanwhile was in the frame (1,220 red pixels). ⚠️ A .NET 8 tooltip
  stays open while the pointer rests on its button — it does not close by itself after 5 s.
  - ⚠️ **A tooltip opens in a window of its own**, which the toolbar's exclusion does not
    cover: until 2026-09-28 every tooltip was recorded. Now a class handler on
    `ToolTip.OpenedEvent` (App) excludes that window and makes it click-through
    (`WS_EX_TRANSPARENT`), and only then fades the tooltip in: it opens at opacity 0
    (App.xaml's ToolTip style), so no frame of it is ever recorded. Any new popup (a
    menu, a flyout) needs the same treatment.
- **Close** — ✕ **stops drawing**, exactly like the mouse button
  (`OnToolbarToolSelected(None)`). It must never quit the app: quitting left drawing out of
  reach until a restart (then: the OBS remote's buttons greyed, CHANGELOG). Quitting is the tray icon's
  "Quit" only. If "SmoothDraw isn't running" anyway, check that it was quit from the
  tray, then check Smart App Control (Install / update). Verified with a real click on the
  home PC (2026-09-27, as SmoothAnnotate): the app kept running, drawing turned off, the log said MOUSE.
- **Snapshots:** the pipe's `snapshot` renders the toolbar to PNGs in
  `%LOCALAPPDATA%\SmoothDraw\toolbar-*.png`: the live window as it is, plus the dot and the
  bar in several states from a copy that is never shown. That is how to look at the toolbar
  at all — it is hidden from every kind of screenshot.

### The dot, and where the bar opens
- ⚠️ **Both machines hide the dot since 2026-10-10** (`HideToolbarWhenIdle: true` in their
  settings.json, Settings below): stopping drawing hides the toolbar completely. The rest of
  this section is how the dot works when it is on.
- **Mouse/Pointer** — exits draw mode, and the toolbar shrinks to a **42 px dot**
  (`HideToolbarWhenIdle` false, the default since 2026-09-28): white, a dark pencil, the ink
  colour as a badge. **Click the dot = pen, drag it = move it.** The dot replaced the OBS
  remote's ✏️ button as the mouse's way in. Until the capture exclusion above, the default
  was `true` (hide completely), because a visible toolbar or dot ended up in every video.
- **The bar opens where the dot is** (since 2026-09-28; before, always top-centre): at the
  dot's vertical centre, growing toward the middle of that screen — from a dot in the right
  half its right edge sits on the dot's right edge, otherwise the left edges line up —
  kept inside the work area of the dot's screen. Closing shrinks it back into the dot.
  **Dragging the bar moves the dot's home** by the same rule in reverse, so the dot and the
  bar always share one place.
  - No visible jump: the window goes to opacity 0, swaps dot and bar, and is placed only
    after layout (`BeginInvoke`, Loaded priority), then shown. The window starts collapsed
    and invisible in XAML, so there is no flash of the full bar at start.
  - **Placement is Win32 pixels throughout** — `GetWindowRect`, `MonitorFromRect`,
    `rcWork`, `SetWindowPos(NOSIZE|NOZORDER|NOACTIVATE)`. They all agree with each other
    whatever the DPI mode ("DPI" below). WPF's `Left/Top` (DIPs) and
    `SystemParameters.WorkArea` (the main screen only) are not used.
  - The dot and the bar each have the same 10 px of transparent room around them for the
    shadow (`ShadowRoom`), so lining up the two windows lines up what you see. The hit
    layer's hole and the hover check use what you see (`VisibleScreenRect`), not the room.
  - ⚠️ **Windows' minimum window width (136 px at 100 %) applies while a window is being
    created.** Created at the dot's size, the window came out 136 px wide. The `Loaded`
    handler runs a second size-to-content pass, which is not held to it.
  - ⚠️ **With `SizeToContent`, WPF raises `Loaded` during `Show`'s first layout, BEFORE
    `SourceInitialized`.** So the toolbar reads its window handle lazily (`Hwnd`) instead of
    saving it in `SourceInitialized`; a saved one was still zero when the dot was placed.
  - Each open / close / drag is logged with both rectangles: `Toolbar open: dot (…) -> bar
    (…) (left edges), work (…)` · `Toolbar closed: dot at (…)` · `Toolbar moved: bar (…) ->
    dot home (…)` · `Dot moved to (…)`.
  - **Verified on the home PC 2026-09-28** (pipe `dot X Y`, then open and close; the rects
    read from outside): bottom-left, bottom-right, top-right, left edge of the main screen
    and the left edge of the 150 % screen — every bar on the dot's vertical centre (at the
    very bottom edge the 66 px bar sits 2 px higher than the 62 px dot, to stay inside the
    work area), on the side facing the middle, inside the work area, and the dot back on its
    exact spot after closing.
- **`%APPDATA%\SmoothDraw\dot-position.json`** keeps the dot's home as
  `{"X","Y","Units":"px"}` (the window's top-left). SmoothAnnotate's file, in WPF units of a
  window without shadow room, is read once and rewritten in pixels (logged `Dot position
  converted from WPF units`). A place no longer on any screen
  (`MonitorFromRect(…, MONITOR_DEFAULTTONULL)`) = top-centre of the main screen, as the
  first time.

## Undo / redo (`Services/DrawingHistory.cs`)
**Commands, not snapshots:** each step keeps the REAL strokes and elements. Laser strokes
share `DrawCanvas.Strokes` and `LaserService` holds references to them, and `_arrowPairs`
is keyed by the elements themselves, so a copy put back would be a stranger to both.
At most 100 steps; a new change empties redo. The Undo / Redo buttons dim when there is
nothing to do (`Changed`). Toolbar, Ctrl+Z / Ctrl+Y / Ctrl+Shift+Z (while drawing), pipe
`undo` / `redo`.

| What changes | One undo step | Recorded in |
|---|---|---|
| Pen stroke + its shadow | `InkAdded` (both, with indices) | `OnStrokeCollected`, after the shadow `Insert` |
| Highlighter stroke | `InkAdded` | `OnStrokeCollected` |
| Laser | never recorded (it fades) | — |
| Eraser drag (also the pen's eraser end) | `InkRemoved` per stroke, one group per drag | `StrokeErasing` (cancelled for laser strokes); the group closes on `LostMouseCapture` / `LostStylusCapture`, and on any push, undo or tool change |
| Moving / resizing selected ink | `InkTransformed` (points before / after, applied as clones) | `SelectionMoving` / `Moved`, `SelectionResizing` / `Resized` |
| Arrow (line + head), rectangle, circle | `ElementsAdded` | `OnShapeMouseUp`; a preview under 3 px (a click) is dropped, not recorded |
| Text | `ElementsAdded` | `CommitActiveTextBox` |
| Pasted image | `ElementsAdded` | `PasteImageFromClipboard` |
| Dragging a shape | `ElementsMoved(dx, dy)` | mouse-up, the total from `_dragStartPoint` |
| Clear (toolbar, F10, pipe) | `Cleared` (every non-laser stroke and every shape, with indices) | `ClearAllStrokes`; nothing to clear = nothing recorded |

- **Z-order:** removing goes by descending index, putting back by ascending index at
  `Math.Min(i, Count)`. Laser strokes in between can put something one laser stroke off —
  harmless, they fade.
- **`_arrowPairs` is never emptied**, not even by Clear, so an arrow brought back by undo
  still moves as one piece.
- **Guards:** undo / redo do nothing mid-gesture (a stroke, shape or drag under way: mouse
  or stylus captured) and log it; they clear the ink selection first; a stroke is removed
  only if the canvas still `Contains` it. From the toolbar or the pipe, an open text box is
  finished first; the keys leave Ctrl+Z to the text box.
- **Clear with a text box open** finishes the box properly first. Before 2026-09-28 it was
  removed with the rest but stayed "active" and kept the mouse.
- **Verified on the home PC 2026-09-29** (`touch-test.ps1 -Steps undo,keys`, installed
  build): draw → undo (ink gone) → redo (back) → clear → undo (back) → Ctrl+4 + an eraser
  drag (gone) → undo (back), all PASS; and while drawing, Ctrl+V / Ctrl+Z / Ctrl+Y and F11
  twice left a text box underneath untouched. (By accident the same held for Windows 11
  Notepad underneath, twice: its text stayed as typed, nothing pasted or undone.)

## Pen, colour, thickness (`%APPDATA%\SmoothDraw\pen.json`)
- **Thickness** Thin / Medium / Thick = `PenSize` × 0.6 / 1 / 1.8, for the pen stroke and
  the shapes' outline (not the highlighter or the laser). Medium = `PenSize` as before, so
  a `PenSize` pinned in settings.json keeps meaning the same.
- **`pen.json`** (`Models/PenState.cs`, `Services/PenStateService.cs`) remembers the colour,
  the thickness and the text size across restarts; the toolbar starts on them (before
  2026-09-28 it always started red). Kept apart from `settings.json` on purpose: that file
  is only ever written by hand, because a saved file pins every value (Settings below).
  With no pen.json yet, the colour is settings.json's `PenColor` if it is one of the five.

## Structure
```
src/SmoothDraw/
├── App.xaml(.cs)              # Entry point, tray icon, service wiring, pipe, tooltip capture exclusion,
│                              #   taking over from SmoothAnnotate
├── AppIdentity.cs             # Every outside name: mutex, pipe, %APPDATA% / %LOCALAPPDATA% folders, old name
├── GlobalUsings.cs            # Resolves WPF/WinForms type ambiguities
├── Models/
│   ├── AnnotationSettings.cs  # Settings POCO
│   ├── AnnotationTool.cs      # Tool enum (None/Pen/Highlighter/Eraser/Laser/Arrow/Rectangle/Circle/Text/Select)
│   ├── InkPalette.cs          # The 5 colours
│   └── PenState.cs            # pen.json: colour, thickness, text size
├── Native/
│   ├── User32.cs              # P/Invoke: hooks, window styles, monitors, SetWindowPos, DPI
│   └── Kernel32.cs            # P/Invoke: GetModuleHandle
├── Services/
│   ├── DrawingHistory.cs      # Undo / redo: the history and its steps
│   ├── KeyboardHookService.cs # F9, F10-F12, Esc, Ctrl+0-8, Ctrl+V, Ctrl+Z/Y, Ctrl+Alt combos; what to keep from other programs
│   ├── LaserService.cs        # Laser fade-out timer (single-stroke approach)
│   ├── StopwatchService.cs    # Timer with double-tap reset
│   ├── ConfettiService.cs     # Particle physics confetti
│   ├── OverlayService.cs      # Win32 click-through toggling, z-order, capture exclusion
│   ├── PenStateService.cs     # pen.json
│   └── SettingsService.cs     # JSON persistence
└── Views/
    ├── OverlayWindow.xaml(.cs)  # Fullscreen transparent overlay (InkCanvas + ShapeCanvas + ConfettiCanvas)
    ├── PenInkCanvas.cs          # InkCanvas that ignores touches while the pen is near (IgnoreTouchNearPen)
    ├── ToolbarWindow.xaml(.cs)  # The light toolbar and its dot; placement; snapshots
    ├── ToolbarProps.cs          # IsActive, for the button styles
    ├── Icons.xaml               # Tabler icons as geometries + IconTemplate
    └── ToastWindow.xaml(.cs)    # Mode indicator popup
```

## Key Technical Patterns
- **Click-through overlay:** `WS_EX_TRANSPARENT` toggled via Win32 `SetWindowLong`. The window's background is always `Transparent`. While drawing, input is caught by `_hitLayer`, an alpha-1 (`#01000000`) fill at the bottom of the overlay's Grid. A layered window takes input only where its pixels aren't fully transparent.
- **Toolbar clickable in draw mode — two mechanisms, both needed:**
  - **Holes in the hit layer** under the toolbar and the OBS remote (`UpdateHitLayer`). A tap there falls through the overlay to the window below. **This is what makes finger taps work:** a finger doesn't hover, so the timer below never sees it coming — without the holes a tap on the toolbar lands on the canvas.
  - **50ms `DispatcherTimer`** checks the cursor position via `GetCursorPos` and temporarily sets the overlay click-through while hovering over the toolbar (what you see of it, `GetScreenBounds`, +12 px) or the remote. It also re-cuts the remote's hole when the remote moves; the toolbar's hole follows its `LocationChanged` / `SizeChanged` / `IsVisibleChanged`.
- **Toolbar in mouse mode:** the dot (`ShowIdleToolbar()`, called by `ExitDrawMode()`,
  so every way out of drawing ends there). `MinimalButton_Click` runs `DragMove()`, which
  returns on release: moved more than 4 px = a drag (the place is saved), otherwise a click
  (pen). The bar's own drag (`Bar_MouseLeftButtonDown`) works the same way. With
  `HideToolbarWhenIdle: true` the toolbar hides completely instead.
- **Single-monitor overlay:** `MonitorFromPoint` + `GetMonitorInfo` constrains overlay to cursor's monitor when entering draw mode.
- **WS_EX_NOACTIVATE** on overlay so toolbar keeps focus.
- **Arrow pairing:** `_arrowPairs` dictionary maps Line↔Polygon so Select tool moves both together.
- **Delegate pinning:** Hook delegates stored as class fields to prevent GC collection.

## DPI
⚠️ **SmoothDraw runs System-DPI-aware, not per-monitor** (read 2026-09-28 on the home PC:
every one of its windows reports awareness 1 at 96 DPI; SmoothZoom's too). The csproj's
`ApplicationHighDpiMode` — which the old docs credited with PerMonitorV2 — only feeds
WinForms' startup code; a WPF app takes its DPI mode from the manifest, and `app.manifest`
declares none. What that means:
- On the home PC (main screen 100 %), every Win32 call answers in the main screen's pixels.
  The 150 % left screen is stretched by Windows (DWM), so the overlay's ink and the toolbar
  are slightly blurry there; the main screen, where OBS records, is sharp.
- On the laptop (one screen, 200 %) System-aware and per-monitor come out the same.
- The code is written for either mode (Win32 pixels throughout). Switching to
  PerMonitorV2 (a `<dpiAwareness>` in `app.manifest`) is a separate change: the overlay's
  `RepositionToCurrentMonitor` converts pixels with the DPI of the screen it is leaving,
  which would then be wrong for a screen of other scaling.

## Settings
Stored at `%APPDATA%\SmoothDraw\settings.json`. **Since 2026-10-10 both machines have one,
written by hand with ONE key: `{"HideToolbarWhenIdle": true}`** (Omri: "I don't want the
SmoothDraw circle to be visible all the time on the side of my screen"). Every other value
is still the code default: a missing key keeps its default when the file is read. A file the
app itself saved would pin every value at the time it was saved — re-check before relying on
a default. Before that day neither machine had a file.
`HideToolbarWhenIdle` (code default false: the dot) — see "The dot" above.
- **No dot = drawing starts from F9**: the pad's small right knob press (davinci-automation
  CLAUDE.md surface 6), the Start menu / the pen's top button (`--toggle`), or F9 itself.
  The toolbar shows while drawing and goes away when drawing stops.
- How it was put there: the laptop through a one-off scheduled task (the MSIX redirect in
  "Build & Run"), the home PC over SSH; then SmoothDraw stopped and started again by its
  logon task (`Start-ScheduledTask SmoothDraw`). The setting is read only at start. The log
  still says `Toolbar closed: dot at (…)` at start; what counts is the toolbar window
  (`SmoothDraw Toolbar`) being invisible, which it was on the laptop. `IgnoreTouchNearPen` (default true)
and `PassThroughWindowTitle` — see "Pen and touch" below. The dot's place is NOT a setting:
`dot-position.json` beside it; nor is what you pick on the toolbar: `pen.json`.
- **Taken over from SmoothAnnotate** (`App.OnStartup`): each of `settings.json` /
  `dot-position.json` that `%APPDATA%\SmoothDraw` lacks is **copied** (never moved) from
  `%APPDATA%\SmoothAnnotate`, logged `Carried over … from …`. And a running
  `SmoothAnnotate.exe` is stopped, at start and once more 60 s later (a logon on a
  half-migrated machine can start it after SmoothDraw), logged `Stopped the old
  SmoothAnnotate (pid …)`. Never two drawing layers on one screen.

## Debug Log
Written to `%LOCALAPPDATA%\SmoothDraw\debug.log`. It is cleared on start. A touch that
palm rejection threw away logs `touch ignored, pen near (in range: …, last seen … ms ago)`,
so "my finger doesn't draw" is answered there first.

## Planned (Tier 1 — not yet built)
1. **Filled shapes** — Outline / Tinted / Solid fill mode for Rect + Circle
2. **Export PNG** (Ctrl+E) — renders annotations to clipboard as transparent PNG
3. **10 colors** — adds Orange, Pink, Purple, Teal, Gray to palette

(Undo/Redo and the pen size toggle, items 1–2 of this list until 2026-09-28, are built.)

---

## Control from other programs (Start menu, pen button, tests)
⚠️ **The OBS dashboard no longer controls this app** (2026-09-28, Omri: "I want to have a
separation between the tools"). Its remote's ring / draw / laser / clear row and its
`smooth.py` were removed; the dot, the hotkeys, the tray and the Start menu replace them.
One link is left on purpose: while drawing, SmoothDraw lets clicks through to the
remote window (`PassThroughWindowTitle`, "Pen and touch"), so REC / stop stay pressable.

The app is controlled from outside through a **named pipe** (`src/Shared/ControlPipe.cs`,
linked into the csproj; the same file is in the smoothzoom repo for SmoothZoom's pipe). One
text command goes in and one JSON line comes back, and every reply carries the app's current
state. Only the same Windows user can connect (`PipeOptions.CurrentUserOnly`). There are 4
instances, so a few callers at once never see "pipe busy".

| Pipe | Commands | Reply |
|---|---|---|
| `\\.\pipe\SmoothDraw.control` | `status` · `draw toggle` · `draw off` · `laser toggle` · `clear` · `undo` · `redo` · `color <1-5>` · `snapshot` · `dot <x> <y>` | `{ok, drawing, tool, color, thickness, undo, redo, files}` (`undo` / `redo` = steps available; `files` = what `snapshot` wrote, or where `dot` put the dot) |

SmoothZoom's pipe (`SmoothZoom.control`) is documented in the smoothzoom repo. Until
2026-09-28 this one was `SmoothAnnotate.control`.
- **`draw toggle` is on/off, not F9's cycle.** It switches between pen and mouse. From the
  laser it goes to the pen, because the laser has its own switch (F11; the remote's laser
  button, until 2026-09-28) (`ToggleDrawOnOff`).
- **`laser toggle` = F11**: laser on, or from the laser back to the mouse (`ToggleLaser`).
- **`color <n>`** sets the ink colour (and remembers it, like the toolbar). **`dot <x> <y>`**
  gives the dot a new home in Win32 pixels, kept inside that screen's work area — for
  tests; a person drags it.
- Commands run on the UI thread through `ControlPipeServer.OnUi`, which gives up after
  1.5 s and replies `busy`. The reply is always written, with its own timer.
  ⚠️ **Never one timer across read + handle + write:** a slow UI moment cancels the write
  and the caller gets an EMPTY reply.
- ⚠️ **`ControlPipeServer.Send` runs on the thread pool** (`Task.Run`). Waiting on the async
  pipe calls straight from the second launch's `OnStartup` (the UI thread) deadlocks: their
  continuations queue for that same waiting thread, so the `--toggle` copy delivers its
  command and then hangs forever.
- ⚠️ **The server never calls `WaitForPipeDrain()`.** It blocks a thread with no timeout,
  so a caller that never reads holds an instance for good. After replying, it waits at
  most 2 s for the caller to hang up (`WaitForHangUpAsync`).
- A `--toggle` launched from an SSH session (session 0) exits without reaching the
  running copy in the desktop session. A real tap, or a test through an Interactive
  scheduled task, works. Python's plain `open()` on the pipe does work from SSH.
- **Measured 2026-09-27:** warm replies take 1–6 ms. The first calls after an app starts
  took up to ~1 s, so a caller should wait 1.5 s (the removed `smooth.TIMEOUT` did).
- **Command-line flags:**
  - `--toggle`: if the app is already running, send it `draw toggle` over the pipe and
    exit quietly. Otherwise start and switch on. This is what the Start-menu entry
    `SmoothDraw` and the pen's top button run.
  - `--autostart`: already running = exit quietly (the logon task).
  - A plain second launch still shows the "already running" box.

## Pen and touch (the laptop: HP OmniBook Ultra Flip 14, touch screen + pen)
- **A finger draws, except while the pen is near** (`IgnoreTouchNearPen`, default true,
  `Views/PenInkCanvas.cs`). Omri's choice: he draws with his hand too, so don't go back to
  "only the pen draws" (CHANGELOG). `false` = a finger always draws.
  - **"Near"** = the pen is in range over the overlay, or was less than 1 s ago (a palm lifts
    a moment after the pen). The pen is watched on the whole *window*, with
    `handledEventsToo`: in-range / in-air-move / down / move = here; out-of-range /
    `StylusLeave` = gone. It isn't watched on the canvas alone, because a shape tool puts
    ShapeCanvas on top and the pen's events then never reach the canvas. `StylusLeave` counts
    as gone because over the toolbar the pen's out-of-range event goes to the toolbar.
  - **Each touch is judged once, at touch-down, and keeps that verdict until lift-off.** A
    palm stays ignored even if the pen leaves mid-contact, and a finger stroke never breaks
    off halfway.
  - **Two places make that call and must agree:**
    - The routed stylus events: a touch is marked handled, so no stroke is collected.
    - The `DynamicRenderer`: it draws live ink on WPF's pen thread BEFORE those events.
      Without its filter a palm would leave a trail that vanishes on lift-off. The pen
      thread can't query tablets, so the touch digitizers' ids are collected on the UI thread
      at load. `PenIsNear` reads only plain fields, so the pen thread can ask it too.
  - Shapes, text and select ignore a mouse-down that came from a touch while the pen is
    near (`IsFromTouch`). Only the *down* is filtered: moves and ups act only on a shape or
    drag that a down started, and filtering an up could leave the mouse captured.
  - Press-and-hold (right-click ring), flicks (a quick stroke would become "back") and tap
    feedback circles are switched off on the overlay (`Stylus.Set…Enabled`).
- **Getting out of drawing on the laptop:** Esc or the toolbar's mouse button.
  Finger taps work on the toolbar thanks to the hit-layer holes (Key Technical Patterns).
  F9/F11 need Fn there (see the last bullet).
- **The remote stays clickable while drawing** (`PassThroughWindowTitle`, default
  "מרכז השליטה של OBS"). The full-screen drawing layer would otherwise cover it, including
  REC / stop (until 2026-09-28 also its "stop drawing" button). The hit layer has a hole over that window (`PassThroughRect`,
  re-cut by the 50 ms timer when the remote moves). The same timer also lets mouse clicks
  through over it (`FindWindow`, looked up once a second), and entering draw mode raises it
  above the layer. Verified with a real mouse click on the home PC (2026-09-27): ✏️ on the
  remote switched drawing off through the layer. How: OBS-dashboard CLAUDE.md → "How the row
  was tested".
- HP laptops ship with the top row as media keys (F1–F12 need Fn), so on the laptop F9 is
  most likely **Fn+F9**. Not verified on this one: its BIOS "action keys" setting needs admin
  to read (2026-09-27). Either way the laptop is meant to be driven from the dot, the
  Start menu and the pen.

### Testing without hands (`tools/touch-test.ps1`)
`powershell -ExecutionPolicy Bypass -File tools\touch-test.ps1 [-Steps finger,palm,laser,esc,toolbar,undo,keys]`
drives the **running** SmoothDraw. It uses the control pipe, simulated finger strokes
(`InjectTouchInput`), a simulated pen (`CreateSyntheticPointerDevice`) and simulated keys. It
checks the screen for red ink and the log for `touch ignored`, then prints PASS/FAIL per
check. It takes over the screen for ~40 s and presses Esc, so **tell Omri before running it**.
- It sets the colour to red first (`color 1`: the app remembers its colour, and the checks
  count red pixels) and puts the old one back at the end. It finds the toolbar's Mouse
  button by its AutomationId (`BtnMouse`), never by its tooltip's words.
- **`undo`**: draw → undo (no red) → redo (red) → clear → undo (red) → Ctrl+4 (eraser) +
  a finger drag along the stroke (no red) → undo (red). The first stroke and the eraser
  drag are retried (up to 4×): a lost injected touch would otherwise fail every check after it.
- **`keys`**: a small test window of the script's own holds the keyboard. While drawing,
  Ctrl+V / Ctrl+Z / Ctrl+Y and F11 twice must leave its text alone. It borrows the
  clipboard and puts it back. `esc` uses the same window.
- 🚨 **Never Notepad as the key target, and never type unless the script's own window is
  in front** (`Focus-TestWindow` clicks it and checks `GetForegroundWindow`; it gives up
  rather than type elsewhere). On 2026-09-29 the old `esc` step's `Start-Process notepad`
  reopened Windows 11 Notepad with **all 53 of Omri's last-session tabs** (9 with unsaved
  edits); the process it got back was only a launcher, so `Stop-Process` closed nothing;
  and the keys step then typed into Notepad because its own window never came to the
  front. The test tab was emptied and closed by hand; Notepad keeps typed text across
  restarts. Also never bring a window forward with the "hold Alt" trick: a lone Alt tap
  puts a WinForms window into its menu mode, a modal loop, and the script's `DoEvents`
  never came back.
- **Results on the home PC 2026-09-29:** finger, pen-gone, laser, esc, toolbar, undo, redo,
  clear-undo, erase-undo and keys PASS. **palm and grace FAIL there, for the old
  SmoothAnnotate (built from the pre-split commit) exactly as for SmoothDraw**: the
  synthetic pen never registers as near, although this PC does have pen digitizers
  (`SM_DIGITIZER` 0x8C, "HID-compliant pen"). So those two are judged on the laptop, where
  all 7 original checks passed on 2026-09-27; the palm code itself only changed its name.
The script works around each quirk below:
- **WPF sees neither the injected-touch device nor the synthetic pen until its device list
  refreshes**, a few seconds after the synthetic pen is created. Until then every injected
  touch is dropped silently: no ink and no log line, which looks exactly like a bug in the
  app. The warm-up keeps stroking until one draws.
- **Ink over existing ink can't be measured** by counting red pixels, so the canvas is
  cleared before every measured stroke.
- **Long, fast, diagonal injected strokes were sometimes lost** before reaching WPF. It was
  never reproducible with short horizontal ones, and the mouse drew everywhere, so this is
  the injection, not the app. All strokes in the script are short and horizontal.
- **A palm with no ink isn't proof on its own**: the injected touch can also be dropped
  upstream. The script says which happened ("the app ignored it" = a new `touch ignored`
  line).
- The OBS remote is excluded from screen capture: screenshots never show it, but it is
  there and it is a hole in the drawing layer. The script keeps strokes clear of it.
- PowerShell traps hit on the way: `$null` passed to a `string` P/Invoke parameter arrives
  as `""` (use `[NullString]::Value`). `$r` and `$R` are the same variable. `$home` is
  PowerShell's own (read-only). An exception on a background .NET thread kills the whole
  script, results included.
- Windows PowerShell reads a BOM-less `.ps1` as ANSI: keep the script ASCII (the remote's
  Hebrew title is built from code points).

---

## Build & Run

```bash
# Requires the .NET 8 SDK, 8.0.425 on both machines:
# - laptop: C:\Program Files\dotnet (winget, 2026-09-27). A shell opened before that
#   install lacks it on PATH: open a new one.
# - home PC: portable, E:\tools\dotnet (dotnet-install.ps1 -Version 8.0.425 -NoPath,
#   2026-09-28; no admin, nothing on the full C:). Not on PATH — see "On the home PC" below.

dotnet build src/SmoothDraw/SmoothDraw.csproj
start src/SmoothDraw/bin/Debug/net8.0-windows/SmoothDraw.exe
```
A Debug copy takes over like the installed one: it stops a running SmoothAnnotate, and
while it runs, the installed SmoothDraw can't start (one copy per session).

⚠️ **From a Claude Code session in the Claude desktop app, a program you start runs inside
Claude's MSIX package**, and the files it CREATES under AppData land in
`%LOCALAPPDATA%\Packages\Claude_pzs8sxrjxfjjc\LocalCache\…`, visible only to programs in
the package. Seen 2026-09-28/29: a Debug SmoothDraw started from the session wrote its
`debug.log` and snapshots there; the session then kept reading that stale copy laid over
the real `%LOCALAPPDATA%\SmoothDraw`, while the installed app (started by Task Scheduler,
outside the package) wrote the real one — it looked as if the installed app logged nothing.
So: judge the installed app, read its real files through a one-off scheduled task (it runs
outside the package), and delete any `LocalCache\Local\SmoothDraw` a Debug run left.
Named pipes, processes, E:\ and the Start menu were not redirected.
The same goes for installing on the laptop: run `install.ps1` through a one-off scheduled
task, as on 2026-09-29 (smoothzoom CLAUDE.md → Install / update, "From a Claude desktop
session"). SmoothZoom's laptop settings never reached the real app this way: from 2026-09-27 to
2026-09-29 they existed only in Claude's hidden copy.

### On the home PC (E:\tools\dotnet)
⚠️ **A plain `dotnet restore` fails there**: `Unable to find fallback package folder
'C:\Program Files (x86)\Microsoft Visual Studio\Shared\NuGetPackages'`. A leftover
machine-wide `C:\Program Files (x86)\NuGet\Config\Microsoft.VisualStudio.FallbackLocation.config`
names that missing folder. The env var `RestoreFallbackFolders=clear` did NOT help. What
works: a nuget.config OUTSIDE the repo — **`E:\tools\nuget-clean.config`** (since
2026-09-28: packageSources = nuget.org only, `<fallbackPackageFolders><clear /></fallbackPackageFolders>`,
and `globalPackagesFolder` = `E:\tools\nuget-packages`, so packages stay off the full C:) —
passed as `restore --configfile`, then `build` / `publish --no-restore`. Keep the CLI's own
state on E: as well: `DOTNET_CLI_HOME=E:\tools\dotnet-home`.
- **Release, one command** (`publish.ps1` does the restore with that config itself):
  `$env:DOTNET_CLI_HOME='E:\tools\dotnet-home'; powershell -File deploy\publish.ps1 -Dotnet E:\tools\dotnet\dotnet.exe -NuGetConfig E:\tools\nuget-clean.config`
  (~20 s, 2026-09-28).
- **Debug:** `E:\tools\dotnet\dotnet.exe restore src\SmoothDraw\SmoothDraw.csproj --configfile E:\tools\nuget-clean.config`
  once, then `E:\tools\dotnet\dotnet.exe build src\SmoothDraw\SmoothDraw.csproj --no-restore`.

## Key Technical Details
- **No hotkey conflicts:** SmoothZoom uses Ctrl+Alt, SmoothDraw uses F-keys + Ctrl+number (different patterns).
  ⚠️ **F8 belongs to OBS** (zoom-to-mouse, OBS-dashboard repo; F9 until 2026-10-13, when Omri
  swapped the two so the zoom sits beside F7 = the camera full / small) — never bind it here. F9 is
  passed on to other programs, so a key shared with another app fires both at once. F7 is the
  OBS dashboard's too.

## Install / update (laptop + home PC)
Its own install, fully apart from SmoothZoom's (Omri: "a separation between the tools"):
its own folder, logon task and Start-menu entry. The build is self-contained — no .NET
needed on the target. Both machines can build and install their own copy in place.

| Machine | Installed at |
|---|---|
| Laptop | `%LOCALAPPDATA%\Programs\SmoothDraw` |
| Home PC (where Omri records) | `E:\apps\SmoothDraw` |

1. **Build**, from the repo root: `powershell -File deploy\publish.ps1` (the home PC:
   the one command in "On the home PC"). It writes `publish\SmoothDraw\` (gitignored):
   `SmoothDraw.exe` + `install.ps1`. The exe icon is `assets/smoothdraw.ico`, drawn by
   `deploy/make_icons.py` (Pillow, run once, committed; re-running it gives the same
   bytes): a pencil on a red tile. The tray icon is the same icon, read back from the exe.
2. **Install:** `& publish\SmoothDraw\install.ps1 -Target E:\apps\SmoothDraw` (home PC) or
   `-Target "$env:LOCALAPPDATA\Programs\SmoothDraw"` (laptop).
3. **Pushing a build to the home PC over SSH** instead: the same rules as SmoothZoom's
   (smoothzoom CLAUDE.md → Install / update, step 3), with `SmoothDraw` in the names —
   an upload folder of your own (`E:/apps/SmoothDraw-incoming-<unique>`, checked not to
   exist first), its name never in an inline SSH command as a variable (on 2026-09-27 that
   ran `Remove-Item -Recurse -Force E:\apps`), `-LiteralPath` deletes only, and 8 MB chunks
   checked by SHA-256 (a single large `scp` stopped at ~52 MB twice and still exited 0).

What `deploy\install.ps1` does (safe to re-run; that is how you update):
- Stops SmoothDraw **and SmoothAnnotate**, waits for them to exit, and copies the new files
  in. Windows can hold an exe's file lock for a moment after the process ends, so the copy
  retries.
- Registers the logon task **"SmoothDraw"**, which runs `SmoothDraw.exe --autostart`
  directly (no `start.vbs`). The task starts it in the logged-on desktop; that is why it
  also works over SSH, where a process launched directly would run in invisible session 0.
  - The task then counts as **running for as long as the app does**, so its settings are
    essential: no time limit (`ExecutionTimeLimit 0`) and both battery flags.
  - **Priority 4 = normal.** Task Scheduler's default (7) starts the app at BelowNormal —
    SmoothZoom, started through its task, ran BelowNormal on 2026-09-28.
  - A task registered over SSH can't be re-registered from a plain desktop shell
    ("Access is denied", SmoothZoom's task, 2026-09-28): the script then keeps the existing
    task when it runs the same exe, warns, and goes on. If the app is ever found stopped
    after an install, `Start-ScheduledTask SmoothDraw`.
- Writes the **Start-menu** entry `Start Menu\Programs\SmoothDraw.lnk`: the exe with
  `--toggle`, so a tap switches drawing on and off. It can be pinned to the taskbar or
  picked for the pen's top button. English name: WScript.Shell reads Hebrew-named `.lnk`
  files as empty. ⚠️ A pin or pen button that pointed at SmoothAnnotate's old entry
  ("Draw - SmoothAnnotate", removed by smoothzoom's installer) must be re-picked.
- Ends by printing `running: SmoothDraw (pid …)`. After that, `debug.log` should say
  `Toolbar: hidden from screen capture`, and on a machine coming from SmoothAnnotate,
  `Carried over …` and/or `Stopped the old SmoothAnnotate`.
- **Installed on the home PC 2026-09-29 15:05** (build of 79948af): `running: SmoothDraw`,
  at Normal priority; the real log said `Carried over dot-position.json …` and `Dot position
  converted from WPF units (10,975) to pixels (0,965)`, so the dot came up on its old spot
  (bottom-left); task "SmoothDraw" at logon with priority 4; the Start-menu entry switched
  drawing on and off. Then smoothzoom's cleanup removed `E:\apps\SmoothTools\SmoothAnnotate`
  and "Draw - SmoothAnnotate".
- **Installed on the laptop 2026-09-29 18:44** (build of 1092023), through a one-off
  scheduled task (Build & Run above): `running: SmoothDraw`, Normal priority, no
  CodeIntegrity 3077; the log said `Toolbar: hidden from screen capture`. Nothing was
  carried over: the laptop had no `%APPDATA%\SmoothAnnotate` (it ran on defaults), so the
  dot started top-centre. Then smoothzoom's install removed SmoothAnnotate's folder and
  "Draw - SmoothAnnotate".
- **Order on a machine that has SmoothAnnotate:** install SmoothDraw first and check it
  (running, the log lines above, the old dot place), and only then install smoothzoom's
  cleanup, which deletes the old `SmoothAnnotate\` folder and its Start-menu entry. If
  SmoothDraw is blocked (below), start the old SmoothAnnotate again and remove the task
  `SmoothDraw`: nothing is lost, its settings were only copied.
- 🚨 **The laptop has Smart App Control ON** (last read 2026-09-29 ~18:40: `Get-MpComputerStatus` →
  `SmartAppControlState: On`; the home PC is Off. Omri was given the steps to turn it off
  himself, since it's a security setting; re-read it before relying on this line). SAC blocks
  unsigned programs it has no good cloud verdict for. **Every new build is judged again, and
  the verdict varies** — a build can be blocked and the next one let through (CHANGELOG). So
  after any update, check that the app is running on the laptop. Last seen: the first
  `SmoothDraw.exe` on the laptop (build of 1092023) was let through, running within 10 s (2026-09-29 18:44). A block is CodeIntegrity
  event 3077, "did not meet the Enterprise signing level requirements". (Launched by the old
  `start.vbs`, the block showed up as a "Windows Script Host" error box.) The check: `Get-WinEvent -LogName
  'Microsoft-Windows-CodeIntegrity/Operational'`, event 3077 naming the exe. Never try to
  get around SAC. The only ways forward are Omri turning it off, or a real code-signing
  certificate. `SmoothDraw.exe` is a new name, so it gets a verdict of its own.
- ⚠️ **A NEW build can start late, once — which is NOT a block.** On its first run an unknown
  unsigned exe is held for a cloud check (Defender cloud-protection events 2010 at that
  moment): it starts 20 s–2 min late, and the install prints `NOT running yet`. Tell the two
  apart before waiting: a **block** is a CodeIntegrity 3077 naming the exe (above), and
  waiting never ends it. No 3077 = wait and check again. During the hold the file also reads
  as "in use", so an install run straight after another can fail its copy; re-run it.
