using System.Runtime.InteropServices;
using Ruki.Core.Automation;

namespace Ruki.Infrastructure.Automation;

/// <summary>
/// Implementazione di <see cref="IGlobalActionHotkeys"/> con un hook globale di tastiera
/// (<c>WH_KEYBOARD_LL</c>) che intercetta <c>Esc</c> (ferma) e <c>Barra spaziatrice</c> (pausa/riprendi)
/// indipendentemente dalla finestra attiva. Vengono considerati solo i tasti FISICI: quelli iniettati
/// dall'agente stesso (flag <c>LLKHF_INJECTED</c>) sono ignorati, così l'agente non si auto-interrompe
/// né si mette in pausa quando digita degli spazi. I due tasti di controllo vengono "consumati" (non
/// inoltrati all'app sottostante), per non lasciare input spuri nella finestra che l'agente sta usando.
/// </summary>
public sealed class GlobalActionHotkeys : IGlobalActionHotkeys
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int VK_ESCAPE = 0x1B;
    private const int VK_SPACE = 0x20;
    // Flag in KBDLLHOOKSTRUCT.flags: il tasto è stato INIETTATO (es. via SendInput) e non premuto fisicamente.
    private const int LLKHF_INJECTED = 0x10;
    private const int LLKHF_LOWER_IL_INJECTED = 0x02;

    private LowLevelKeyboardProc? _proc;   // tenuto vivo finché l'hook è installato
    private nint _hook;
    private Action? _onStop;
    private Action? _onTogglePause;

    public void Start(Action onStop, Action onTogglePause)
    {
        if (_hook != 0)
            return;

        _onStop = onStop;
        _onTogglePause = onTogglePause;
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
        _onTogglePause = null;
    }

    private nint Callback(int nCode, nint wParam, nint lParam)
    {
        if (nCode >= 0 && (int)wParam == WM_KEYDOWN)
        {
            // KBDLLHOOKSTRUCT: vkCode a offset 0, flags a offset 8.
            var vkCode = Marshal.ReadInt32(lParam);
            var flags = Marshal.ReadInt32(lParam + 8);
            var injected = (flags & (LLKHF_INJECTED | LLKHF_LOWER_IL_INJECTED)) != 0;

            // Solo i tasti FISICI dell'utente controllano l'esecuzione; quelli iniettati dall'agente no.
            if (!injected && (vkCode == VK_ESCAPE || vkCode == VK_SPACE))
            {
                if (vkCode == VK_ESCAPE)
                    _onStop?.Invoke();
                else
                    _onTogglePause?.Invoke();

                return 1;   // consuma il tasto: non lo inoltra alla finestra che l'agente sta usando
            }
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
