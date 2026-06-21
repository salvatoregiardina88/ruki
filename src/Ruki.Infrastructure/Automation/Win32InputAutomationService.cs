using System.Runtime.InteropServices;
using Ruki.Core.Automation;

namespace Ruki.Infrastructure.Automation;

/// <summary>
/// Implementazione di <see cref="IInputAutomationService"/> tramite l'input reale di Windows
/// (<c>SendInput</c>/<c>SetCursorPos</c>): il cursore si muove davvero e i tasti vengono premuti,
/// così l'utente può vedere e supervisionare ciò che l'agente fa.
/// </summary>
public sealed class Win32InputAutomationService : IInputAutomationService
{
    private const int WheelDelta = 120;

    public void MoveMouse(int x, int y)
    {
        // Movimento interpolato in piccoli passi: visibile e seguibile dall'utente.
        if (!GetCursorPos(out var start))
        {
            SetCursorPos(x, y);
            return;
        }

        const int steps = 12;
        for (var i = 1; i <= steps; i++)
        {
            var px = start.X + (x - start.X) * i / steps;
            var py = start.Y + (y - start.Y) * i / steps;
            SetCursorPos(px, py);
            Thread.Sleep(8);
        }
    }

    public void Click(int x, int y, MouseButton button = MouseButton.Left)
    {
        MoveMouse(x, y);
        var (down, up) = ButtonFlags(button);
        SendMouse(down);
        SendMouse(up);
    }

    public void DoubleClick(int x, int y, MouseButton button = MouseButton.Left)
    {
        Click(x, y, button);
        Thread.Sleep(60);
        var (down, up) = ButtonFlags(button);
        SendMouse(down);
        SendMouse(up);
    }

    public void Scroll(int x, int y, int notches)
    {
        MoveMouse(x, y);
        SendMouseWheel(notches * WheelDelta);
    }

    public void TypeText(string text)
    {
        foreach (var ch in text)
        {
            if (ch == '\n')
                PressVirtualKey(0x0D);   // Invio
            else
                SendUnicode(ch);
            Thread.Sleep(5);
        }
    }

    public void PressKeys(string combination)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(combination);

        var keys = combination.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(MapKey)
            .Where(vk => vk != 0)
            .ToArray();
        if (keys.Length == 0)
            return;

        // Premi i tasti nell'ordine indicato (i modificatori prima), poi rilasciali al contrario.
        foreach (var vk in keys)
            SendKey(vk, keyUp: false);
        for (var i = keys.Length - 1; i >= 0; i--)
            SendKey(keys[i], keyUp: true);
    }

    // ------------------------------------------------------------------ helper

    private static (uint Down, uint Up) ButtonFlags(MouseButton button) => button switch
    {
        MouseButton.Right => (MOUSEEVENTF_RIGHTDOWN, MOUSEEVENTF_RIGHTUP),
        MouseButton.Middle => (MOUSEEVENTF_MIDDLEDOWN, MOUSEEVENTF_MIDDLEUP),
        _ => (MOUSEEVENTF_LEFTDOWN, MOUSEEVENTF_LEFTUP),
    };

    private static void SendMouse(uint flags) => Send(new INPUT
    {
        type = INPUT_MOUSE,
        U = { mi = new MOUSEINPUT { dwFlags = flags } },
    });

    private static void SendMouseWheel(int amount) => Send(new INPUT
    {
        type = INPUT_MOUSE,
        U = { mi = new MOUSEINPUT { dwFlags = MOUSEEVENTF_WHEEL, mouseData = (uint)amount } },
    });

    private static void SendUnicode(char ch)
    {
        Send(new INPUT { type = INPUT_KEYBOARD, U = { ki = new KEYBDINPUT { wScan = ch, dwFlags = KEYEVENTF_UNICODE } } });
        Send(new INPUT { type = INPUT_KEYBOARD, U = { ki = new KEYBDINPUT { wScan = ch, dwFlags = KEYEVENTF_UNICODE | KEYEVENTF_KEYUP } } });
    }

    private static void PressVirtualKey(ushort vk)
    {
        SendKey(vk, keyUp: false);
        SendKey(vk, keyUp: true);
    }

    private static void SendKey(ushort vk, bool keyUp) => Send(new INPUT
    {
        type = INPUT_KEYBOARD,
        U = { ki = new KEYBDINPUT { wVk = vk, dwFlags = keyUp ? KEYEVENTF_KEYUP : 0 } },
    });

    private static void Send(INPUT input)
    {
        var inputs = new[] { input };
        SendInput(1, inputs, Marshal.SizeOf<INPUT>());
    }

    /// <summary>Mappa un token (es. "Ctrl", "Enter", "A", "F5") sul relativo virtual-key code.</summary>
    private static ushort MapKey(string token) => token.ToLowerInvariant() switch
    {
        "ctrl" or "control" => 0x11,
        "alt" => 0x12,
        "shift" => 0x10,
        "win" or "windows" or "meta" => 0x5B,
        "enter" or "return" => 0x0D,
        "tab" => 0x09,
        "esc" or "escape" => 0x1B,
        "space" => 0x20,
        "backspace" => 0x08,
        "delete" or "del" => 0x2E,
        "home" => 0x24,
        "end" => 0x23,
        "up" => 0x26,
        "down" => 0x28,
        "left" => 0x25,
        "right" => 0x27,
        { Length: 1 } single when char.IsLetterOrDigit(single[0]) => char.ToUpperInvariant(single[0]),
        _ when token.Length is 2 or 3 && (token[0] is 'f' or 'F') && int.TryParse(token[1..], out var n) && n is >= 1 and <= 12
            => (ushort)(0x70 + n - 1),   // F1..F12
        _ => 0,
    };

    // ------------------------------------------------------------------ P/Invoke

    private const uint INPUT_MOUSE = 0;
    private const uint INPUT_KEYBOARD = 1;
    private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    private const uint MOUSEEVENTF_LEFTUP = 0x0004;
    private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
    private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
    private const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
    private const uint MOUSEEVENTF_MIDDLEUP = 0x0040;
    private const uint MOUSEEVENTF_WHEEL = 0x0800;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint KEYEVENTF_UNICODE = 0x0004;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT { public uint type; public InputUnion U; }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public nuint dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public nuint dwExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out POINT point);
}
