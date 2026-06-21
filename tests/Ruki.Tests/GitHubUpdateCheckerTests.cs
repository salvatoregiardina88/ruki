using Ruki.Infrastructure.Update;
using Xunit;

namespace Ruki.Tests;

/// <summary>Test del confronto di versione usato dalla verifica aggiornamenti.</summary>
public class GitHubUpdateCheckerTests
{
    [Theory]
    [InlineData("v0.2.0", "0.1.0", true)]    // più recente, con "v"
    [InlineData("0.2.0", "0.1.0", true)]     // più recente, senza "v"
    [InlineData("v0.1.1", "0.1.0", true)]
    [InlineData("v0.1.0", "0.1.0", false)]   // stessa versione
    [InlineData("v0.1.0", "0.2.0", false)]   // più vecchia
    [InlineData("non-una-versione", "0.1.0", false)]   // tag non interpretabile
    public void IsNewerVersion_ComparesCorrectly(string tag, string current, bool expected)
        => Assert.Equal(expected, GitHubUpdateChecker.IsNewerVersion(tag, Version.Parse(current)));
}
