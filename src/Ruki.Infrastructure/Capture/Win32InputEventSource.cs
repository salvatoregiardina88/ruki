using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Logging;
using Ruki.Core.Capture;

namespace Ruki.Infrastructure.Capture;

/// <summary>
/// Sorgente di eventi del PC basata sugli hook globali di Windows:
/// <list type="bullet">
///   <item><c>WH_MOUSE_LL</c> per click e scroll;</item>
///   <item><c>WH_KEYBOARD_LL</c> per i tasti premuti;</item>
///   <item><c>SetWinEventHook</c> per il cambio di finestra in primo piano.</item>
/// </list>
/// <para>
/// Gli hook low-level richiedono un message loop sul thread che li installa: va quindi
/// avviata dal thread UI. I callback restano volutamente brevi (sollevano solo l'evento);
/// il lavoro pesante lo fa chi ascolta.
/// </para>
/// </summary>
public sealed class Win32InputEventSource : IInputEventSource
{
    public event Action<InputEvent>? EventCaptured;

    private readonly ILogger<Win32InputEventSource> _logger;
    private readonly IPasswordFieldDetector _passwordDetector;

    // I delegati vanno tenuti vivi finché gli hook sono installati, altrimenti il GC li
    // raccoglie e il callback nativo va in crash.
    private HookProc? _mouseProc;
    private HookProc? _keyboardProc;
    private WinEventProc? _winEventProc;

    private nint _mouseHook;
    private nint _keyboardHook;
    private nint _winEventHook;

    public Win32InputEventSource(IPasswordFieldDetector passwordDetector, ILogger<Win32InputEventSource> logger)
    {
        _passwordDetector = passwordDetector;
        _logger = logger;
    }

    public void Start()
    {
        if (_mouseHook != 0)
            return;

        _mouseProc = MouseCallback;
        _keyboardProc = KeyboardCallback;
        _winEventProc = WinEventCallback;

        var module = GetModuleHandle(null);
        _mouseHook = SetWindowsHookEx(WH_MOUSE_LL, _mouseProc, module, 0);
        _keyboardHook = SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardProc, module, 0);
        _winEventHook = SetWinEventHook(
            EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND,
            0, _winEventProc, 0, 0, WINEVENT_OUTOFCONTEXT);

        if (_mouseHook == 0 || _keyboardHook == 0)
            _logger.LogWarning("Installazione hook input parzialmente fallita (mouse={Mouse}, tastiera={Kbd}).",
                _mouseHook != 0, _keyboardHook != 0);

        // Inizia a riconoscere i campi password, per non registrarne il contenuto.
        _passwordDetector.Start();
    }

    public void Stop()
    {
        if (_mouseHook != 0) { UnhookWindowsHookEx(_mouseHook); _mouseHook = 0; }
        if (_keyboardHook != 0) { UnhookWindowsHookEx(_keyboardHook); _keyboardHook = 0; }
        if (_winEventHook != 0) { UnhookWinEvent(_winEventHook); _winEventHook = 0; }

        _mouseProc = null;
        _keyboardProc = null;
        _winEventProc = null;

        _passwordDetector.Stop();
    }

    private void Raise(InputEvent inputEvent) => EventCaptured?.Invoke(inputEvent);

    private nint MouseCallback(int nCode, nint wParam, nint lParam)
    {
        if (nCode >= 0)
        {
            var data = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
            switch ((int)wParam)
            {
                case WM_LBUTTONDOWN:
                    Raise(new InputEvent(InputEventType.MouseClick, data.pt.X, data.pt.Y, "Left"));
                    break;
                case WM_RBUTTONDOWN:
                    Raise(new InputEvent(InputEventType.MouseClick, data.pt.X, data.pt.Y, "Right"));
                    break;
                case WM_MBUTTONDOWN:
                    Raise(new InputEvent(InputEventType.MouseClick, data.pt.X, data.pt.Y, "Middle"));
                    break;
                case WM_LBUTTONDBLCLK:
                    Raise(new InputEvent(InputEventType.MouseDoubleClick, data.pt.X, data.pt.Y, "Left"));
                    break;
                case WM_MOUSEWHEEL:
                    var delta = (short)((data.mouseData >> 16) & 0xFFFF);
                    Raise(new InputEvent(InputEventType.MouseScroll, data.pt.X, data.pt.Y, ScrollDelta: delta));
                    break;
            }
        }

        return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
    }

    private nint KeyboardCallback(int nCode, nint wParam, nint lParam)
    {
        if (nCode >= 0 && ((int)wParam == WM_KEYDOWN || (int)wParam == WM_SYSKEYDOWN))
        {
            var data = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            // In un campo password registriamo solo "•": la password non finisce mai su disco né a Gemini.
            var key = PasswordKeyMask.Apply(KeyName(data.vkCode), _passwordDetector.IsPasswordFieldFocused);
            Raise(new InputEvent(InputEventType.KeyDown, Key: key));
        }

        return CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
    }

    private void WinEventCallback(nint hWinEventHook, uint eventType, nint hwnd,
        int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        if (hwnd == 0)
            return;

        var title = GetWindowTitle(hwnd);
        var process = GetProcessName(hwnd);
        Raise(new InputEvent(InputEventType.WindowChanged, ProcessName: process, WindowTitle: title));
    }

    private static string GetWindowTitle(nint hwnd)
    {
        var buffer = new StringBuilder(256);
        GetWindowText(hwnd, buffer, buffer.Capacity);
        return buffer.ToString();
    }

    private static string? GetProcessName(nint hwnd)
    {
        _ = GetWindowThreadProcessId(hwnd, out var processId);
        try
        {
            return Process.GetProcessById((int)processId).ProcessName;
        }
        catch (ArgumentException)
        {
            return null;   // il processo potrebbe essere già terminato
        }
    }

    /// <summary>Nome leggibile del tasto a partire dal virtual-key code.</summary>
    private static string KeyName(uint vk) => vk switch
    {
        >= 0x30 and <= 0x39 => ((char)vk).ToString(),          // 0-9
        >= 0x41 and <= 0x5A => ((char)vk).ToString(),          // A-Z
        0x20 => "Space",
        0x0D => "Enter",
        0x08 => "Backspace",
        0x09 => "Tab",
        0x1B => "Esc",
        _ => $"VK_{vk}",
    };

    // ------------------------------- P/Invoke -------------------------------

    private const int WH_KEYBOARD_LL = 13;
    private const int WH_MOUSE_LL = 14;
    private const int WM_LBUTTONDOWN = 0x0201;
    private const int WM_LBUTTONDBLCLK = 0x0203;
    private const int WM_RBUTTONDOWN = 0x0204;
    private const int WM_MBUTTONDOWN = 0x0207;
    private const int WM_MOUSEWHEEL = 0x020A;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
    private const uint WINEVENT_OUTOFCONTEXT = 0x0000;

    private delegate nint HookProc(int nCode, nint wParam, nint lParam);

    private delegate void WinEventProc(nint hWinEventHook, uint eventType, nint hwnd,
        int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSLLHOOKSTRUCT
    {
        public POINT pt;
        public uint mouseData;
        public uint flags;
        public uint time;
        public nuint dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public nuint dwExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetWindowsHookEx(int idHook, HookProc lpfn, nint hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(nint hhk);

    [DllImport("user32.dll")]
    private static extern nint CallNextHookEx(nint hhk, int nCode, nint wParam, nint lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern nint GetModuleHandle(string? lpModuleName);

    [DllImport("user32.dll")]
    private static extern nint SetWinEventHook(uint eventMin, uint eventMax, nint hmodWinEventProc,
        WinEventProc lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWinEvent(nint hWinEventHook);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(nint hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint hWnd, out uint lpdwProcessId);
}
