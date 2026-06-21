namespace Ruki.Core.Capture;

/// <summary>Tipo di evento del PC catturato durante l'addestramento.</summary>
public enum InputEventType
{
    MouseClick,
    MouseDoubleClick,
    MouseScroll,
    KeyDown,
    WindowChanged,
}

/// <summary>
/// Un evento del PC sulla timeline (click, scroll, tasto, cambio finestra). I campi non
/// pertinenti a un certo tipo restano <c>null</c>.
/// </summary>
public sealed record InputEvent(
    InputEventType Type,
    int? X = null,
    int? Y = null,
    string? Button = null,
    int? ScrollDelta = null,
    string? Key = null,
    string? ProcessName = null,
    string? WindowTitle = null);
