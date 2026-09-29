# SmoothDraw — history

**Not auto-loaded.** Split out of `CLAUDE.md` on 2026-09-27 (`/compress-docs`). `CLAUDE.md`
keeps the rules; this file keeps why they exist — the dated incidents and decisions behind
them. Newest first. Git history has the full detail. Everything before 2026-09-28 happened
in the smoothzoom repo, where this app was SmoothAnnotate next to SmoothZoom, so those
entries cover both apps.

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
