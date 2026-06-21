using Ruki.Infrastructure.Capture;
using Xunit;

namespace Ruki.Tests;

/// <summary>Test del mascheramento dei tasti nei campi password.</summary>
public class PasswordKeyMaskTests
{
    [Theory]
    [InlineData("A", true, "•")]        // carattere in campo password → mascherato
    [InlineData("5", true, "•")]
    [InlineData("Space", true, "•")]
    [InlineData("Backspace", true, "•")]
    [InlineData("Enter", true, "Enter")] // navigazione/invio → mantenuti
    [InlineData("Tab", true, "Tab")]
    [InlineData("Esc", true, "Esc")]
    [InlineData("A", false, "A")]        // fuori da un campo password → invariato
    [InlineData("Enter", false, "Enter")]
    public void Apply_MasksTypedKeysOnlyInPasswordField(string key, bool inPasswordField, string expected)
        => Assert.Equal(expected, PasswordKeyMask.Apply(key, inPasswordField));
}
