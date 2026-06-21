using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Ruki.Core.Capture;

namespace Ruki.Infrastructure.Capture;

/// <summary>
/// Cattura schermo tramite GDI (<see cref="Graphics.CopyFromScreen(int,int,int,int,Size)"/>),
/// con codifica JPEG. Niente dipendenze esterne: usa solo le API grafiche di Windows.
/// </summary>
public sealed class GdiScreenCaptureService : IScreenCaptureService
{
    private const int SM_CXSCREEN = 0;
    private const int SM_CYSCREEN = 1;

    // Encoder JPEG e parametro di qualità (riusati: la ricerca dell'encoder è costosa).
    private static readonly ImageCodecInfo JpegEncoder =
        ImageCodecInfo.GetImageEncoders().First(c => c.FormatID == ImageFormat.Jpeg.Guid);

    public CapturedFrame Capture(bool highlightCursor = false)
    {
        var width = GetSystemMetrics(SM_CXSCREEN);
        var height = GetSystemMetrics(SM_CYSCREEN);

        using var bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.CopyFromScreen(0, 0, 0, 0, new Size(width, height), CopyPixelOperation.SourceCopy);

            if (highlightCursor && GetCursorPos(out var cursor))
            {
                const int radius = 18;
                using var pen = new Pen(Color.FromArgb(230, 255, 40, 40), 3);
                graphics.DrawEllipse(pen, cursor.X - radius, cursor.Y - radius, radius * 2, radius * 2);
            }
        }

        using var stream = new MemoryStream();
        using var parameters = new EncoderParameters(1);
        parameters.Param[0] = new EncoderParameter(Encoder.Quality, 70L);
        bitmap.Save(stream, JpegEncoder, parameters);

        return new CapturedFrame(stream.ToArray(), width, height);
    }

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out Point point);
}
