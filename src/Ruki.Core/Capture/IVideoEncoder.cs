namespace Ruki.Core.Capture;

/// <summary>
/// Compone i fotogrammi catturati e l'audio in un unico file video.
/// L'implementazione usa l'encoder integrato nel sistema operativo (license-safe).
/// </summary>
public interface IVideoEncoder
{
    /// <summary>
    /// Crea un MP4 in <paramref name="outputPath"/> mostrando ogni fotogramma per la durata
    /// che lo separa dal successivo, con l'eventuale audio (WAV) sincronizzato dall'inizio.
    /// </summary>
    /// <param name="frames">Fotogrammi ordinati per offset crescente.</param>
    /// <param name="audioWavPath">File audio WAV da incorporare, oppure <c>null</c>.</param>
    /// <param name="outputPath">Percorso del file MP4 da generare.</param>
    /// <param name="audioStartOffset">
    /// Istante (sulla timeline della sessione) in cui l'audio ha iniziato a registrare: viene
    /// usato come ritardo della traccia audio per allinearla ai fotogrammi.
    /// </param>
    Task EncodeAsync(
        IReadOnlyList<TimedFrame> frames,
        string? audioWavPath,
        string outputPath,
        TimeSpan audioStartOffset = default,
        CancellationToken cancellationToken = default);
}
