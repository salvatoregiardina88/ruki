namespace Ruki.App.ViewModels;

/// <summary>Tipo di "bolla" mostrata nella chat, usato per stile e allineamento.</summary>
public enum ChatBubbleKind
{
    User,
    Assistant,
    Error,
}

/// <summary>
/// Singolo messaggio mostrato nella chat. È un modello di sola visualizzazione: i booleani
/// <see cref="IsUser"/>/<see cref="IsError"/> servono ai trigger del XAML per colore e allineamento.
/// </summary>
public sealed record ChatBubble(string Text, ChatBubbleKind Kind)
{
    public bool IsUser => Kind == ChatBubbleKind.User;
    public bool IsError => Kind == ChatBubbleKind.Error;

    public static ChatBubble User(string text) => new(text, ChatBubbleKind.User);
    public static ChatBubble Assistant(string text) => new(text, ChatBubbleKind.Assistant);
    public static ChatBubble Error(string text) => new(text, ChatBubbleKind.Error);
}
