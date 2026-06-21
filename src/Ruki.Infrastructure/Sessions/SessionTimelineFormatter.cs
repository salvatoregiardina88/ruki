using System.Text;
using System.Text.Json;

namespace Ruki.Infrastructure.Sessions;

/// <summary>
/// Trasforma gli artefatti di una sessione (<c>events.jsonl</c> e <c>chat.jsonl</c>) in una timeline
/// testuale leggibile, da affiancare al video nella richiesta al modello.
/// <para>
/// I tasti consecutivi vengono accorpati in una sola riga "Digitato: …" per non sommergere il modello
/// con un evento per carattere.
/// </para>
/// </summary>
internal static class SessionTimelineFormatter
{
    public static string Build(string sessionFolder)
    {
        var entries = new List<(long TimeMs, string Line)>();
        ReadEvents(Path.Combine(sessionFolder, "events.jsonl"), entries);
        ReadChat(Path.Combine(sessionFolder, "chat.jsonl"), entries);

        return string.Join("\n", entries.OrderBy(e => e.TimeMs).Select(e => e.Line));
    }

    private static void ReadEvents(string path, List<(long, string)> entries)
    {
        if (!File.Exists(path))
            return;

        var typedText = new StringBuilder();
        long typedStart = -1;

        void FlushTyped()
        {
            if (typedText.Length == 0)
                return;
            entries.Add((typedStart, $"{Timestamp(typedStart)} Typed: {typedText}"));
            typedText.Clear();
            typedStart = -1;
        }

        foreach (var line in File.ReadLines(path))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            var timeMs = root.GetProperty("tMs").GetInt64();
            var type = root.GetProperty("type").GetString();

            if (type == "KeyDown")
            {
                if (typedStart < 0)
                    typedStart = timeMs;
                typedText.Append(RenderKey(GetString(root, "key")));
                continue;
            }

            FlushTyped();
            entries.Add((timeMs, $"{Timestamp(timeMs)} {FormatEvent(type, root)}"));
        }

        FlushTyped();
    }

    private static void ReadChat(string path, List<(long, string)> entries)
    {
        if (!File.Exists(path))
            return;

        foreach (var line in File.ReadLines(path))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            var timeMs = root.GetProperty("tMs").GetInt64();
            var who = GetString(root, "role") == "User" ? "User" : "Ruki";
            var text = GetString(root, "text");
            entries.Add((timeMs, $"{Timestamp(timeMs)} {who} (chat): \"{text}\""));
        }
    }

    private static string FormatEvent(string? type, JsonElement root) => type switch
    {
        "MouseClick" => $"Click {GetString(root, "button")} at ({GetInt(root, "x")},{GetInt(root, "y")})",
        "MouseDoubleClick" => $"Double-click {GetString(root, "button")} at ({GetInt(root, "x")},{GetInt(root, "y")})",
        "MouseScroll" => $"Scroll {GetInt(root, "scrollDelta")} at ({GetInt(root, "x")},{GetInt(root, "y")})",
        "WindowChanged" => $"Foreground window: {GetString(root, "processName")} — \"{GetString(root, "windowTitle")}\"",
        _ => type ?? "event",
    };

    private static string RenderKey(string? key) => key switch
    {
        null or "" => string.Empty,
        "Space" => " ",
        "Enter" => "⏎",
        "Tab" => "\t",
        "Backspace" => "⌫",
        _ when key.Length == 1 => key,
        _ => $"[{key}]",
    };

    private static string Timestamp(long timeMs)
    {
        var time = TimeSpan.FromMilliseconds(timeMs);
        return $"[{(int)time.TotalMinutes:D2}:{time.Seconds:D2}.{time.Milliseconds / 100}]";
    }

    private static string? GetString(JsonElement root, string property)
        => root.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int? GetInt(JsonElement root, string property)
        => root.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetInt32()
            : null;
}
