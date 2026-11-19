# SmoothDraw — history

**Not auto-loaded.** Split out of `CLAUDE.md` on 2026-09-27 (`/compress-docs`). `CLAUDE.md`
keeps the rules; this file keeps why they exist — the dated incidents and decisions behind
them. Newest first. Git history has the full detail. Everything before 2026-09-28 happened
in the smoothzoom repo, where this app was SmoothAnnotate next to SmoothZoom, so those
entries cover both apps.

## 2026-11-19 — a press on the OBS remote draws on the recorded screen
- Omri: on the home PC the remote sits on the left screen, which is not recorded. A press on the
  laser (or pen, arrow, ⏳) left the cursor there, so the overlay opened on that screen.
- `DrawingMonitor`: when the cursor is on the remote's screen, the overlay goes to the main screen
  (else any other one). One screen (the laptop), or the remote closed or minimized: as before.
- Checked on the home PC: cursor on the remote, `tool laser` → overlay (0,0)-(1920,1080), the main
  screen; log `Overlay: the cursor is on the remote's screen, so the other screen`. Installed there;
  the old exe is kept in `E:pps\SmoothDraw-backup-2026-11-19`. The laptop (one screen) needs no update.

## 2026-10-28 — the thinking timer's place from the OBS remote, and it follows the take
- Omri, in the OBS-dashboard session: pick WHERE the timer shows from the remote's ⚙️, and make it
  smart: when the take is paused the timer pauses and goes on with it; when the take is restarted
  (🔄), cancelled (🗑) or a part redone (↩️) the timer goes. He approved the plan.
- New pipe commands for the dashboard: `countdown place <pos>` (adds `bottom-center`), `countdown
  pause` / `resume` / `stop`; every reply gains `countdown_paused` and `countdown_place`.
- The place lives in `countdown.json`, not settings.json: a saved settings.json would pin every
  value (the same reason as pen.json). settings.json's `CountdownPosition` stays the fallback.
- Paused = dimmed to 0.45 with a ⏸ drawn as two bars (no emoji: WPF draws them in one colour).
- Built on the laptop (Release, 0 warnings). Installed 2026-10-28 (build of 105443d, Omri: "Merge
  and deploy"), neither machine recording: the home PC over SSH (built there, 17:44,
  `running: SmoothDraw`), the laptop through a one-off scheduled task (19:30; the first try was cut
  off mid-run and left the old build running, the second went through). Each `status` reply carried
  `"countdown_paused":false,"countdown_place":"top-right"`; both at Normal priority, no
  CodeIntegrity 3077 on the laptop. ⏳ Not yet seen: pause / place on the screen itself.

## 2026-10-27 — the thinking timer reads m:ss from a minute up
- The OBS remote's ⏳ took a custom length up to 10 minutes the same day (OBS-dashboard), and the
  ring showed 599. Omri: "I don't want seconds! I want a minutes display too". `CountdownHud`
  now writes `m:ss` from 60 s up, with a smaller font for 4 and 5 characters (0.27 / 0.22 of the
  disc) so 10:00 stays inside the ring. Under a minute nothing changed.
- Home PC: built and installed 19:55 (`E:\apps\SmoothDraw`); the pipe's `countdown 600` and
  `countdown 90` started and stopped (debug.log). ⏳ Not seen on its screen (a GDI grab of it
  came back blank: nobody at it).
- Laptop: **Smart App Control blocked the build** (CodeIntegrity 3077, 19:51), so SmoothDraw
  was not running there for ~2 minutes; the 2026-10-18 build was put back (running 19:53). The
  laptop showed seconds until Omri turned SAC off (20:49); the 64764ef build then installed
  (20:50), and a 10-minute timer read **10:00** inside the ring on the laptop's screen.

## 2026-10-24 — no more bar on hover; a dragged bar stays where it was put
- Omri: "The bar is always appearing at the top, and if I drag it down, it always reverts to the
  top", and "it's annoying that when I hover my mouse, I suddenly see the bar … I want to have it
  when I want it". The home PC's log showed both came from 2026-10-18's top edge: each time the bar
  came from a `Toolbar peek`; dragged down (`Toolbar moved … dot home (-2530,685)`), it was still a
  peek because drawing was off, so a second later `Toolbar peek ended` hid it and the next hover
  opened it at the top again. The edge was removed altogether (`OnEdgeCheck`, `PeekAtTop` /
  `KeepPeek` / `EndPeek`). The bar shows while drawing (F9, the pad, the pen button, the Start
  menu) and opens where it was last dragged: the drag saves `dot-position.json`, as before.
- Installed on the home PC 2026-10-24 16:24 (build of fa4b35b, `E:\apps\SmoothDraw`, Normal
  priority; it started with the dot home on the last dragged place, (-2530,685)). The laptop was
  offline: not installed there yet.

## 2026-10-18 (night, later) — the cursor ring moves in, ⏳ on the toolbar, SmoothZoom retired
- Omri: "I don't use SmoothZoom; the only thing I want from it is the ring. Move it into SmoothDraw
  and we can say goodbye to SmoothZoom", and "a shortcut to the thinking timer … as a button in
  SmoothDraw". The ring was ported as it was (look, ripple, Ctrl+Alt+H, `ring toggle / on / off`),
  without the zoom and without the OBS recording watcher (auto-on had been off since 2026-09-28).
  Its settings fall back to SmoothZoom's settings.json, so each machine's own ring size comes along.
- ⏳ opens 10 / 30 / 60 inside the bar rather than a popup: a popup is a window of its own, which
  needs its own capture exclusion and is no hole in the hit layer, so a finger tap on it would draw.
- SmoothZoom was then removed from the laptop with Omri's OK: its logon task "SmoothTools", its
  folder `%LOCALAPPDATA%\Programs\SmoothTools` and its Start-menu folder. Its
  `%APPDATA%\SmoothZoom\settings.json` was kept (the ring reads its size 60 there). It had not run
  there since Smart App Control blocked it (2026-10-10). The home PC still has it.

## 2026-10-18 (night) — a cleaner arrow point
- Omri: "the arrow's point doesn't look good" (a thick blue arrow). The shaft ran all the way to the
  tip with a flat end as wide as the stroke, while the head narrows to nothing there, so a stub of
  the line poked out past the point and the head looked bent. The head was also a fixed 30–50 DIP
  spike (40° at the tip) whatever the thickness.
- Now (`Views/ArrowShape.cs`): the shaft stops inside the head, where the head is wider than it;
  the head is 5 × the thickness long (at least 18 DIP; a short arrow's at most 60 % of it), 0.8 of
  that wide, with slightly rounded corners, and its rounded tip lands exactly on the end point. The
  preview draws the head while dragging, through the same call. Still a Line + a Polygon, so
  `_arrowPairs`, undo and Select are unchanged. Checked in an offscreen render at thin / medium /
  thick, eight directions and lengths, against the old arrow (which reproduced his screenshot).

## 2026-10-18 (evening) — `tool <name>` on the pipe: the OBS remote drives drawing again
- Omri reversed 2026-09-28's separation: "I want it all from there". OBS-dashboard's remote got a
  row on its main screen (◎ ring, ✏️ pen, ➡️ arrow, 🔴 laser, ↶ undo, 🧹 clear), also from his phone.
  Its `smooth.py` was built and merged first; it needed one tool per button, which `draw toggle`
  (pen ↔ mouse) and `laser toggle` could not give, so `tool <name>` was added here.
- The remote treats `unknown command` as "SmoothDraw needs an update", so an older build fails
  visibly rather than doing the wrong thing.

## 2026-10-18 (later) — the top edge brings the bar, and the board has pages
- Omri: "when I move to the edge of the screen the mouse usually doesn't bring back the toolbar",
  and "I want several screens to go through, like flipping pages". There was no edge feature at
  all: since 2026-10-10 (`HideToolbarWhenIdle`) the bar was gone whenever drawing was off, and only
  F9 / the pad brought it. Asked, he chose: the top edge (over the bar's own side or any edge),
  pages on the board only (not the screen), flipped by keys on the board and arrows on the toolbar
  (not the pad).
- The edge needs a 350 ms rest, not a touch: maximised windows keep their tabs and title bars at
  the very top, and a touch on the way to them would cover them with the bar. Nothing happens with
  a mouse button held (a window dragged up there snaps to full screen).
- No endless blank pages: past the last page is a new one only when the page on show has something.

## 2026-10-18 — the thinking timer (F6) and the board (F5)
- Omri asked for "a timer that shows on the screen and goes away" (the class thinks for 10 seconds)
  and a whiteboard "in the style of Khan Academy or Excalidraw". Asked with mockups, he chose: on the
  real screen over an OBS-only overlay (a Zoom share shows it too), a ring that empties with the number
  inside over a big number or a small corner clock, the board inside SmoothDraw (black) over
  Excalidraw, F6 / F5 (accepting that a browser loses its refresh / address-bar keys), the pad, and no
  button on the OBS remote, which would have tied the dashboard to this app again (2026-09-28's
  separation). On the pad: a new page 4 (pages 2 and 3 were full), with ✏ and 🧹 in its two spare keys.
- The existing F12 stopwatch counts up and stays on screen until hidden, so the timer is new
  (`CountdownHud`), not a mode of it.
- 🧹 on the pad sends Ctrl+Shift+F6, not F10: F10 is a drawing-only key, and when not drawing it went
  on to the app in front (a browser's menu).

## 2026-10-13 — drawing moves to F9 (F8 is OBS's zoom now)
- Omri, setting up live editing in OBS (the camera full / small on F7, OBS-dashboard): he wanted
  the zoom to the mouse on F8, beside F7, and chose to swap the two keys. Drawing's toggle is
  `VK_F9` now; F8 belongs to OBS (OBS-dashboard `tools/set_zoom_key.py` moved the zoom's binding
  in every scene collection). The macro pad's small right knob press sends F9 now
  (davinci-automation `generate_deck.py`).
- Why both had to move together: this hook passes the key on, so a key both tools catch fires
  both (the reason drawing left F9 for F8 on 2026-09-26, the other way round).

## 2026-09-29 (evening) — installed on the laptop

The cursor ring came on by itself in a laptop recording: the laptop still ran the
2026-09-27 builds of both apps (smoothzoom's CHANGELOG). Both were updated that evening,
SmoothDraw first. It was installed through a one-off scheduled task, so its files landed
outside the Claude desktop app's package (CLAUDE.md → Build & Run). It ran at once, and
Smart App Control let it through. Nothing to carry over: the laptop's SmoothAnnotate had
never saved a settings file or a dot place. SmoothAnnotate's folder and Start-menu entry
went with smoothzoom's install right after.

The lines this replaced in CLAUDE.md: "⏳ **The laptop still runs SmoothAnnotate** — same
order there (below), and watch Smart App Control."; Settings: "(the home PC had none on
2026-09-28, so code defaults rule there; … — check the laptop's before relying on a
default)"; Smart App Control: "(last read 2026-09-27 15:49: …)" and "Last seen: the ✕-fix
build (5d3f3f4, then still SmoothAnnotate) was let through, and ran within 18 s
(2026-09-27 ~21:50)."

## 2026-09-28/29 — SmoothDraw: its own repo, a toolbar that opens at the dot, undo

Omri calls the drawing app "Smooth Draw" and asked for three things: a folder of its own,
the toolbar opening where the dot is (his dot sits at the bottom; the bar always opened at
the top-centre), and a lighter toolbar — confetti and delete were invisible. Decided with
him the same day: the new name everywhere, the light toolbar he was shown, and, this
round, undo/redo (it also makes Clear undoable, instead of a planned "undo for 5 s after
Clear") and pen thickness.

- **The split.** A fresh clone of smoothzoom through `git filter-repo` (paths of this app,
  `src/Shared`, deploy, tools and the docs; `src/SmoothAnnotate` renamed `src/SmoothDraw`)
  kept 31 commits; published as `omri-il/SmoothDraw` (public, like smoothzoom). One commit
  renamed everything and put every outside name in `AppIdentity.cs`. On first start the
  app copies SmoothAnnotate's settings files (never moves them) and stops a running
  SmoothAnnotate, again after 60 s. Its own install: `E:\apps\SmoothDraw`, logon task
  "SmoothDraw" running the exe directly, Start-menu `SmoothDraw.lnk`.
- **The toolbar.** Rebuilt light, with Tabler line icons. The two invisible buttons were
  emoji `TextBlock`s, which WPF draws in one colour — black, on the near-black bar. It now
  opens at the dot (same vertical centre, growing toward the middle of the screen) and
  closes back into it; dragging the bar moves the dot's home. Two WPF surprises on the way,
  both in CLAUDE.md: the dot's window came out 136 px wide (Windows' minimum width while a
  window is created), and `Loaded` fired before `SourceInitialized`, so the first placement
  ran with no window handle.
- **Found: the app is System-DPI-aware**, not PerMonitorV2 as the docs had said for both
  apps. The placement is written in Win32 pixels, which is right in either mode; on the
  home PC's 150 % left screen the toolbar and ink are stretched by Windows. Not changed
  (CLAUDE.md → DPI).
- **Undo/redo** as commands over the real strokes and elements, recorded where each change
  happens. **Keys used while drawing are now kept from the program underneath** (F11 had
  also put the browser full screen, Ctrl+V pasted there), except while typing in a text
  box. **Tooltips were being recorded** (their own windows, outside the toolbar's capture
  exclusion); now excluded before they fade in.
- **Pen thickness** (Thin / Medium / Thick) and the colour and text size remembered in
  `pen.json` — before, every start began red.
- **The home PC's portable SDK** builds with one command now (`publish.ps1 -Dotnet
  -NuGetConfig`, the config at `E:\tools\nuget-clean.config`).
- **Installed on the home PC 2026-09-29.** The Debug runs had been started from the Claude
  desktop app, whose MSIX package redirected the files they created under AppData into its
  own `LocalCache`; the session then read that stale log over the real one, and the
  installed app looked as if it logged nothing. Read through a one-off scheduled task, the
  real log had the migration lines. The logon task asks for normal priority: SmoothZoom,
  started through its task, was found running BelowNormal.
- **The touch test and Notepad.** Its old `esc` step started Notepad as the key target. On
  Windows 11 that reopened Omri's last Notepad session (53 tabs, 9 with unsaved edits of
  his), `Stop-Process` on the launcher closed nothing, and the new keys step then typed
  "abc" into Notepad twice, because its own window had not come to the front. Nothing of
  his changed; the test tab was emptied and closed, and the Notepad window was left open
  for him to close. The test now uses only a window of its own, types only when that
  window is in front, and never uses the "hold Alt" trick (which froze it: a lone Alt tap
  opens a WinForms window's menu mode). Its palm and grace checks fail on the home PC for
  the old SmoothAnnotate too — the synthetic pen is not seen as near there — so they are
  the laptop's to judge.

The text of `CLAUDE.md` before the split: `git show 49c653b:CLAUDE.md` (smoothzoom's
`e1be3aa`). The lines of it that did not survive word for word into the two new CLAUDE.md
files (reworded, moved, renamed, or about SmoothZoom and trimmed from this repo's copy):

```text
# SmoothZoom + SmoothAnnotate
Two companion WPF desktop tools for video tutorial recording: screen zoom + screen annotation overlay.
src/SmoothAnnotate/    the drawing overlay app (below)
src/Shared/ControlPipe.cs   the control pipe, compiled into both ("Control from other programs")
assets/                smoothzoom.ico · smoothannotate.ico (drawn by deploy/make_icons.py)
tools/touch-test.ps1   touch/pen checks without hands ("Testing touch without hands")
## SmoothAnnotate (`src/SmoothAnnotate/`)
Transparent overlay for screen drawing, shapes, laser pointer, and fun effects. Designed for video tutorials with Wacom stylus support.
| F8 | Toggle draw mode, from anywhere (never F9 — that is OBS zoom-to-mouse) |
| F10 | Clear all |
| Esc | Stop drawing (back to the mouse). While typing text, the first Esc finishes the text. Swallowed only when it stopped something, so it still reaches the app underneath otherwise |
| Ctrl+0 | Mouse mode (click-through; the toolbar shrinks to its dot — see Mouse/Pointer below) |
### Toolbar Features
- **Excalidraw-style horizontal bar** at top-center of screen (draggable)
  since 2026-09-28): the toolbar, its dot and the mode labels (`ToastWindow`: "PEN",
  "MOUSE"…). Omri sees them; OBS, screenshots and screen shares don't, the same as the OBS
  remote. **Never the overlay:** its ink is what the video is for. The debug log says
  `Toolbar: hidden from screen capture` / `Mode label: …` at start, or `… FAILED` if Windows
  (`HideToolbarWhenIdle` false, the default since 2026-09-28): **click the dot = pen, drag
  it = move it**. It remembers its place in `%APPDATA%\SmoothAnnotate\dot-position.json`
  (DIPs; top-center the first time, or when that place is no longer on any screen). The
  full toolbar still opens top-center. The dot replaced the OBS remote's ✏️ button as the
  mouse's way in. Until the capture exclusion above, the default was `true` (hide
  completely), because a visible toolbar or dot ended up in every video.
- **Select/Move** — drag ink strokes and shapes to reposition; arrows move as one piece (line + head)
- **Pen** — pressure-sensitive Wacom support, subtle shadow
- **Highlighter** — semi-transparent yellow, rectangle tip
- **Eraser** — stroke-level removal
- **Laser** — single-stroke fade with glow, configurable fade duration
- **Shapes** — Arrow (sharp pointy head), Rectangle, Circle — all with drop shadows
- **Text** — Hebrew RTL auto-detect, 4 sizes (Small 24 / Medium 32 / Large 48 / XL 72)
- **Color picker** — 5 colors with glow swatches
- **Confetti** — 60-particle burst with physics (gravity, spin, fade)
- **Timer** — Stopwatch HUD, double-tap to reset
- **Close** — ✕ in the toolbar header **stops drawing**, exactly like the mouse button
  home PC (2026-09-27): the app kept running, drawing turned off, the log said MOUSE.
- **Paste image** — Ctrl+V pastes clipboard image as draggable element on overlay
├── App.xaml(.cs)              # Entry point, tray icon, service wiring
│   └── AnnotationTool.cs      # Tool enum (None/Pen/Highlighter/Eraser/Laser/Arrow/Rectangle/Circle/Text/Select)
│   ├── User32.cs              # P/Invoke: hooks, window styles, monitors, SetWindowPos
│   ├── KeyboardHookService.cs # F8, F10-F12, Esc, Ctrl+0-8, Ctrl+V, Ctrl+Alt combos
│   ├── OverlayService.cs      # Win32 click-through toggling, z-order
    ├── ToolbarWindow.xaml(.cs)  # Horizontal dark toolbar (draggable, collapsible to dot)
### Key Technical Patterns
  - **50ms `DispatcherTimer`** checks the cursor position via `GetCursorPos` and temporarily sets the overlay click-through while hovering over the toolbar or remote. DPI-aware using `PresentationSource.TransformToDevice`. It also re-cuts the remote's hole when the remote moves; the toolbar's hole follows its `LocationChanged` / `SizeChanged` / `IsVisibleChanged`.
- **Toolbar in mouse mode:** the 42 px dot (`ShowIdleToolbar()`, called by `ExitDrawMode()`,
  (pen). With `HideToolbarWhenIdle: true` the toolbar hides completely instead.
`HideToolbarWhenIdle` (default false: the dot) — see Mouse/Pointer above. `IgnoreTouchNearPen` (default true)
`dot-position.json` beside it.
### Debug Log
### Planned (Tier 1 — not yet built)
1. **Undo/Redo** (Ctrl+Z / Ctrl+Y) — UndoService with combined ink+shape stack
2. **Pen size toggle** — Thin/Medium/Thick buttons in toolbar
3. **Filled shapes** — Outline / Tinted / Solid fill mode for Rect + Circle
4. **Export PNG** (Ctrl+E) — renders annotations to clipboard as transparent PNG
5. **10 colors** — adds Orange, Pink, Purple, Teal, Gray to palette
⚠️ **The OBS dashboard no longer controls these apps** (2026-09-28, Omri: "I want to have a
Both apps are controlled from outside through a **named pipe** (`src/Shared/ControlPipe.cs`,
compiled into both via a linked `<Compile>` in each csproj). One text command goes in and
one JSON line comes back, and every reply carries the app's current state. Only the same
Windows user can connect (`PipeOptions.CurrentUserOnly`). There are 4 instances, so a few
callers at once never see "pipe busy".
| `\\.\pipe\SmoothAnnotate.control` | `status` · `draw toggle` · `draw off` · `laser toggle` · `clear` | `{ok, drawing, tool}` |
  - `--toggle`: if the app is already running, send it `ring toggle` / `draw toggle` over
    the pipe and exit quietly. Otherwise start and switch on. This is what the Start-menu
    entries and the pen's top button run.
- **Getting out of drawing on the laptop:** Esc or the toolbar's arrow button.
- **Ring size:** the laptop's `settings.json` has `HighlightRingSize: 60`. At 200%
  scaling a ring is 2× its size in pixels, and OBS shrinks that screen to 0.6× (1800 →
  1080), so 60 comes out at ~72 px in the video, matching the home PC's 70. This is a
  per-machine value, never a code default.
### Testing touch without hands (`tools/touch-test.ps1`)
`powershell -ExecutionPolicy Bypass -File tools\touch-test.ps1 [-Steps finger,palm,laser,esc,toolbar]`
check. It takes over the screen for ~30 s and presses Esc, so **tell Omri before running it**.
  as `""` (use `[NullString]::Value`). `$r` and `$R` are the same variable. An exception on
  a background .NET thread kills the whole script, results included.
# Build both
# Run (can run both simultaneously)
works: a nuget.config OUTSIDE the repo (packageSources = nuget.org, `<fallbackPackageFolders><clear /></fallbackPackageFolders>`),
passed as `restore --configfile`, then `build` / `publish --no-restore`. Keep packages and
CLI state on E: as well: `NUGET_PACKAGES=E:\tools\nuget-packages`,
`DOTNET_CLI_HOME=E:\tools\dotnet-home`. So `deploy\publish.ps1` (plain `dotnet`, restores
itself) does not run there as is. Its steps done by hand, per app: `restore -r win-x64
--configfile …` + `publish --no-restore` with publish.ps1's flags, then copy `start.vbs` +
`install.ps1` beside them. Done this way on 2026-09-28.
- **DPI awareness:** PerMonitorV2 via ApplicationHighDpiMode project property
  ⚠️ **F9 belongs to OBS** (zoom-to-mouse, OBS-dashboard repo) — never bind it here. SmoothAnnotate's
  hook passes keys on, so a shared key fires both apps at once.
1. **Build** on the laptop, from the repo root: `powershell -File deploy\publish.ps1`. It writes
   `publish\SmoothTools\` (gitignored): `SmoothZoom\`, `SmoothAnnotate\`, `start.vbs`, `install.ps1`.
   The exe icons are `assets/*.ico`, drawn by `deploy/make_icons.py` (Pillow, run once,
   committed): a yellow ring on a dark tile, and a pencil on a red tile. The tray icons
   are the same icons, read back from the exe.
   copy stops both apps first, then copies broken or missing files over the good install.
- Stops both apps, waits for them to exit, and copies the new files in. Windows can hold an
  folder. `start.vbs` launches both apps with `--autostart`, and one that is already running
  exits quietly. The task starts them in the logged-on desktop. That is why it also works
  over SSH, where a process launched directly would run in invisible session 0.
  script stopped right there, with both apps already stopped and the new files copied. Now
  If both apps are ever found stopped after an install, `Start-ScheduledTask SmoothTools`.
- Writes two **Start-menu** entries, `Start Menu\Programs\SmoothTools\`: **"Cursor ring -
  SmoothZoom"** and **"Draw - SmoothAnnotate"**. Both run the exe with `--toggle`, so a tap
  switches the ring or drawing on and off. They can be pinned to the taskbar or picked for
  the pen's top button. The names are English because WScript.Shell reads Hebrew-named
  `.lnk` files as empty.
  after any update, check that both apps are running on the laptop. Last seen: the ✕-fix
  moment): it starts 20 s–2 min late, and the install prints only the other app (e.g.
  `running: SmoothZoom`). Tell the two apart before waiting: a **block** is a CodeIntegrity
- Ends by printing `running: SmoothAnnotate, SmoothZoom`. After that, SmoothAnnotate's
  `debug.log` should say `Toolbar: hidden from screen capture`. (`obs.log` says `connected
  to OBS` only if `AutoRingWhileRecording` was switched back on.)
```

## 2026-09-28 — separated from the OBS dashboard; nothing turns on by itself any more

Omri: the ring and the drawing "keep opening by itself", and he wanted "a separation
between the tools". The logs showed two causes, and neither was a bug in the pipe:

- **Drawing:** the keyboard hook acted on Ctrl+1…8, Ctrl+V, F10–F12 and the Ctrl+Alt keys
  in every program. The debug log that morning had `PEN` at 09:33:19 with no `F8 pressed`
  and no `control:` line (Ctrl+1, a browser tab switch), then `IMAGE PASTED` at 09:33:31
  (Ctrl+V with a screenshot on the clipboard, in another app). Now only F8 works
  everywhere; the rest only while drawing.
- **Ring:** auto-on while recording. The remote's click log has `ring:off` at 09:14:08,
  and `obs.log` has `recording started` at 09:15:00, which switched it straight back on.
  Omri chose "only when I turn it on": `AutoRingWhileRecording` false, applied by settings
  v3 on the first start of the new build.

He asked whether OBS could skip the toolbar, as it skips the dashboard's remote: yes, the
same `WDA_EXCLUDEFROMCAPTURE`, now on the toolbar and the mode labels. With the toolbar out
of the videos, the reason for hiding it completely was gone, so the default became the
small dot (click = pen, drag = move, place remembered). The dot replaced the remote's ✏️
button, which went with the rest of the remote's row (OBS-dashboard repo, same day).
Verified on the home PC: OBS's own frame had nothing where the dot was on screen.

The build was made on the home PC for the first time, with a portable SDK on E: (C: is
nearly full). Two machine quirks turned up, both in CLAUDE.md: a leftover Visual Studio
NuGet fallback folder that breaks every restore there, and `install.ps1` stopping at
"Access is denied" on the SSH-registered logon task, which left both apps stopped until
the task was started by hand. The script now keeps the existing task.

## 2026-09-27 (night) — a home-PC deploy that deleted E:\apps

Deploying the ✕ fix to the home PC, an inline SSH command carried the upload folder's
name as `E:\apps\\$IN`. The backslashes were eaten on the way; PowerShell saw
`E:\apps$IN` with `$IN` empty, so the install looked for `E:\apps\install.ps1` (not
found), and the cleanup ran `Remove-Item -Recurse -Force E:\apps`. Only the two running
exes survived (locked). `start.vbs` and `install.ps1` were gone, so the logon task would
have started nothing. The follow-up test ran on the OLD build, and its ✕ click quit
SmoothAnnotate, as the old ✕ did. A restart attempt then hung on a "script not found" box,
and while that `wscript` lived, every later start of the SmoothTools task was refused
(0x800710E0: one instance was still running).

Repair: the upload was re-sent as 8 MB chunks (a single `scp` twice stopped at ~52 MB),
reassembled and SHA-256-checked, and installed through a guarded script. Then the stuck
`wscript` was closed and the task started again. Result: all four files back, both apps
running, and the ✕ fix verified with a real click (the app stays, drawing turns off). The
rules are in CLAUDE.md → Install / update, step 3.

## 2026-09-27 (evening) — the toolbar ✕ stops drawing instead of quitting

The toolbar's ✕ called `Application.Current.Shutdown()`. On the laptop SmoothAnnotate
vanished twice that way (15:53 and 20:10: the log ends with MOUSE, with no crash and no
Smart App Control block), and the OBS remote's drawing buttons went grey until a restart.
Omri chose that ✕ only stops drawing, like the toolbar's mouse button. Quitting stays in
the tray menu.

## 2026-09-27 — the ring, the OBS remote, the laptop

One long day across several sessions: the tools went from "exists in the repo" to installed
on both machines and driven from the OBS dashboard.

- **Home PC deploy, and the upload-folder collision.** Two sessions were both told "update
  the home PC" and uploaded into the same `E:\apps\SmoothTools-incoming` about a minute
  apart (15:44). `scp -r` nested the second copy inside the first; the first session's
  cleanup deleted it half-written. No harm: the second `install.ps1` found nothing to run,
  and the home PC ended up on the first session's verified build of b743508. → rule: an
  upload folder of your own.
- **Finger drawing (b3b7f94, b743508).** The first touch version made "only the pen draws"
  (`IgnoreTouch`). The same day Omri chose instead that a finger draws too, except while the
  pen is near (`IgnoreTouchNearPen`, palm rejection). Also: F11 became a real on/off (from the
  laser it went to the pen before, so the screen stayed covered), Esc stops drawing, and the
  hit layer got holes under the toolbar and the remote — before that, a finger tap on the
  toolbar landed on the canvas. `tools/touch-test.ps1` added; all 7 of its checks passed on
  the laptop. Its quirks cost an afternoon (they are listed in CLAUDE.md).
- **`--toggle` hung forever (fixed in 3211443).** `ControlPipeServer.Send` waited on async
  pipe calls from the second launch's UI thread; the continuations queued behind that wait,
  so each Start-menu tap left a stuck copy after delivering its command — two stuck SmoothZoom
  copies on the laptop. Those callers never read their reply, and the server's
  `WaitForPipeDrain()` (no timeout) held a pipe instance per stuck caller. Fixed: `Send` runs
  on the pool; the server waits at most 2 s for the caller to hang up.
- **Empty pipe replies (fixed in 4ae855b).** One 2 s timer covered read + handle + write, so a
  slow UI moment cancelled the write and the caller got an empty reply. Now the reply has its
  own timer and UI work gives up after 1.5 s with `busy`.
- **Smart App Control on the laptop.** The new HP OmniBook Ultra Flip 14 has SAC ON (the home
  PC is off). One build's SmoothAnnotate was blocked at 15:00:05 (CodeIntegrity 3077 + 3118,
  "Windows Script Host: An Application Control policy has blocked this file (800711C7)" from
  `start.vbs`). The next build — only the pipe fix changed — was let through for both apps
  after ~20 s, and later builds too. The first "late start" was at first mistaken for a
  Defender scan; the 3077 event is what tells a block from a delay. Omri was given the steps
  to turn SAC off himself (a security setting); as of 15:49 it was still on.
- **Defender/cloud first-run hold.** A brand-new build of SmoothAnnotate started 20 s–2 min
  late while the install printed `running: SmoothZoom` only (Defender cloud-protection events
  2010 at that moment); no 3077, so not a block.
- **`start.vbs` and WMI (fixed in 4ae855b).** It asked WMI which apps were running; right after
  an update the just-stopped SmoothAnnotate was still listed (install.ps1's PowerShell held a
  handle to it), so it was skipped. Now both apps start with `--autostart` and exit quietly
  if already running.
- **Control pipe, Start menu, OBS remote (4ae855b).** Named pipes `SmoothZoom.control` /
  `SmoothAnnotate.control`; `--toggle` / `--autostart`; Start-menu entries "Cursor ring -
  SmoothZoom" and "Draw - SmoothAnnotate"; app icons; the OBS dashboard's remote got ring /
  draw / laser / clear buttons (OBS-dashboard cca68ff). Measured: warm replies 1–6 ms, the
  first calls after a start up to ~1 s.
- **Laptop install + one installer (2f9aba9).** `deploy/publish.ps1` + `install.ps1` +
  `start.vbs`; the laptop got the .NET 8 SDK (8.0.425, winget) because the home PC has none.
- **Cursor ring for OBS (4e3f93a).** Yellow ring with click ripple; turns on by itself while
  OBS records (`ObsRecordingWatcher`). SmoothAnnotate's draw toggle moved from F9 to F8,
  because F9 is OBS-dashboard's zoom-to-mouse and SmoothAnnotate's hook passes keys on (one
  press fired both). The toolbar hides outside draw mode (`HideToolbarWhenIdle`) — the first
  home-PC test showed it sitting on the recorded screen.

## Verbatim: lines rewritten or moved out of CLAUDE.md on 2026-09-27

Every line below is exactly as it stood in `CLAUDE.md` before the compression (commit
82ed227), kept so that no fact is lost in a rewrite. The rules they carried are still in
`CLAUDE.md`, reworded without the dates and stories.

```text
| F8 | Toggle draw mode (was F9 until 2026-09-27 — F9 is OBS zoom-to-mouse) |
| F11 | Laser pointer on/off — pressed again it goes back to the mouse (until 2026-09-27 it went to the pen, so the screen stayed covered) |
- **Mouse/Pointer** — exits draw mode. With `HideToolbarWhenIdle` (default **on** since 2026-09-27) the toolbar **hides completely** — it sits on the recorded screen, so any visible toolbar or dot ends up in every OBS video; F8 / Ctrl+1-8 bring it back. With it off: collapses to a small floating dot; click the dot to re-expand
  - **Holes in the hit layer** (since 2026-09-27) under the toolbar and the OBS remote (`UpdateHitLayer`). A tap there falls through the overlay to the window below. **This is what makes finger taps work:** a finger doesn't hover, so the timer below never sees it coming. Before this, a tap on the toolbar landed on the canvas.
  ⚠️ **Until that fix, one 2 s timer covered read + handle + write.** A slow UI moment then
  cancelled the write, and the caller got an EMPTY reply.
- ⚠️ **`ControlPipeServer.Send` runs on the thread pool** (`Task.Run`). The first version
  waited on the async pipe calls straight from the second launch's `OnStartup`, which is
  the UI thread. Their continuations queued for that same waiting thread, so every
  `--toggle` copy hung forever after delivering its command (2026-09-27: two copies of
  SmoothZoom stuck).
  and those hung callers held an instance each. After replying, it waits at most 2 s for
  the caller to hang up (`WaitForHangUpAsync`).
  `Views/PenInkCanvas.cs`). Omri chose this on 2026-09-27, the same day, over the first
  version's "only the pen draws": he wants to draw with his hand too. `false` = a finger
  always draws.
All 7 checks passed on the laptop on 2026-09-27. The quirks below cost an afternoon; the
script works around each one:
   exist first.** On 2026-09-27 two sessions, both told "update the home PC", uploaded into
   the same `SmoothTools-incoming` a minute apart. `scp -r` into an existing folder nests
   the copy (`…-incoming\SmoothTools\…`) instead of failing. The first session's cleanup
   then deleted the second one's half-written files. No harm was done, because the second
   install found nothing to copy. But an `install.ps1` run from a partial copy stops both
   apps first, and then copies broken or missing files over the good install.
  ⚠️ `start.vbs` used to ask WMI which apps were running. That failed after an update: a
  process stopped a moment earlier stays listed while anything still holds a handle to it
  (install.ps1's own PowerShell did), so SmoothAnnotate was skipped.
  himself, since it's a security setting; re-read it before relying on this line). SAC blocks unsigned programs it has no
  good cloud verdict for. **Every new build is judged again, and the verdict varies.** On
  2026-09-27 one build ran SmoothZoom and blocked SmoothAnnotate (CodeIntegrity event
  3077, "did not meet the Enterprise signing level requirements"). The next build, with
  only the pipe fix, was let through for both after ~20 s. So after any update, check that
  both apps are running on the laptop. Launched by `start.vbs`, the block shows up as a
  moment): SmoothAnnotate started 20 s–2 min late on 2026-09-27, while the install printed
  `running: SmoothZoom` only. Tell the two apart before waiting: a **block** is a CodeIntegrity
```

## Moved from CLAUDE.md (2026-11-15)

Text moved out of `CLAUDE.md` on 2026-11-15 (round-2 trim), verbatim, grouped by the section it
stood in (old line numbers in brackets, from commit 9ecf185). The rules these lines carry are still
in `CLAUDE.md`, reworded without the dates, quotes, test runs and install records.

### From "SmoothDraw"

[lines 8-11]
```text
Until 2026-09-28 it was **SmoothAnnotate**, inside the `smoothzoom` repo next to the
cursor-ring app. It moved here with its history (`git filter-repo`); SmoothZoom stayed
there, until it was retired on 2026-10-18 (its ring moved here). Why things are the way they are (dated incidents and decisions): `CHANGELOG.md`,
not auto-loaded. Entries before 2026-09-28 there cover both apps.
```

### From "Hotkeys"

[lines 25-70]
```text
⚠️ **Only F9, F5, F6 and Ctrl+Alt+H work in every program (F9 = F8 until 2026-10-13, see the table; F5 / F6
since 2026-10-18, "The thinking timer and the board" below; Ctrl+Alt+H since 2026-10-18, "The cursor ring"). Every other key below acts only while you are
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
| F6 | **Thinking timer, 10 s, from anywhere.** Shift+F6 = 30 s, Ctrl+F6 = 60 s, Ctrl+Shift+F6 = clear the drawings (the pad's page 4 types these). The same length again stops it. Kept from other programs |
| F5 | **The board, from anywhere**: on (and the pen) / off (back to the screen). Kept from other programs; Ctrl+F5 still reaches them |
| PageDown / → , PageUp / ← | **On the board only:** the next / previous page (past the last = a new page). Elsewhere these keys are untouched |
| F9 | Toggle draw mode, from anywhere (never F8 — that is OBS zoom-to-mouse since 2026-10-13; the two were swapped that day, F8 was this key before). Not kept from other programs |
| F10 | Clear all (undoable) |
| F11 | Laser pointer on/off — pressed again it goes back to the mouse, never to the pen (the screen would stay covered) |
| Esc | Stop drawing (back to the mouse). While typing text, the first Esc finishes the text. Kept from the app underneath only when it stopped something |
| F12 | Timer start/pause (double-tap = reset) |
| Ctrl+Z | Undo |
| Ctrl+Y, Ctrl+Shift+Z | Redo (never with Alt: Ctrl+Alt+Z was SmoothZoom's zoom) |
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
| Ctrl+Alt+H | **The cursor ring on/off, from anywhere** (SmoothZoom's key until 2026-10-18; the pad's ◎ types it). Kept from other programs |
| Ctrl+Alt+T | Timer show/hide |
| Ctrl+Alt+1-5 | Colors: Red, Blue, Green, White, Yellow |
```

### From "The thinking timer and the board (since 2026-10-18)"

[lines 73-77]
```text
Omri: "a timer that shows on the screen and goes away — say I want them to think about something for
10 seconds", and "a whiteboard in the style of Khan Academy or Excalidraw". Asked, he chose: on the REAL
screen (so a recording AND a Zoom screen share both show it, which an OBS source would not), a ring that
empties with the number inside, F6 / F5, the pad, and no button on the OBS remote (the tools stay
apart). Both live on the overlay, which OBS's Display Capture records like the drawings.
```

[lines 83-85]
```text
  their plain codes too (VK_SHIFT 0x10 …): a key typed by a program (the pad's `press_key.py`,
  `keybd_event`) may come that way. Checked 2026-10-18: press_key.py's Shift+F6 / Ctrl+F6 / F6 /
  Ctrl+Shift+F6 reached it as 30 s / 60 s / 10 s / clear, and Shift+F6 twice stopped the 30 s.
```

[lines 95-96]
```text
  out at once; another length = it starts over with that one. Measured 2026-10-18: 10 s started →
  done in 10.000 s (the log's `Countdown 10s: started / done`).
```

[lines 106-107]
```text
  - **Follows the OBS take** (since 2026-10-28; Omri: the timer should pause with the take, and go
    when the take is restarted 🔄, cancelled 🗑 or a part redone ↩️). The OBS dashboard sends
```

[lines 125-125]
```text
  - **Pages** (since 2026-10-18 later; Omri: "several screens to go through, like flipping pages"):
```

[lines 134-150]
```text
  - **Checked 2026-10-18 on the laptop** (a Release build from the session, pipe + simulated mouse
    and keys): the top-edge bar (removed 2026-10-24) and the pages (a stroke on page 1; page 2 empty; PageUp back to page 1 with its ink;
    → / PageDown; no new page from an empty one; F5 off/on opens on page 2; undo acts on the page on
    show).
  - **Installed 2026-10-18 ~17:00 on both machines** (build of 4420e91): the laptop through a
    one-off scheduled task (⚠️ on battery, a task with default settings sits "Queued" and never
    starts: give it `-AllowStartIfOnBatteries -DontStopIfGoingOnBatteries`), the home PC over SSH
    (its first build with the timer and the board too). Each `status` reply carried `page` /
    `pages`; both at Normal priority, no CodeIntegrity 3077 on the laptop.
- **Checked 2026-10-18 on the laptop** (a Release build from the session, against OBS's own frame of
  the program scene): the timer at 8 s (amber) and 2 s (red), gone after; the board in OBS's frame
  with the camera circle on it and the timer top-right; F5 off = the screen back with the timer still
  running. ⏳ Not yet: the board's drawings coming back on a second F5 with a real stroke (the one
  drawn during the test was cut off by the board closing mid-stroke).
- **Checked 2026-10-18 17:52 on the home PC** (the 4420e91 build installed there at ~17:00, over SSH,
  OBS open and not recording): the pipe's `countdown 3` answered `countdown 3`, still 3 a second
  later, 0 five seconds later; SmoothDraw at Normal priority. ⏳ Not yet there: OBS's frame of it.
```

### From "The cursor ring (Ctrl+Alt+H, since 2026-10-18)"

[lines 153-156]
```text
Omri: "I don't use SmoothZoom; the only thing I want from it is the ring. Move it into SmoothDraw
and we can say goodbye to SmoothZoom." Ported from SmoothZoom's `CursorHighlightService` as
`Services/CursorRingService.cs`, looking and behaving the same; not the zoom, and not its OBS
recording watcher: **the ring never comes on by itself.**
```

[lines 172-173]
```text
  (`HighlightRingSize` …), else the defaults above. That is how the laptop's ring 60 came along
  (60 at 200 % scaling comes out ~72 px in the video, like the home PC's 70; smoothzoom CLAUDE.md).
```

### From "The toolbar"

[lines 185-195]
```text
- **⏳ the thinking timer** (since 2026-10-18; Omri: "a shortcut to the thinking timer … as a
  button"): a Tabler `hourglass`, tooltip "טיימר חשיבה (F6)". A click opens 10 · 30 · 60 beside
  it **inside the bar** (`CountdownGroup`), not a popup: a popup is a window of its own, which
  would be recorded and would not be a hole in the hit layer (a finger tap would land on the
  canvas). A pick = `StartCountdown`, exactly F6 / Shift+F6 / Ctrl+F6: the same one again while
  it runs stops it. The running one is lit (`SetCountdown`, from `CountdownHud.Ended` when it
  stops). The choice stays open until ⏳ again or the bar hides, so after a pick nothing slides
  under the pointer; opening it grows the bar (pulled back inside the screen, as for the pages).
  - Why it changed: confetti and delete were invisible. They were emoji `TextBlock`s, and
    WPF draws emoji in one colour: with no Foreground set, black on the near-black bar.
    **Never use emoji for toolbar icons.**
```

[lines 202-229]
```text
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
```

### From "The dot, and where the bar opens"

[lines 236-248]
```text
- ⚠️ **Both machines hide the dot since 2026-10-10** (`HideToolbarWhenIdle: true` in their
  settings.json, Settings below): stopping drawing hides the toolbar completely. The rest of
  this section is how the dot works when it is on.
- **No hover brings the bar** (removed 2026-10-24; it was there from 2026-10-18). The mouse resting
  on the top edge used to show the bar there, centred on the mouse. Omri: "it's annoying that when I
  hover my mouse, I suddenly see the bar" — and a bar dragged down from it jumped back to the top
  (drawing was still off, so it stayed a "peek" and was put back). With `HideToolbarWhenIdle` the bar
  shows only while drawing, at the place it was last dragged to. Don't bring a hover back.
- **Mouse/Pointer** — exits draw mode, and the toolbar shrinks to a **42 px dot**
  (`HideToolbarWhenIdle` false, the default since 2026-09-28): white, a dark pencil, the ink
  colour as a badge. **Click the dot = pen, drag it = move it.** The dot replaced the OBS
  remote's ✏️ button as the mouse's way in. Until the capture exclusion above, the default
  was `true` (hide completely), because a visible toolbar or dot ended up in every video.
```

[lines 274-279]
```text
  - **Verified on the home PC 2026-09-28** (pipe `dot X Y`, then open and close; the rects
    read from outside): bottom-left, bottom-right, top-right, left edge of the main screen
    and the left edge of the 150 % screen — every bar on the dot's vertical centre (at the
    very bottom edge the 66 px bar sits 2 px higher than the 62 px dot, to stay inside the
    work area), on the side facing the middle, inside the work area, and the dot back on its
    exact spot after closing.
```

### From "Undo / redo (`Services/DrawingHistory.cs`)"

[lines 317-323]
```text
- **Clear with a text box open** finishes the box properly first. Before 2026-09-28 it was
  removed with the rest but stayed "active" and kept the mouse.
- **Verified on the home PC 2026-09-29** (`touch-test.ps1 -Steps undo,keys`, installed
  build): draw → undo (ink gone) → redo (back) → clear → undo (back) → Ctrl+4 + an eraser
  drag (gone) → undo (back), all PASS; and while drawing, Ctrl+V / Ctrl+Z / Ctrl+Y and F11
  twice left a text box underneath untouched. (By accident the same held for Windows 11
  Notepad underneath, twice: its text stayed as typed, nothing pasted or undone.)
```

### From "Structure"

[lines 335-370]
```text
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
│   ├── KeyboardHookService.cs # F9, F5/F6 (any program), F10-F12, Esc, Ctrl+0-8, Ctrl+V, Ctrl+Z/Y, Ctrl+Alt combos; what to keep from other programs
│   ├── LaserService.cs        # Laser fade-out timer (single-stroke approach)
│   ├── CursorRingService.cs   # The cursor ring (Ctrl+Alt+H) and its click ripple
│   ├── StopwatchService.cs    # Timer with double-tap reset
│   ├── ConfettiService.cs     # Particle physics confetti
│   ├── OverlayService.cs      # Win32 click-through toggling, z-order, capture exclusion
│   ├── PenStateService.cs     # pen.json
│   ├── CountdownPlaceService.cs # countdown.json: where the thinking timer shows
│   └── SettingsService.cs     # JSON persistence
└── Views/
    ├── OverlayWindow.xaml(.cs)  # Fullscreen transparent overlay (Board + InkCanvas + ShapeCanvas + CountdownHost + ConfettiCanvas); the board's swap
    ├── CountdownHud.cs          # The thinking timer's ring (F6)
    ├── ArrowShape.cs            # The arrow's shaft + head layout (tip on the end point, head by thickness)
    ├── PenInkCanvas.cs          # InkCanvas that ignores touches while the pen is near (IgnoreTouchNearPen)
    ├── ToolbarWindow.xaml(.cs)  # The light toolbar and its dot; placement; snapshots
    ├── ToolbarProps.cs          # IsActive, for the button styles
    ├── Icons.xaml               # Tabler icons as geometries + IconTemplate
    └── ToastWindow.xaml(.cs)    # Mode indicator popup
```
```

### From "DPI"

[lines 388-392]
```text
⚠️ **SmoothDraw runs System-DPI-aware, not per-monitor** (read 2026-09-28 on the home PC:
every one of its windows reports awareness 1 at 96 DPI; SmoothZoom's too). The csproj's
`ApplicationHighDpiMode` — which the old docs credited with PerMonitorV2 — only feeds
WinForms' startup code; a WPF app takes its DPI mode from the manifest, and `app.manifest`
declares none. What that means:
```

### From "Settings"

[lines 403-408]
```text
Stored at `%APPDATA%\SmoothDraw\settings.json`. **Since 2026-10-10 both machines have one,
written by hand with ONE key: `{"HideToolbarWhenIdle": true}`** (Omri: "I don't want the
SmoothDraw circle to be visible all the time on the side of my screen"). Every other value
is still the code default: a missing key keeps its default when the file is read. A file the
app itself saved would pin every value at the time it was saved — re-check before relying on
a default. Before that day neither machine had a file.
```

[lines 415-425]
```text
- **No dot = drawing starts from F9**: the pad's small right knob press (davinci-automation
  CLAUDE.md surface 6), the Start menu / the pen's top button (`--toggle`) or F9 itself. Nothing on
  the screen's edge brings the bar (since 2026-10-24, "The dot" above).
  The toolbar shows while drawing, where it was last dragged, and goes away when drawing stops.
- How it was put there: the laptop through a one-off scheduled task (the MSIX redirect in
  "Build & Run"), the home PC over SSH; then SmoothDraw stopped and started again by its
  logon task (`Start-ScheduledTask SmoothDraw`). The setting is read only at start. The log
  still says `Toolbar closed: dot at (…)` at start; what counts is the toolbar window
  (`SmoothDraw Toolbar`) being invisible, which it was on the laptop. `IgnoreTouchNearPen` (default true)
and `PassThroughWindowTitle` — see "Pen and touch" below. The dot's place is NOT a setting:
`dot-position.json` beside it; nor is what you pick on the toolbar: `pen.json`.
```

### From "Planned (Tier 1 — not yet built)"

[lines 443-443]
```text
(Undo/Redo and the pen size toggle, items 1–2 of this list until 2026-09-28, are built.)
```

### From "Control from other programs (OBS remote, Start menu, pen button, tests)"

[lines 448-457]
```text
**The OBS dashboard's remote drives this app again** (since 2026-10-18; Omri reversed
2026-09-28's "I want to have a separation between the tools" with "I want it all from there").
The remote's main screen has a row ◎ ring (`ring toggle`, SmoothZoom's pipe until 2026-10-18) · ✏️ pen · ➡️ arrow ·
🔴 laser · ↶ undo · 🧹 clear, through OBS-dashboard's `smooth.py` (`tool pen` / `tool arrow` /
`tool laser`, `tool none` to go back to the mouse, `undo`, `clear`). It works from his phone
too, over Tailscale. The hotkeys, the dot, the tray, the Start menu and the pad work as before.
A SmoothDraw from before `tool <name>` answers `unknown command`, which the remote shows as
"SmoothDraw needs an update". While drawing, SmoothDraw lets clicks through to the remote
window (`PassThroughWindowTitle`, "Pen and touch"), so its buttons and REC / stop stay
pressable.
```

[lines 469-470]
```text
SmoothZoom's pipe (`SmoothZoom.control`) went with it on 2026-10-18: the ring's `ring …`
commands are this pipe's now. Until 2026-09-28 this one was `SmoothAnnotate.control`.
```

[lines 495-496]
```text
- **Measured 2026-09-27:** warm replies take 1–6 ms. The first calls after an app starts
  took up to ~1 s, so a caller should wait 1.5 s (the removed `smooth.TIMEOUT` did).
```

### From "Pen and touch (the laptop: HP OmniBook Ultra Flip 14, touch screen + pen)"

[lines 504-542]
```text
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
```

### From "Testing without hands (`tools/touch-test.ps1`)"

[lines 553-555]
```text
- **`undo`**: draw → undo (no red) → redo (red) → clear → undo (red) → Ctrl+4 (eraser) +
  a finger drag along the stroke (no red) → undo (red). The first stroke and the eraser
  drag are retried (up to 4×): a lost injected touch would otherwise fail every check after it.
```

[lines 559-595]
```text
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
```

### From "Build & Run"

[lines 602-610]
```text
# Requires the .NET 8 SDK, 8.0.425 on both machines:
# - laptop: C:\Program Files\dotnet (winget, 2026-09-27). A shell opened before that
#   install lacks it on PATH: open a new one.
#   ⚠️ Check `hostname` first: a Claude desktop session on the home PC (DESKTOP-7HQM8GO) has the
#   same C:\Users\omrii\Projects\SmoothDraw path. There, C:\Program Files\dotnet is only the
#   runtime; use E:\tools\dotnet. On 2026-10-24 a session took the home PC for the laptop and
#   installed into the laptop's folder on its C: (undone the same day).
# - home PC: portable, E:\tools\dotnet (dotnet-install.ps1 -Version 8.0.425 -NoPath,
#   2026-09-28; no admin, nothing on the full C:). Not on PATH — see "On the home PC" below.
```

[lines 618-631]
```text
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
```

### From "Key Technical Details"

[lines 649-656]
```text
## Key Technical Details
- **Hotkeys:** F-keys + Ctrl+number, Ctrl+Alt+letters while drawing, and Ctrl+Alt+H (the ring) from
  anywhere. SmoothZoom (Ctrl+Alt+Z zoom, Ctrl+Alt+H) is retired since 2026-10-18; a machine that
  still runs it would catch Ctrl+Alt+H in both apps (the newer hook first, which keeps the key).
  ⚠️ **F8 belongs to OBS** (zoom-to-mouse, OBS-dashboard repo; F9 until 2026-10-13, when Omri
  swapped the two so the zoom sits beside F7 = the camera full / small) — never bind it here. F9 is
  passed on to other programs, so a key shared with another app fires both at once. F7 is the
  OBS dashboard's too. F5 and F6 are this app's from any program since 2026-10-18 (kept from them).
```

### From "Install / update (laptop + home PC)"

[lines 659-661]
```text
Its own install, fully apart from SmoothZoom's (Omri: "a separation between the tools"):
its own folder, logon task and Start-menu entry. The build is self-contained — no .NET
needed on the target. Both machines can build and install their own copy in place.
```

[lines 699-740]
```text
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
- 🚨 **The laptop had Smart App Control ON** (OFF since 2026-10-27; read 2026-09-29 ~18:40: `Get-MpComputerStatus` →
  `SmartAppControlState: On`; the home PC is Off. Omri was given the steps to turn it off
  himself, since it's a security setting; re-read it before relying on this line). SAC blocks
  unsigned programs it has no good cloud verdict for. **Every new build is judged again, and
  the verdict varies** — a build can be blocked and the next one let through (CHANGELOG). So
  after any update, check that the app is running on the laptop. Last seen: the build of 64764ef was blocked (3077, 2026-10-27 19:51); Omri then turned SAC
  **OFF** on the laptop (20:49, `SmartAppControlState: Off`; it cannot be turned back on without
  resetting Windows), and the same build installed and ran at 20:50. A block is CodeIntegrity
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
```
