using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Ruki.App.Localization;
using Ruki.Core.Abstractions;
using Ruki.Core.Agents;
using Ruki.Core.Memory;

namespace Ruki.App.ViewModels;

/// <summary>
/// ViewModel del tab "Memoria": mostra l'albero delle memorie e permette di aggiungere,
/// modificare ed eliminare nodi.
/// <para>
/// L'albero viene ricaricato per intero dallo store a ogni modifica, ma lo stato di espansione e la
/// selezione vengono PRESERVATI (vedi <see cref="Rebuild"/>): così aggiungere/eliminare un nodo non
/// richiude l'albero. La memoria iniziale è piccola; se crescerà molto si passerà al lazy-load.
/// </para>
/// </summary>
public sealed partial class MemoryViewModel : ObservableObject
{
    private readonly IMemoryStore _store;
    private readonly IMemoryMaintenanceAgent _maintenance;
    private readonly ISettingsService _settings;
    private readonly ILogger<MemoryViewModel> _logger;

    public ObservableCollection<MemoryNodeViewModel> Roots { get; } = [];

    /// <summary>Testo "Ultima manutenzione: …" mostrato nella barra dei comandi.</summary>
    [ObservableProperty]
    private string _lastMaintenanceText = string.Empty;

    [ObservableProperty]
    private MemoryNodeViewModel? _selectedNode;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
    [NotifyCanExecuteChangedFor(nameof(ToggleObsoleteCommand))]
    private bool _hasSelection;

    /// <summary>True se il nodo selezionato è una memoria (mostra il campo "Contenuto").</summary>
    [ObservableProperty]
    private bool _isMemorySelected;

    /// <summary>
    /// Stato di archiviazione del nodo selezionato. La UI ci fa binding per scegliere l'etichetta
    /// del pulsante (Archivia/Riattiva), localizzata via DataTrigger in XAML.
    /// </summary>
    [ObservableProperty]
    private bool _selectedIsObsolete;

    [ObservableProperty] private string _editTitle = string.Empty;
    [ObservableProperty] private string _editSummary = string.Empty;
    [ObservableProperty] private string _editContent = string.Empty;
    [ObservableProperty] private string _statusMessage = string.Empty;

    public MemoryViewModel(
        IMemoryStore store,
        IMemoryMaintenanceAgent maintenance,
        ISettingsService settings,
        ILogger<MemoryViewModel> logger)
    {
        _store = store;
        _maintenance = maintenance;
        _settings = settings;
        _logger = logger;
        Refresh();
    }

    /// <summary>Chiamato dalla View quando cambia la selezione nell'albero.</summary>
    public void Select(MemoryNodeViewModel? node)
    {
        SelectedNode = node;
        HasSelection = node is not null;

        if (node is null)
        {
            ClearEditor();
            return;
        }

        var full = _store.GetNode(node.Id);
        IsMemorySelected = node.Type == MemoryNodeType.Memory;
        SelectedIsObsolete = node.IsObsolete;
        EditTitle = full?.Title ?? node.Title;
        EditSummary = full?.Summary ?? string.Empty;
        EditContent = full?.Content ?? string.Empty;
        StatusMessage = string.Empty;
    }

    [RelayCommand]
    private void Refresh() => Rebuild();

    /// <summary>
    /// Ricostruisce l'albero dallo store preservando espansione e selezione. Può forzare
    /// l'espansione di un nodo e selezionarne uno (utile dopo un inserimento).
    /// </summary>
    private void Rebuild(string? forceExpandId = null, string? selectId = null)
    {
        // 1. Memorizza lo stato corrente (cosa è espanso e cosa è selezionato).
        var expanded = new HashSet<string>(StringComparer.Ordinal);
        CollectExpanded(Roots, expanded);
        if (forceExpandId is not null)
            expanded.Add(forceExpandId);
        selectId ??= SelectedNode?.Id;

        // 2. Ricostruisci.
        Roots.Clear();
        foreach (var info in _store.GetChildren(null))
            Roots.Add(BuildNode(info));

        // 3. Riapplica espansione e selezione ai nodi corrispondenti.
        var toSelect = ApplyExpansionAndFindSelection(Roots, expanded, selectId);
        if (toSelect is not null)
            toSelect.IsSelected = true;   // bindato a TreeViewItem.IsSelected -> richiama Select()
        else
            Select(null);                 // selezione persa (es. nodo eliminato): svuota l'editor

        UpdateLastMaintenanceText();
    }

    /// <summary>Esegue la manutenzione (deduplica/fusione) e aggiorna l'albero.</summary>
    [RelayCommand]
    private async Task RunMaintenanceAsync()
    {
        StatusMessage = Loc.T("Memory_MaintRunning");
        try
        {
            var report = await _maintenance.RunAsync();
            Rebuild();
            StatusMessage = report.Summary;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Manutenzione della memoria fallita.");
            StatusMessage = Loc.T("Memory_Error", ex.Message);
        }
    }

    [RelayCommand]
    private void AddCategory() => AddNode(MemoryNodeType.Category, Loc.T("Memory_NewCategory"));

    [RelayCommand]
    private void AddMemory() => AddNode(MemoryNodeType.Memory, Loc.T("Memory_NewMemory"));

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Save()
    {
        if (SelectedNode is not { } selected)
            return;

        var node = _store.GetNode(selected.Id);
        if (node is null)
        {
            Rebuild();   // il nodo è sparito (eliminato altrove): ricarichiamo.
            return;
        }

        node.Title = string.IsNullOrWhiteSpace(EditTitle) ? node.Title : EditTitle.Trim();
        node.Summary = string.IsNullOrWhiteSpace(EditSummary) ? null : EditSummary.Trim();
        // Le categorie non hanno contenuto esteso.
        node.Content = selected.IsCategory || string.IsNullOrWhiteSpace(EditContent) ? null : EditContent;

        _store.Update(node);
        selected.Title = node.Title;   // riflette il nuovo titolo nell'albero
        StatusMessage = Loc.T("Memory_Saved");
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Delete()
    {
        if (SelectedNode is not { } selected)
            return;

        // Il nodo non esiste più: niente selezione da ripristinare.
        _store.Delete(selected.Id);
        SelectedNode = null;
        Rebuild();
        StatusMessage = Loc.T("Memory_Deleted");
    }

    /// <summary>Archivia o riattiva la memoria selezionata.</summary>
    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void ToggleObsolete()
    {
        if (SelectedNode is not { } selected)
            return;

        var nowObsolete = !SelectedIsObsolete;
        _store.SetObsolete(selected.Id, nowObsolete);
        Rebuild(selectId: selected.Id);   // resta selezionato lo stesso nodo, aggiornato
        StatusMessage = nowObsolete ? Loc.T("Memory_Archived") : Loc.T("Memory_Reactivated");
    }

    /// <summary>Aggiunge un nodo sotto la categoria selezionata (o accanto/al livello giusto).</summary>
    private void AddNode(MemoryNodeType type, string defaultTitle)
    {
        // Padre: la categoria selezionata; se è selezionata una memoria, il suo stesso padre;
        // se non c'è selezione, la radice.
        string? parentId = SelectedNode switch
        {
            { IsCategory: true } category => category.Id,
            { } memory => _store.GetNode(memory.Id)?.ParentId,
            _ => null,
        };

        var created = _store.Add(new MemoryNode { Title = defaultTitle, Type = type, ParentId = parentId });
        // Espandi il padre (così il nuovo nodo è visibile) e seleziona il nuovo nodo.
        Rebuild(forceExpandId: parentId, selectId: created.Id);
        StatusMessage = type == MemoryNodeType.Category ? Loc.T("Memory_CategoryAdded") : Loc.T("Memory_MemoryAdded");
    }

    private MemoryNodeViewModel BuildNode(MemoryNodeInfo info)
    {
        var node = new MemoryNodeViewModel(info);
        foreach (var child in _store.GetChildren(info.Id))
            node.Children.Add(BuildNode(child));
        return node;
    }

    /// <summary>Raccoglie gli id dei nodi attualmente espansi (per ripristinarli dopo la ricostruzione).</summary>
    private static void CollectExpanded(IEnumerable<MemoryNodeViewModel> nodes, HashSet<string> expanded)
    {
        foreach (var node in nodes)
        {
            if (node.IsExpanded)
                expanded.Add(node.Id);
            CollectExpanded(node.Children, expanded);
        }
    }

    /// <summary>Riapplica l'espansione e restituisce il nodo da selezionare (se presente).</summary>
    private static MemoryNodeViewModel? ApplyExpansionAndFindSelection(
        IEnumerable<MemoryNodeViewModel> nodes, HashSet<string> expanded, string? selectId)
    {
        MemoryNodeViewModel? found = null;
        foreach (var node in nodes)
        {
            if (expanded.Contains(node.Id))
                node.IsExpanded = true;
            if (selectId is not null && node.Id == selectId)
                found = node;
            found ??= ApplyExpansionAndFindSelection(node.Children, expanded, selectId);
        }
        return found;
    }

    private void UpdateLastMaintenanceText()
    {
        var last = _settings.Current.LastMemoryMaintenanceUtc;
        var when = last is null
            ? Loc.T("Memory_NeverRun")
            : last.Value.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
        LastMaintenanceText = Loc.T("Memory_LastMaintenance", when);
    }

    private void ClearEditor()
    {
        SelectedNode = null;
        HasSelection = false;
        IsMemorySelected = false;
        SelectedIsObsolete = false;
        EditTitle = string.Empty;
        EditSummary = string.Empty;
        EditContent = string.Empty;
    }
}
