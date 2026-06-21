using Ruki.Core.Localization;
using Xunit;

namespace Ruki.Tests;

/// <summary>Test del localizzatore di Core (messaggi LLM mostrati all'utente).</summary>
public class LocalizerTests
{
    [Fact]
    public void T_ReturnsMessageInCurrentLanguage()
    {
        var original = Localizer.Language;
        try
        {
            Localizer.Language = "it";
            Assert.Contains("Chiave API", Localizer.T("Llm_NoApiKey"));

            Localizer.Language = "en";
            Assert.Contains("API key", Localizer.T("Llm_NoApiKey"));
            Assert.Equal("Error from the Gemini model (503): busy", Localizer.T("Llm_ApiError", 503, "busy"));
        }
        finally
        {
            Localizer.Language = original;
        }
    }
}
