using Ruki.Core.Memory;

namespace Ruki.Tests;

/// <summary>Profilo utente finto: espone un profilo "attivo" configurabile e registra l'ultima nota ricordata.</summary>
internal sealed class FakeUserProfileMemory : IUserProfileMemory
{
    public string? Active;          // restituito da GetActiveProfile
    public string? LastRemembered;  // ultima nota passata a RememberAsync

    public string? GetActiveProfile() => Active;

    public Task RememberAsync(string durableFact, CancellationToken cancellationToken = default)
    {
        LastRemembered = durableFact;
        return Task.CompletedTask;
    }
}
