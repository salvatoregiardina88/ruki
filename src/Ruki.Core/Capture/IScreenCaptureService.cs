namespace Ruki.Core.Capture;

/// <summary>Cattura lo schermo come immagine JPEG.</summary>
public interface IScreenCaptureService
{
    /// <summary>
    /// Cattura lo schermo principale. Se <paramref name="highlightCursor"/> è true, disegna
    /// un evidenziatore sulla posizione corrente del cursore (utile per i frame "su evento").
    /// </summary>
    CapturedFrame Capture(bool highlightCursor = false);
}
