namespace Ruki.Core.Automation;

/// <summary>Informazioni sulla finestra attualmente in primo piano.</summary>
public sealed record ForegroundWindowInfo(string ProcessName, string Title);

/// <summary>Fornisce informazioni sulle finestre, per dare contesto all'agente e verificare il focus.</summary>
public interface IForegroundWindowService
{
    /// <summary>Finestra attualmente in primo piano, oppure <c>null</c> se non determinabile.</summary>
    ForegroundWindowInfo? GetForeground();

    /// <summary>Finestre top-level visibili e con titolo, attualmente aperte.</summary>
    IReadOnlyList<ForegroundWindowInfo> GetOpenWindows();
}
