namespace Ruki.Core.Automation;

/// <summary>Pulsante del mouse.</summary>
public enum MouseButton
{
    Left,
    Right,
    Middle,
}

/// <summary>
/// Pilota mouse e tastiera reali (input "vero" del sistema), così l'utente vede ciò che l'agente fa
/// e può supervisionare. L'implementazione usa le API native di Windows.
/// </summary>
public interface IInputAutomationService
{
    /// <summary>Sposta il cursore alle coordinate schermo indicate (movimento visibile).</summary>
    void MoveMouse(int x, int y);

    /// <summary>Sposta e clicca alle coordinate indicate.</summary>
    void Click(int x, int y, MouseButton button = MouseButton.Left);

    /// <summary>Sposta e fa doppio click alle coordinate indicate.</summary>
    void DoubleClick(int x, int y, MouseButton button = MouseButton.Left);

    /// <summary>Scorre la rotellina alle coordinate indicate (positivo = su, negativo = giù).</summary>
    void Scroll(int x, int y, int notches);

    /// <summary>Digita il testo carattere per carattere (supporta Unicode).</summary>
    void TypeText(string text);

    /// <summary>Preme una combinazione di tasti, es. "Enter", "Ctrl+S", "Alt+Tab".</summary>
    void PressKeys(string combination);
}
