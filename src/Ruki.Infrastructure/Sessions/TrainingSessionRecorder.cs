using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Ruki.Core.Abstractions;
using Ruki.Core.Capture;
using Ruki.Core.Llm;
using Ruki.Core.Training;
using Ruki.Infrastructure.Storage;

namespace Ruki.Infrastructure.Sessions;

/// <summary>
/// Implementazione di <see cref="ITrainingSessionRecorder"/>: mette insieme cattura schermo,
/// audio ed eventi su una timeline, scrive tutto su disco in modo incrementale e, allo stop,
/// produce il video MP4 con audio.
/// </summary>
public sealed class TrainingSessionRecorder : ITrainingSessionRecorder
{
    private static readonly JsonSerializerOptions JsonLine = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly JsonSerializerOptions JsonIndented = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private readonly IScreenCaptureService _screen;
    private readonly IAudioRecorder _audio;
    private readonly IInputEventSource _input;
    private readonly IVideoEncoder _encoder;
    private readonly ISettingsService _settings;
    private readonly ILogger<TrainingSessionRecorder> _logger;

    // Sincronizzazione tra i vari thread (timer dei frame, callback degli hook, thread UI).
    private readonly object _framesGate = new();
    private readonly object _writeGate = new();
    private readonly object _captureGate = new();

    private readonly List<TimedFrame> _frames = [];

    private volatile bool _isRecording;
    private string _id = string.Empty;
    private string _sessionDir = string.Empty;
    private string _framesDir = string.Empty;
    private string? _audioPath;
    private TimeSpan _audioStartOffset;
    private DateTimeOffset _startedAt;
    private Stopwatch? _stopwatch;
    private Timer? _frameTimer;
    private Timer? _maxTimer;
    private StreamWriter? _eventsWriter;
    private StreamWriter? _chatWriter;
    private int _frameSeq;
    private int _eventCount;
    private int _screenWidth;
    private int _screenHeight;

    public TrainingSessionRecorder(
        IScreenCaptureService screen,
        IAudioRecorder audio,
        IInputEventSource input,
        IVideoEncoder encoder,
        ISettingsService settings,
        ILogger<TrainingSessionRecorder> logger)
    {
        _screen = screen;
        _audio = audio;
        _input = input;
        _encoder = encoder;
        _settings = settings;
        _logger = logger;
    }

    public bool IsRecording => _isRecording;

    /// <summary>Muto del microfono: inoltrato al recorder audio (che registra silenzio quando attivo).</summary>
    public bool IsMicMuted
    {
        get => _audio.IsMuted;
        set => _audio.IsMuted = value;
    }

    public TimeSpan Elapsed => _stopwatch?.Elapsed ?? TimeSpan.Zero;

    public event Action? MaxDurationReached;

    public void Start()
    {
        if (_isRecording)
            return;

        var settings = _settings.Current;

        _id = DateTimeOffset.Now.ToString("yyyyMMdd_HHmmss");
        _sessionDir = Path.Combine(RukiPaths.SessionsDirectory, _id);
        _framesDir = Path.Combine(_sessionDir, "frames");
        Directory.CreateDirectory(_framesDir);

        lock (_framesGate) _frames.Clear();
        _frameSeq = 0;
        _eventCount = 0;
        _screenWidth = _screenHeight = 0;
        _startedAt = DateTimeOffset.Now;

        // AutoFlush: i dati finiscono su disco subito, così un crash non perde la sessione.
        _eventsWriter = new StreamWriter(Path.Combine(_sessionDir, "events.jsonl")) { AutoFlush = true };
        _chatWriter = new StreamWriter(Path.Combine(_sessionDir, "chat.jsonl")) { AutoFlush = true };

        _stopwatch = Stopwatch.StartNew();
        _isRecording = true;

        // Audio best-effort: un microfono assente non deve bloccare la sessione.
        // Ogni nuova sessione parte col microfono acceso (annulla un eventuale muto precedente).
        _audio.IsMuted = false;
        _audioPath = Path.Combine(_sessionDir, "audio.wav");
        try
        {
            _audio.Start(_audioPath);
            // Istante in cui l'audio è effettivamente partito: serve ad allinearlo ai frame.
            _audioStartOffset = _stopwatch.Elapsed;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Avvio audio fallito: la sessione continua senza audio.");
            _audioPath = null;
            _audioStartOffset = TimeSpan.Zero;
        }

        _input.EventCaptured += OnInputEvent;
        _input.Start();

        var interval = TimeSpan.FromSeconds(1.0 / settings.ScreenCaptureFps);
        _frameTimer = new Timer(_ => CaptureAndStoreFrame(highlightCursor: false), null, TimeSpan.Zero, interval);

        var maxDuration = TimeSpan.FromMinutes(settings.MaxSessionMinutes);
        _maxTimer = new Timer(_ => MaxDurationReached?.Invoke(), null, maxDuration, Timeout.InfiniteTimeSpan);

        _logger.LogInformation("Sessione di addestramento avviata: {Id}.", _id);
    }

    public void NoteChatMessage(ChatRole role, string text)
    {
        if (!_isRecording)
            return;

        var tMs = (long)_stopwatch!.Elapsed.TotalMilliseconds;
        WriteLine(_chatWriter, JsonSerializer.Serialize(new { tMs, role = role.ToString(), text }, JsonLine));
    }

    public async Task<TrainingSessionInfo> StopAsync(CancellationToken cancellationToken = default)
    {
        if (!_isRecording)
            throw new InvalidOperationException("Nessuna sessione in corso.");

        _isRecording = false;

        // Ferma i timer e attendi che i callback in volo terminino, poi gli hook e l'audio.
        if (_frameTimer is not null) { await _frameTimer.DisposeAsync(); _frameTimer = null; }
        if (_maxTimer is not null) { await _maxTimer.DisposeAsync(); _maxTimer = null; }

        _input.EventCaptured -= OnInputEvent;
        _input.Stop();
        _audio.Stop();

        _stopwatch!.Stop();
        var duration = _stopwatch.Elapsed;

        _eventsWriter?.Dispose();
        _eventsWriter = null;
        _chatWriter?.Dispose();
        _chatWriter = null;

        List<TimedFrame> frames;
        lock (_framesGate)
            frames = _frames.OrderBy(f => f.Offset).ToList();

        string? videoPath = null;
        if (frames.Count > 0)
        {
            videoPath = Path.Combine(_sessionDir, "video.mp4");
            try
            {
                await _encoder.EncodeAsync(frames, _audioPath, videoPath, _audioStartOffset, cancellationToken);

                // Video creato e verificato: i singoli JPEG non servono più (sono nel video),
                // quindi liberiamo subito lo spazio. Se invece il video non è valido, li teniamo.
                if (IsVideoValid(videoPath))
                    TryDeleteFramesFolder();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Codifica video fallita: i frame restano comunque su disco.");
                videoPath = null;
            }
        }

        WriteManifest(duration, frames.Count);

        _logger.LogInformation("Sessione {Id} terminata: {Frames} frame, {Events} eventi, durata {Duration}.",
            _id, frames.Count, _eventCount, duration);

        return new TrainingSessionInfo(_id, _sessionDir, videoPath, duration, frames.Count, _eventCount);
    }

    public void Abort() => StopCapture("interrotta alla chiusura (video non codificato)");

    public void Discard()
    {
        if (!_isRecording)
            return;

        var dir = _sessionDir;
        StopCapture("annullata dall'utente");

        // Elimina tutti i dati della sessione registrati finora: niente apprendimento.
        try
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "Eliminazione della sessione annullata fallita.");
        }
    }

    /// <summary>
    /// Ferma in modo sincrono cattura/hook/audio e chiude i file SENZA codificare il video.
    /// Stesso ordine dello stop normale (prima si fermano le sorgenti, poi si chiudono i file).
    /// </summary>
    private void StopCapture(string reason)
    {
        if (!_isRecording)
            return;

        _isRecording = false;

        _frameTimer?.Dispose();
        _frameTimer = null;
        _maxTimer?.Dispose();
        _maxTimer = null;

        _input.EventCaptured -= OnInputEvent;
        _input.Stop();
        _audio.Stop();
        _stopwatch?.Stop();

        _eventsWriter?.Dispose();
        _eventsWriter = null;
        _chatWriter?.Dispose();
        _chatWriter = null;

        _logger.LogInformation("Sessione {Id} {Reason}.", _id, reason);
    }

    private void OnInputEvent(InputEvent inputEvent)
    {
        if (!_isRecording)
            return;

        var tMs = (long)_stopwatch!.Elapsed.TotalMilliseconds;
        WriteLine(_eventsWriter, JsonSerializer.Serialize(new
        {
            tMs,
            type = inputEvent.Type.ToString(),
            inputEvent.X,
            inputEvent.Y,
            inputEvent.Button,
            inputEvent.ScrollDelta,
            inputEvent.Key,
            inputEvent.ProcessName,
            inputEvent.WindowTitle,
        }, JsonLine));
        Interlocked.Increment(ref _eventCount);

        // Frame "su evento" per i momenti visivamente rilevanti (non per ogni tasto digitato).
        // Offload su thread pool: il callback dell'hook deve restare velocissimo.
        if (inputEvent.Type is InputEventType.MouseClick or InputEventType.MouseDoubleClick or InputEventType.WindowChanged)
            Task.Run(() => CaptureAndStoreFrame(highlightCursor: true));
    }

    private void CaptureAndStoreFrame(bool highlightCursor)
    {
        if (!_isRecording)
            return;

        try
        {
            var offset = _stopwatch!.Elapsed;

            // Serializziamo le catture GDI per non farle accavallare tra timer ed eventi.
            CapturedFrame frame;
            lock (_captureGate)
                frame = _screen.Capture(highlightCursor);

            var sequence = Interlocked.Increment(ref _frameSeq);
            var path = Path.Combine(_framesDir, $"frame_{sequence:D6}.jpg");
            File.WriteAllBytes(path, frame.JpegBytes);

            lock (_framesGate)
            {
                _frames.Add(new TimedFrame(path, offset));
                _screenWidth = frame.Width;
                _screenHeight = frame.Height;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cattura frame fallita.");
        }
    }

    private void WriteLine(StreamWriter? writer, string line)
    {
        if (writer is null)
            return;
        lock (_writeGate)
            writer.WriteLine(line);
    }

    /// <summary>Considera valido un video esistente e di dimensione non banale.</summary>
    private static bool IsVideoValid(string videoPath)
        => File.Exists(videoPath) && new FileInfo(videoPath).Length > 10_000;

    private void TryDeleteFramesFolder()
    {
        try
        {
            if (Directory.Exists(_framesDir))
                Directory.Delete(_framesDir, recursive: true);
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "Eliminazione della cartella frames fallita.");
        }
    }

    private void WriteManifest(TimeSpan duration, int frameCount)
    {
        var manifest = new
        {
            id = _id,
            startedAt = _startedAt,
            endedAt = DateTimeOffset.Now,
            durationMs = (long)duration.TotalMilliseconds,
            frameCount,
            eventCount = _eventCount,
            screenWidth = _screenWidth,
            screenHeight = _screenHeight,
            fps = _settings.Current.ScreenCaptureFps,
        };
        File.WriteAllText(Path.Combine(_sessionDir, "manifest.json"), JsonSerializer.Serialize(manifest, JsonIndented));
    }
}
