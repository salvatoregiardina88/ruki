namespace Ruki.Core.Llm;

/// <summary>Chi ha prodotto un messaggio in una conversazione con il modello.</summary>
public enum ChatRole
{
    /// <summary>Messaggio scritto dall'utente.</summary>
    User,

    /// <summary>Messaggio prodotto dal modello/assistente.</summary>
    Assistant,
}

/// <summary>Un singolo turno di conversazione (ruolo + testo).</summary>
public sealed record ChatMessage(ChatRole Role, string Text);
