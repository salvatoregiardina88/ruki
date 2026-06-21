namespace Ruki.App.Services;

/// <summary>
/// Apre le finestre secondarie dell'app. Esiste per disaccoppiare i ViewModel dalle
/// finestre concrete (un ViewModel chiede "apri le impostazioni", non costruisce
/// <c>SettingsWindow</c>) e per garantire una sola istanza aperta per ciascun tipo.
/// </summary>
public interface IWindowService
{
    /// <summary>Apre (o porta in primo piano) la finestra della chat.</summary>
    void ShowChat();

    /// <summary>Apre (o porta in primo piano) la finestra delle impostazioni.</summary>
    void ShowSettings();

    /// <summary>Apre (o porta in primo piano) la finestra di debug dell'Action Agent.</summary>
    void ShowActionDebug();
}
