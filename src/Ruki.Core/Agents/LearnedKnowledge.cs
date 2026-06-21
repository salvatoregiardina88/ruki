namespace Ruki.Core.Agents;

/// <summary>
/// Una singola memoria estratta da una sessione: può essere una procedura (come si fa qualcosa)
/// oppure una nozione (un fatto generale riutilizzabile sull'utente e sul suo ambiente).
/// </summary>
public sealed record LearnedKnowledge(
    /// <summary>Titolo breve della memoria (es. "Registrazione di una fattura in Excel").</summary>
    string Title,
    /// <summary>Riassunto in una riga, usato per la navigazione dell'albero.</summary>
    string Summary,
    /// <summary>Contenuto esteso: procedura passo-passo oppure il fatto/nozione, con tutti i dettagli.</summary>
    string Content,
    /// <summary>Percorso di categorie dal generale allo specifico (es. ["Software", "Excel"]).</summary>
    IReadOnlyList<string> CategoryPath,
    /// <summary>Tipo di memoria: "procedura" o "nozione".</summary>
    string Kind = "nozione");
