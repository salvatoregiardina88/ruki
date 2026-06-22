using System.Text.Json;
using Ruki.Core.Llm;
using Xunit;

namespace Ruki.Tests;

public class JsonTextTests
{
    [Fact]
    public void RepairControlChars_escapes_raw_newlines_inside_strings_so_json_parses()
    {
        // Riproduce il caso reale: il modello scrive "thought" su più righe (a capo NON escapati),
        // il che rendeva il JSON non interpretabile e abortiva l'azione.
        var broken = "{\n  \"thought\": \"the click was wrong.\nLet's try again.\",\n  \"action\": \"click\",\n  \"x\": 213,\n  \"y\": 162\n}";

        var repaired = JsonText.RepairControlChars(broken);

        using var doc = JsonDocument.Parse(repaired);   // non deve lanciare
        Assert.Equal("click", doc.RootElement.GetProperty("action").GetString());
        Assert.Equal(213, doc.RootElement.GetProperty("x").GetInt32());
        Assert.Equal(162, doc.RootElement.GetProperty("y").GetInt32());
        Assert.Contains("try again", doc.RootElement.GetProperty("thought").GetString());
    }

    [Fact]
    public void RepairControlChars_preserves_already_escaped_sequences()
    {
        var alreadyValid = "{\"reply\":\"line1\\nline2\",\"actionGoal\":null}";

        var repaired = JsonText.RepairControlChars(alreadyValid);

        Assert.Equal(alreadyValid, repaired);   // niente doppio escape
        using var doc = JsonDocument.Parse(repaired);
        Assert.Equal("line1\nline2", doc.RootElement.GetProperty("reply").GetString());
    }

    [Fact]
    public void RepairControlChars_leaves_formatting_newlines_outside_strings_untouched()
    {
        var pretty = "{\n  \"action\": \"wait\"\n}";

        Assert.Equal(pretty, JsonText.RepairControlChars(pretty));
    }
}
