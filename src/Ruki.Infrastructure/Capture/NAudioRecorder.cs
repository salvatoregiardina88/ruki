using Microsoft.Extensions.Logging;
using NAudio.Wave;
using Ruki.Core.Capture;

namespace Ruki.Infrastructure.Capture;

/// <summary>
/// Registra il microfono su file WAV tramite NAudio. Formato 16 kHz mono 16-bit: adatto al
/// parlato e leggero da inviare al modello.
/// </summary>
public sealed class NAudioRecorder : IAudioRecorder
{
    private readonly object _gate = new();
    private readonly ILogger<NAudioRecorder> _logger;

    private WaveInEvent? _waveIn;
    private WaveFileWriter? _writer;

    public NAudioRecorder(ILogger<NAudioRecorder> logger) => _logger = logger;

    public bool IsRecording { get; private set; }

    /// <summary>Quando true, registriamo silenzio al posto dell'audio del microfono.</summary>
    public bool IsMuted { get; set; }

    public void Start(string wavFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(wavFilePath);

        lock (_gate)
        {
            if (IsRecording)
                return;

            Directory.CreateDirectory(Path.GetDirectoryName(wavFilePath)!);

            _waveIn = new WaveInEvent { WaveFormat = new WaveFormat(16000, 16, 1) };
            _writer = new WaveFileWriter(wavFilePath, _waveIn.WaveFormat);
            _waveIn.DataAvailable += OnDataAvailable;

            _waveIn.StartRecording();
            IsRecording = true;
            _logger.LogInformation("Registrazione audio avviata: {Path}.", wavFilePath);
        }
    }

    public void Stop()
    {
        lock (_gate)
        {
            if (!IsRecording)
                return;

            IsRecording = false;
            _waveIn?.StopRecording();
            _waveIn?.Dispose();
            _waveIn = null;

            _writer?.Dispose();   // chiude e finalizza l'header del WAV
            _writer = null;
            _logger.LogInformation("Registrazione audio fermata.");
        }
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        lock (_gate)
        {
            if (_writer is null)
                return;

            if (IsMuted)
                // Silenzio (zeri) della stessa lunghezza: il file mantiene la durata reale, così
                // l'audio resta allineato al video anche dopo un tratto in muto.
                _writer.Write(new byte[e.BytesRecorded], 0, e.BytesRecorded);
            else
                _writer.Write(e.Buffer, 0, e.BytesRecorded);
        }
    }
}
