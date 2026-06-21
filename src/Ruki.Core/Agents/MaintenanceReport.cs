namespace Ruki.Core.Agents;

/// <summary>Riepilogo di un ciclo di manutenzione della memoria (deduplica + riorganizzazione + archiviazione).</summary>
public sealed record MaintenanceReport(
    int MergedGroups,
    int RemovedMemories,
    int MovedMemories,
    int RemovedCategories,
    int ObsoletedMemories,
    IReadOnlyList<string> Notes)
{
    public bool MadeChanges =>
        MergedGroups > 0 || RemovedMemories > 0 || MovedMemories > 0 || RemovedCategories > 0 || ObsoletedMemories > 0;

    public string Summary => MadeChanges
        ? $"Unite {MergedGroups} duplicati ({RemovedMemories} accorpate), {MovedMemories} risistemate, "
          + $"{RemovedCategories} categorie vuote rimosse, {ObsoletedMemories} archiviate per inutilizzo."
        : "Nessuna modifica necessaria.";
}
