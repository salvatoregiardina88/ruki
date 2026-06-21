namespace Ruki.Core.Capture;

/// <summary>
/// Un fotogramma catturato e il suo istante sulla timeline della sessione (offset dall'inizio).
/// Il percorso punta al file JPEG su disco.
/// </summary>
public sealed record TimedFrame(string JpegPath, TimeSpan Offset);
