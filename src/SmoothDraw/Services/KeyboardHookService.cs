using System.Runtime.InteropServices;
using SmoothDraw.Native;

namespace SmoothDraw.Services;

public class KeyboardHookService : IDisposable
{
    private IntPtr _kbHookId = IntPtr.Zero;

    // CRITICAL: Store delegate as class field to prevent GC collection
    private readonly User32.LowLevelKeyboardProc _kbHookProc;

    private bool _ctrlPressed;
    private bool _altPressed;
    private bool _shiftPressed;

    private const uint VK_ESCAPE = 0x1B, VK_F9 = 0x78, VK_F10 = 0x79, VK_F11 = 0x7A, VK_F12 = 0x7B;
    private const uint VK_V = 0x56, VK_Y = 0x59, VK_Z = 0x5A;

    // Core tools
    public event Action? DrawModeToggled;
    public event Action? ClearInk;
    public event Action? LaserToggled;
    public event Action? TimerToggled;
    public event Action? TimerVisibilityToggled;

    // Shape tools
    public event Action? ArrowToggled;
    public event Action? RectangleToggled;
    public event Action? CircleToggled;
    public event Action? TextToggled;

    // Color picker (1-5)
    public event Action<int>? ColorChanged;

    // Tool number shortcuts (Ctrl+1 through Ctrl+8, Ctrl+0 = mouse)
    public event Action<int>? ToolShortcut;

    // Clipboard paste
    public event Action? ClipboardPaste;

    // Ctrl+Z / Ctrl+Y (or Ctrl+Shift+Z)
    public event Action? UndoRequested;
    public event Action? RedoRequested;

    // Esc: answers true when it used the key (stopped drawing). The key is then kept from
    // the app underneath — there Esc would also end a PowerPoint slideshow, say.
    public event Func<bool>? EscapePressed;

    // Whether you are drawing right now. Every key but F9 acts only while you are: in other
    // programs they are ordinary keys (Ctrl+1 switches browser tabs, Ctrl+V pastes, F11 is
    // full screen), and caught everywhere they kept switching drawing on by itself.
    public Func<bool>? IsDrawing { get; set; }

    // Whether a text box on the overlay is being typed in. It has the keyboard then, so
    // nothing is kept from it, and Ctrl+Z / Ctrl+Y / Ctrl+V are its own (undo typing, paste).
    public Func<bool>? IsTyping { get; set; }

    // A key whose key-down was kept from the other programs gets its key-up kept too, or
    // the program underneath would see a key released that it never saw pressed. Only the
    // key itself: Ctrl / Shift / Alt always go through, both ways.
    private readonly HashSet<uint> _swallowUp = new();

    public KeyboardHookService()
    {
        _kbHookProc = KeyboardHookCallback;
        Install();
    }

    private void Install()
    {
        var moduleHandle = Kernel32.GetModuleHandle(null);
        App.Log($"GetModuleHandle returned: {moduleHandle}");

        _kbHookId = User32.SetWindowsHookEx(User32.WH_KEYBOARD_LL, _kbHookProc, moduleHandle, 0);
        if (_kbHookId == IntPtr.Zero)
        {
            var err = Marshal.GetLastWin32Error();
            App.Log($"HOOK FAILED! Error code: {err}");
            throw new InvalidOperationException($"Failed to install keyboard hook. Error: {err}");
        }
        App.Log($"Keyboard hook installed OK. Handle: {_kbHookId}");
    }

    // Runs on the UI thread, inside Windows' hook call: it only decides and raises events.
    // The work itself is queued by the handlers (App: Dispatcher.BeginInvoke), because a
    // hook that takes too long is skipped, or removed, by Windows.
    private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var kbd = Marshal.PtrToStructure<User32.KBDLLHOOKSTRUCT>(lParam);
            bool isKeyDown = wParam == User32.WM_KEYDOWN || wParam == User32.WM_SYSKEYDOWN;
            uint vk = kbd.vkCode;

            // Track modifier state
            switch (vk)
            {
                case 0xA2 or 0xA3: // VK_LCONTROL / VK_RCONTROL
                    _ctrlPressed = isKeyDown;
                    break;
                case 0xA4 or 0xA5: // VK_LMENU / VK_RMENU (Alt)
                    _altPressed = isKeyDown;
                    break;
                case 0xA0 or 0xA1: // VK_LSHIFT / VK_RSHIFT
                    _shiftPressed = isKeyDown;
                    break;
            }

            if (!isKeyDown)
            {
                if (_swallowUp.Remove(vk)) return (IntPtr)1;
            }
            else if (vk == VK_ESCAPE)
            {
                // Plain Esc only (Ctrl+Alt+Esc is SmoothZoom's)
                if (!_ctrlPressed && !_altPressed && EscapePressed?.Invoke() == true)
                {
                    _swallowUp.Add(vk);
                    return (IntPtr)1;
                }
            }
            // Toggle draw mode, from anywhere. F9 since 2026-10-13 (Omri swapped the two: F8 is
            // OBS zoom-to-mouse now, beside F7 = the camera full / small). Passed on, never kept:
            // a key both tools catch fires both, so the two must never share one.
            else if (vk == VK_F9)
            {
                App.Log("F9 pressed -> DrawModeToggled");
                DrawModeToggled?.Invoke();
            }
            else if (IsDrawing?.Invoke() == true && DrawingKey(vk) is { } key)
            {
                bool typing = IsTyping?.Invoke() == true;
                if (!(typing && key.TextBoxKey)) key.Act();
                if (!typing)
                {
                    // Used here, so kept from the program underneath: F11 would also go
                    // full screen in a browser, Ctrl+Z would undo there too
                    _swallowUp.Add(vk);
                    return (IntPtr)1;
                }
            }
        }

        return User32.CallNextHookEx(_kbHookId, nCode, wParam, lParam);
    }

    /// <summary>What a key does while drawing, or null when it is not one of ours.
    /// textBoxKey: typing in a text box, the key is the text box's (undo typing, paste).</summary>
    private (Action Act, bool TextBoxKey)? DrawingKey(uint vk)
    {
        // F-key hotkeys (no modifiers needed - simple and reliable)
        switch (vk)
        {
            case VK_F10:
                return (() => { App.Log("F10 pressed -> ClearInk"); ClearInk?.Invoke(); }, false);
            case VK_F11:
                return (() => { App.Log("F11 pressed -> LaserToggled"); LaserToggled?.Invoke(); }, false);
            case VK_F12:
                return (() => { App.Log("F12 pressed -> TimerToggled"); TimerToggled?.Invoke(); }, false);
        }

        // Ctrl+number shortcuts for tools, Ctrl+V, undo/redo (Ctrl only, never Alt:
        // Ctrl+Alt+Z is SmoothZoom's)
        if (_ctrlPressed && !_altPressed)
        {
            if (vk is >= 0x30 and <= 0x38) // Ctrl+0 = mouse, Ctrl+1-8 = Pen … Text
            {
                int num = (int)(vk - 0x30);
                return (() => ToolShortcut?.Invoke(num), false);
            }
            switch (vk)
            {
                case VK_V: return (() => ClipboardPaste?.Invoke(), true);
                case VK_Z when _shiftPressed: return (() => RedoRequested?.Invoke(), true);
                case VK_Z: return (() => UndoRequested?.Invoke(), true);
                case VK_Y: return (() => RedoRequested?.Invoke(), true);
            }
        }

        // Ctrl+Alt hotkeys for less frequent tools
        if (_ctrlPressed && _altPressed)
        {
            switch (vk)
            {
                case 0x54: return (() => TimerVisibilityToggled?.Invoke(), false); // Ctrl+Alt+T
                case 0x41: return (() => ArrowToggled?.Invoke(), false);           // Ctrl+Alt+A
                case 0x52: return (() => RectangleToggled?.Invoke(), false);       // Ctrl+Alt+R
                case 0x4F: return (() => CircleToggled?.Invoke(), false);          // Ctrl+Alt+O (Oval)
                case 0x58: return (() => TextToggled?.Invoke(), false);            // Ctrl+Alt+X
            }
            if (vk is >= 0x31 and <= 0x35) // Ctrl+Alt+1-5: Red, Blue, Green, White, Yellow
            {
                int color = (int)(vk - 0x30);
                return (() => ColorChanged?.Invoke(color), false);
            }
        }

        return null;
    }

    public void Dispose()
    {
        if (_kbHookId != IntPtr.Zero)
        {
            User32.UnhookWindowsHookEx(_kbHookId);
            _kbHookId = IntPtr.Zero;
        }
    }
}
