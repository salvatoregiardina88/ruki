using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using Ruki.App.Localization;
using Ruki.Core.Agents;

namespace Ruki.App.ViewModels;

/// <summary>Una voce della conversazione di debug con l'Action Agent (testo + eventuale immagine).</summary>
public sealed record ActionTraceItem(int Step, bool IsSent, string Text, ImageSource? Image)
{
    /// <summary>Intestazione mostrata: passo + direzione (localizzata).</summary>
    public string Header => Step <= 0
        ? (IsSent ? Loc.T("Debug_SystemInstructions") : Loc.T("Debug_AgentReply"))
        : Loc.T("Debug_Step", Step, IsSent ? Loc.T("Debug_SentToModel") : Loc.T("Debug_AgentReply"));
}

/// <summary>
/// ViewModel della finestra di debug: ascolta l'<see cref="IActionTrace"/> e mostra, passo per passo,
/// cosa inviamo al modello (testo + screenshot) e cosa risponde, disegnando un cerchio sulle
/// coordinate indicate dall'agente.
/// </summary>
public sealed partial class ActionDebugViewModel : ObservableObject
{
    private const int MaxEntries = 40;

    private readonly IActionTrace _trace;

    public ObservableCollection<ActionTraceItem> Entries { get; } = [];

    public ActionDebugViewModel(IActionTrace trace)
    {
        _trace = trace;
        _trace.EntryAdded += OnEntryAdded;
        _trace.Cleared += OnCleared;
    }

    /// <summary>Da chiamare alla chiusura della finestra per non lasciare sottoscrizioni appese.</summary>
    public void Detach()
    {
        _trace.EntryAdded -= OnEntryAdded;
        _trace.Cleared -= OnCleared;
    }

    private void OnEntryAdded(ActionTraceEntry entry)
    {
        // Costruiamo l'immagine sul thread UI (RenderTargetBitmap lo richiede).
        OnUi(() =>
        {
            Entries.Add(new ActionTraceItem(
                entry.Step,
                entry.Kind == ActionTraceKind.Sent,
                entry.Text,
                BuildImage(entry.ImageJpeg, entry.X, entry.Y)));

            while (Entries.Count > MaxEntries)
                Entries.RemoveAt(0);
        });
    }

    private void OnCleared() => OnUi(Entries.Clear);

    /// <summary>Carica il JPEG e, se sono indicate delle coordinate, ci disegna sopra un cerchio rosso.</summary>
    private static ImageSource? BuildImage(byte[]? jpeg, int? x, int? y)
    {
        if (jpeg is null || jpeg.Length == 0)
            return null;

        var bitmap = new BitmapImage();
        using (var stream = new MemoryStream(jpeg))
        {
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
        }
        bitmap.Freeze();

        if (x is null || y is null)
            return bitmap;

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawImage(bitmap, new Rect(0, 0, bitmap.PixelWidth, bitmap.PixelHeight));
            var pen = new Pen(Brushes.Red, Math.Max(3.0, bitmap.PixelWidth / 250.0));
            dc.DrawEllipse(null, pen, new Point(x.Value, y.Value), 24, 24);
        }

        var rendered = new RenderTargetBitmap(bitmap.PixelWidth, bitmap.PixelHeight, 96, 96, PixelFormats.Pbgra32);
        rendered.Render(visual);
        rendered.Freeze();
        return rendered;
    }

    private static void OnUi(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
            return;
        if (dispatcher.CheckAccess())
            action();
        else
            dispatcher.InvokeAsync(action);
    }
}
