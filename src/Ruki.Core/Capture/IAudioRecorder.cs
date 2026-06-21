namespace Ruki.Core.Capture;

/// <summary>Registra l'audio del microfono su file WAV.</summary>
public interface IAudioRecorder
{
    bool IsRecording { get; }

    /// <summary>
    /// Se true, il microfono è "mutato": invece dell'audio del mic viene registrato silenzio.
    /// Si scrive comunque silenzio (non si scartano i campioni) per non sfasare l'allineamento
    /// dell'audio col video. Può essere cambiato in qualsiasi momento durante la registrazione.
    /// </summary>
    bool IsMuted { get; set; }

    /// <summary>Avvia la registrazione sul file WAV indicato.</summary>
    void Start(string wavFilePath);

    /// <summary>Ferma la registrazione e chiude il file.</summary>
    void Stop();
}
