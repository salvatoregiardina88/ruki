using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Ruki.Core.Automation;

namespace Ruki.Infrastructure.Automation;

/// <summary>
/// Implementazione di <see cref="IForegroundWindowService"/> tramite le API Win32
/// (<c>GetForegroundWindow</c> + titolo e nome processo).
/// </summary>
public sealed class Win32ForegroundWindowService : IForegroundWindowService
{
    public ForegroundWindowInfo? GetForeground()
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == 0)
            return null;

        var title = GetWindowTitle(hwnd);
        var process = GetProcessName(hwnd);
        return new ForegroundWindowInfo(process ?? string.Empty, title);
    }

    public IReadOnlyList<ForegroundWindowInfo> GetOpenWindows()
    {
        var windows = new List<ForegroundWindowInfo>();
        var seen = new HashSet<string>();

        EnumWindows((hwnd, _) =>
        {
            if (!IsWindowVisible(hwnd))
                return true;

            var title = GetWindowTitle(hwnd);
            if (string.IsNullOrWhiteSpace(title))
                return true;   // ignoriamo le finestre senza titolo (sfondo, system tray…)

            var process = GetProcessName(hwnd) ?? string.Empty;
            if (seen.Add($"{process}|{title}"))
                windows.Add(new ForegroundWindowInfo(process, title));

            return true;
        }, 0);

        return windows;
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
            return null;
        }
    }

    private delegate bool EnumWindowsProc(nint hWnd, nint lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, nint lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(nint hWnd);

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(nint hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint hWnd, out uint lpdwProcessId);
}
