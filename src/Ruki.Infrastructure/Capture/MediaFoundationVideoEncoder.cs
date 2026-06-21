using Microsoft.Extensions.Logging;
using Ruki.Core.Capture;
using Windows.Media.Editing;
using Windows.Media.MediaProperties;
using Windows.Media.Transcoding;
using Windows.Storage;

namespace Ruki.Infrastructure.Capture;

/// <summary>
/// Implementazione di <see cref="IVideoEncoder"/> basata sull'encoder integrato in Windows
/// (Media Foundation, tramite l'API WinRT <see cref="MediaComposition"/>).
/// <para>
/// Produce un MP4 (H.264 + AAC) componendo i fotogrammi come "clip immagine" (ciascuno mostrato
/// per la durata che lo separa dal successivo) e sovrapponendo l'audio come traccia di sottofondo.
/// Nessuna dipendenza esterna né codec di terze parti: tutto fornito dal sistema operativo.
/// </para>
/// </summary>
public sealed class MediaFoundationVideoEncoder : IVideoEncoder
{
    // I contenuti sono quasi statici (screenshot a pochi fps): 10 fps bastano a rappresentare
    // tutti i frame, anche quelli "su evento", senza gonfiare il file.
    private const uint OutputFrameRate = 10;

    // Bitrate proporzionato a un contenuto "schermo" (molto comprimibile), in bit per
    // pixel-per-frame. Volutamente basso per file leggeri, ma sufficiente a mantenere
    // leggibile il testo (es. ~0,7 Mbps a 1080p, ~2,9 Mbps a 4K).
    private const double BitsPerPixelPerFrame = 0.035;
    private const uint MinBitrate = 700_000;     // ~0,7 Mbps
    private const uint MaxBitrate = 3_500_000;   // ~3,5 Mbps (per schermi molto grandi/4K)

    // Durata con cui mostrare l'ultimo fotogramma, che non ha un "successivo" da cui dedurla.
    private static readonly TimeSpan LastFrameDuration = TimeSpan.FromSeconds(1);

    private readonly ILogger<MediaFoundationVideoEncoder> _logger;

    public MediaFoundationVideoEncoder(ILogger<MediaFoundationVideoEncoder> logger) => _logger = logger;

    public async Task EncodeAsync(
        IReadOnlyList<TimedFrame> frames,
        string? audioWavPath,
        string outputPath,
        TimeSpan audioStartOffset = default,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(frames);
        if (frames.Count == 0)
            throw new ArgumentException("Nessun fotogramma da codificare.", nameof(frames));
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        var (width, height) = GetEvenDimensions(frames[0].JpegPath);

        // 1. Costruisci la timeline: una clip immagine per fotogramma.
        var composition = new MediaComposition();
        for (var i = 0; i < frames.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var duration = i < frames.Count - 1
                ? frames[i + 1].Offset - frames[i].Offset
                : LastFrameDuration;
            if (duration <= TimeSpan.Zero)
                duration = TimeSpan.FromMilliseconds(50);

            var imageFile = await StorageFile.GetFileFromPathAsync(frames[i].JpegPath).AsTask(cancellationToken);
            var clip = await MediaClip.CreateFromImageFileAsync(imageFile, duration).AsTask(cancellationToken);
            composition.Clips.Add(clip);
        }

        // 2. Aggiungi l'audio (se presente) come traccia di sottofondo, sincronizzata dall'inizio.
        if (!string.IsNullOrWhiteSpace(audioWavPath) && File.Exists(audioWavPath))
        {
            var audioFile = await StorageFile.GetFileFromPathAsync(audioWavPath).AsTask(cancellationToken);
            var audioTrack = await BackgroundAudioTrack.CreateFromFileAsync(audioFile).AsTask(cancellationToken);

            // Allinea l'audio ai fotogrammi: lo ritardiamo dell'istante in cui ha iniziato a registrare.
            if (audioStartOffset > TimeSpan.Zero)
                audioTrack.Delay = audioStartOffset;

            composition.BackgroundAudioTracks.Add(audioTrack);
        }

        // 3. Codifica su file MP4 con risoluzione pari a quella catturata.
        var outputFile = await CreateOutputFileAsync(outputPath, cancellationToken);
        var profile = BuildProfile(width, height);

        var result = await composition
            .RenderToFileAsync(outputFile, MediaTrimmingPreference.Fast, profile)
            .AsTask(cancellationToken);

        if (result != TranscodeFailureReason.None)
            throw new InvalidOperationException($"Codifica video fallita: {result}.");

        _logger.LogInformation(
            "Video creato: {Path} ({Frames} fotogrammi, {Width}x{Height}).",
            outputPath, frames.Count, width, height);
    }

    private static async Task<StorageFile> CreateOutputFileAsync(string outputPath, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(outputPath)
            ?? throw new ArgumentException("Percorso di output non valido.", nameof(outputPath));
        Directory.CreateDirectory(directory);

        var folder = await StorageFolder.GetFolderFromPathAsync(directory).AsTask(cancellationToken);
        return await folder
            .CreateFileAsync(Path.GetFileName(outputPath), CreationCollisionOption.ReplaceExisting)
            .AsTask(cancellationToken);
    }

    private static MediaEncodingProfile BuildProfile(uint width, uint height)
    {
        // Partiamo da un preset MP4 (H.264 + AAC) e adattiamo risoluzione, fps e bitrate:
        // - risoluzione = quella catturata, così il testo a schermo resta leggibile;
        // - fps e bitrate bassi, perché il contenuto è quasi statico → file molto più piccolo.
        var profile = MediaEncodingProfile.CreateMp4(VideoEncodingQuality.HD1080p);
        profile.Video.Width = width;
        profile.Video.Height = height;
        profile.Video.FrameRate.Numerator = OutputFrameRate;
        profile.Video.FrameRate.Denominator = 1;

        var bitrate = width * height * OutputFrameRate * BitsPerPixelPerFrame;
        profile.Video.Bitrate = (uint)Math.Clamp(bitrate, MinBitrate, MaxBitrate);

        return profile;
    }

    /// <summary>
    /// Dimensioni del primo fotogramma, arrotondate a numeri pari (richiesto da H.264).
    /// </summary>
    private static (uint Width, uint Height) GetEvenDimensions(string jpegPath)
    {
        using var image = System.Drawing.Image.FromFile(jpegPath);
        var width = (uint)(image.Width - (image.Width % 2));
        var height = (uint)(image.Height - (image.Height % 2));
        return (Math.Max(2, width), Math.Max(2, height));
    }
}
