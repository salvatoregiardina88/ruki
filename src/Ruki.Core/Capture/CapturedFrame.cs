namespace Ruki.Core.Capture;

/// <summary>Un fotogramma appena catturato dallo schermo, già codificato in JPEG.</summary>
public sealed record CapturedFrame(byte[] JpegBytes, int Width, int Height);
