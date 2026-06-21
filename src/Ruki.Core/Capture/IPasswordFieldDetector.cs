namespace Ruki.Core.Capture;

/// <summary>
/// Indica se il campo che ha attualmente il focus è un campo password. Serve a NON registrare
/// i tasti digitati nelle password durante l'addestramento.
/// <para>
/// L'implementazione usa l'accessibilità di Windows (UI Automation), perciò vive nel layer App
/// (dove sono disponibili quelle API). È attiva solo mentre si registra: <see cref="Start"/> /
/// <see cref="Stop"/> sono guidati dalla sorgente di eventi.
/// </para>
/// </summary>
public interface IPasswordFieldDetector
{
    /// <summary>True se l'elemento con il focus è un campo password.</summary>
    bool IsPasswordFieldFocused { get; }

    /// <summary>Inizia a monitorare i cambi di focus.</summary>
    void Start();

    /// <summary>Smette di monitorare e azzera lo stato.</summary>
    void Stop();
}
