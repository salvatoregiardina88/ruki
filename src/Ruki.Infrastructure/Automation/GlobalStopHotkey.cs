using System.Runtime.InteropServices;
using Ruki.Core.Automation;

namespace Ruki.Infrastructure.Automation;

/// <summary>
/// Implementazione di <see cref="IGlobalStopHotkey"/> con un hook globale di tastiera
/// (<c>WH_KEYBOARD_LL</c>) che intercetta il tasto Esc indipendentemente dalla finestra attiva.
/// </summary>
public sealed class GlobalStopHotkey : IGlobalStopHotkey
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int VK_ESCAPE = 0x1B;

    private LowLevelKeyboardProc? _proc;   // tenuto vivo finché l'hook è installato
    private nint _hook;
    private Action? _onStop;

    public void Start(Action onStop)
    {
        if (_hook != 0)
            return;

        _onStop = onStop;
        _proc = Callback;
        _hook = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(null), 0);
    }

    public void Stop()
    {
        if (_hook != 0)
        {
            UnhookWindowsHookEx(_hook);
            _hook = 0;
        }
        _proc = null;
        _onStop = null;
    }

    private nint Callback(int nCode, nint wParam, nint lParam)
    {
        if (nCode >= 0 && (int)wParam == WM_KEYDOWN)
        {
            // Il primo campo di KBDLLHOOKSTRUCT è il virtual-key code.
            var vkCode = Marshal.ReadInt32(lParam);
            if (vkCode == VK_ESCAPE)
                _onStop?.Invoke();
        }

        return CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    private delegate nint LowLevelKeyboardProc(int nCode, nint wParam, nint lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, nint hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(nint hhk);

    [DllImport("user32.dll")]
    private static extern nint CallNextHookEx(nint hhk, int nCode, nint wParam, nint lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern nint GetModuleHandle(string? lpModuleName);
}
