using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Ruki.Core.Memory;

namespace Ruki.App.ViewModels;

/// <summary>
/// Nodo dell'albero di memoria così come mostrato nella <c>TreeView</c>. Contiene solo i dati
/// necessari alla visualizzazione; il contenuto esteso viene caricato dallo store alla selezione.
/// </summary>
public sealed partial class MemoryNodeViewModel : ObservableObject
{
    public string Id { get; }
    public MemoryNodeType Type { get; }

    public bool IsCategory => Type == MemoryNodeType.Category;

    /// <summary>True se la memoria è archiviata (mostrata in grigio, ignorata dagli agenti).</summary>
    public bool IsObsolete { get; }

    /// <summary>Icona mostrata accanto al titolo (cartella per le categorie, foglio per le memorie).</summary>
    public string Glyph => IsCategory ? "📁" : "📄";

    [ObservableProperty]
    private string _title;

    /// <summary>Stato di espansione nell'albero (bindato a TreeViewItem.IsExpanded): preservato tra i refresh.</summary>
    [ObservableProperty]
    private bool _isExpanded;

    /// <summary>Selezione nell'albero (bindata a TreeViewItem.IsSelected): preservata tra i refresh.</summary>
    [ObservableProperty]
    private bool _isSelected;

    public ObservableCollection<MemoryNodeViewModel> Children { get; } = [];

    public MemoryNodeViewModel(MemoryNodeInfo info)
    {
        Id = info.Id;
        Type = info.Type;
        IsObsolete = info.IsObsolete;
        _title = info.Title;
    }
}
