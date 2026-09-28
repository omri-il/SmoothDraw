# Installs the SmoothDraw folder this script sits in (built by publish.ps1) to -Target,
# registers the logon task "SmoothDraw" and starts it. Safe to re-run for an update: it
# stops the running app first. Run it on the machine itself; over SSH is fine, because the
# task starts the app in the logged-on desktop (a process started straight from SSH would
# be invisible).
#
#   Home PC: -Target E:\apps\SmoothDraw
#   Laptop:  -Target "$env:LOCALAPPDATA\Programs\SmoothDraw"
#
# Fully separate from SmoothZoom's install (smoothzoom repo): its own folder, task and
# Start-menu entry.
param([Parameter(Mandatory)][string]$Target)
$ErrorActionPreference = 'Stop'

$src = (Resolve-Path -LiteralPath $PSScriptRoot).Path.TrimEnd('\')
if (-not (Test-Path -LiteralPath "$src\SmoothDraw.exe")) { throw "No SmoothDraw.exe beside this script ($src)" }

# The app, and its old self: SmoothAnnotate (until 2026-09-28) must never draw alongside it
$running = @(Get-Process SmoothDraw, SmoothAnnotate -ErrorAction SilentlyContinue)
$running | Stop-Process -Force
$running | Wait-Process -Timeout 15 -ErrorAction SilentlyContinue

New-Item -ItemType Directory -Force -Path $Target | Out-Null
$dst = (Resolve-Path -LiteralPath $Target).Path.TrimEnd('\')
if ($src -ne $dst) {
    # Windows can hold an exe's file lock for a moment after its process has exited
    for ($i = 1; ; $i++) {
        try { Copy-Item -LiteralPath "$src\SmoothDraw.exe", "$src\install.ps1" -Destination $dst -Force; break }
        catch { if ($i -ge 10) { throw } ; Start-Sleep -Seconds 1 }
    }
}
$exe = "$dst\SmoothDraw.exe"

# The logon task runs the exe itself (no start.vbs). The task then counts as running for as
# long as the app does, so it gets no time limit and keeps going on battery. Priority 4 =
# normal: Task Scheduler's default (7) starts the app at BelowNormal, as it does SmoothZoom.
# $env:USERDOMAIN is WORKGROUP over SSH, and a task for WORKGROUP\user never runs.
$user = "$env:COMPUTERNAME\$env:USERNAME"
$action = New-ScheduledTaskAction -Execute $exe -Argument '--autostart' -WorkingDirectory $dst
$trigger = New-ScheduledTaskTrigger -AtLogOn -User $user
$principal = New-ScheduledTaskPrincipal -UserId $user -LogonType Interactive -RunLevel Limited
$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries `
    -ExecutionTimeLimit ([TimeSpan]::Zero) -Priority 4
try {
    Register-ScheduledTask -TaskName 'SmoothDraw' -Action $action -Trigger $trigger -Principal $principal `
        -Settings $settings -Description 'SmoothDraw: on-screen drawing (SmoothDraw repo)' -Force | Out-Null
} catch {
    # A task registered over SSH can't be overwritten from a plain desktop shell ("Access is
    # denied", home PC 2026-09-28, SmoothZoom's task). The existing one does the same job
    # when it runs this same exe.
    $have = Get-ScheduledTask -TaskName 'SmoothDraw' -ErrorAction SilentlyContinue
    if (-not $have -or $have.Actions[0].Execute -ne $exe) { throw }
    Write-Warning "Kept the existing SmoothDraw task (could not re-register it: $($_.Exception.Message))"
}

# Start menu (and so pinnable to the taskbar, and pickable for the pen's top button):
# --toggle switches the RUNNING copy's drawing on or off. An English name: WScript.Shell
# cannot read back a Hebrew-named .lnk.
$lnk = (New-Object -ComObject WScript.Shell).CreateShortcut(
    (Join-Path ([Environment]::GetFolderPath('Programs')) 'SmoothDraw.lnk'))
$lnk.TargetPath = $exe
$lnk.Arguments = '--toggle'
$lnk.WorkingDirectory = $dst
$lnk.IconLocation = "$exe,0"
$lnk.Description = 'Drawing on/off'
$lnk.Save()

Start-ScheduledTask -TaskName 'SmoothDraw'
Start-Sleep -Seconds 10   # the first start of a compressed single-file exe is slow
$now = @(Get-Process SmoothDraw -ErrorAction SilentlyContinue)
if ($now) { "running: SmoothDraw (pid $($now.Id -join ', '))" }
else { 'SmoothDraw is NOT running yet. A new build can start late once (CLAUDE.md -> Install / update): check again, and look for CodeIntegrity 3077 before waiting.' }
