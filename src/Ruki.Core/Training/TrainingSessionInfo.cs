namespace Ruki.Core.Training;

/// <summary>Riepilogo di una sessione di addestramento registrata.</summary>
public sealed record TrainingSessionInfo(
    string Id,
    string FolderPath,
    string? VideoPath,
    TimeSpan Duration,
    int FrameCount,
    int EventCount);
