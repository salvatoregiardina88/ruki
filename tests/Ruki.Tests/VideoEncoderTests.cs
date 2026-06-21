using System.Drawing;
using System.Drawing.Imaging;
using Microsoft.Extensions.Logging.Abstractions;
using Ruki.Core.Capture;
using Ruki.Infrastructure.Capture;
using Xunit;

namespace Ruki.Tests;

/// <summary>
/// Test di integrazione dell'encoder video: usa davvero l'encoder di Windows per produrre un MP4
/// da fotogrammi e audio generati al volo. Verifica la parte più "rischiosa" della Fase 4.
/// </summary>
public sealed class VideoEncoderTests : IDisposable
{
    private readonly string _dir;

    public VideoEncoderTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "ruki-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    [Fact]
    public async Task EncodeAsync_WithFramesAndAudio_ProducesMp4()
    {
        var frames = new List<TimedFrame>
        {
            new(CreateJpeg("0.jpg", Color.Red), TimeSpan.Zero),
            new(CreateJpeg("1.jpg", Color.Green), TimeSpan.FromSeconds(1)),
            new(CreateJpeg("2.jpg", Color.Blue), TimeSpan.FromSeconds(2)),
        };
        var audio = Path.Combine(_dir, "audio.wav");
        WriteSilenceWav(audio, seconds: 3);
        var output = Path.Combine(_dir, "out.mp4");

        var encoder = new MediaFoundationVideoEncoder(NullLogger<MediaFoundationVideoEncoder>.Instance);
        await encoder.EncodeAsync(frames, audio, output);

        Assert.True(File.Exists(output));
        Assert.True(new FileInfo(output).Length > 1000, "L'MP4 dovrebbe avere dimensione non banale.");
    }

    [Fact]
    public async Task EncodeAsync_WithoutAudio_ProducesMp4()
    {
        var frames = new List<TimedFrame>
        {
            new(CreateJpeg("a.jpg", Color.Black), TimeSpan.Zero),
            new(CreateJpeg("b.jpg", Color.White), TimeSpan.FromMilliseconds(500)),
        };
        var output = Path.Combine(_dir, "noaudio.mp4");

        var encoder = new MediaFoundationVideoEncoder(NullLogger<MediaFoundationVideoEncoder>.Instance);
        await encoder.EncodeAsync(frames, audioWavPath: null, output);

        Assert.True(File.Exists(output));
        Assert.True(new FileInfo(output).Length > 500);
    }

    private string CreateJpeg(string name, Color color)
    {
        var path = Path.Combine(_dir, name);
        using var bitmap = new Bitmap(320, 240);
        using (var graphics = Graphics.FromImage(bitmap))
            graphics.Clear(color);
        bitmap.Save(path, ImageFormat.Jpeg);
        return path;
    }

    /// <summary>Scrive un WAV PCM 16 bit mono di soli zeri (silenzio) della durata indicata.</summary>
    private static void WriteSilenceWav(string path, int seconds, int sampleRate = 16000)
    {
        var dataSize = seconds * sampleRate * 2; // 16 bit = 2 byte per campione

        using var writer = new BinaryWriter(File.Create(path));
        writer.Write("RIFF"u8.ToArray());
        writer.Write(36 + dataSize);
        writer.Write("WAVE"u8.ToArray());
        writer.Write("fmt "u8.ToArray());
        writer.Write(16);                 // dimensione chunk fmt
        writer.Write((short)1);           // PCM
        writer.Write((short)1);           // mono
        writer.Write(sampleRate);
        writer.Write(sampleRate * 2);     // byte rate
        writer.Write((short)2);           // block align
        writer.Write((short)16);          // bit per campione
        writer.Write("data"u8.ToArray());
        writer.Write(dataSize);
        writer.Write(new byte[dataSize]); // silenzio
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); }
        catch (IOException) { /* best effort */ }
    }
}
