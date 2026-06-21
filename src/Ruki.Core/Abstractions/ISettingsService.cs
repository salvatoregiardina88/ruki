using Ruki.Core.Configuration;

namespace Ruki.Core.Abstractions;

/// <summary>
/// Gestisce il caricamento e il salvataggio delle <see cref="RukiSettings"/>.
/// <para>
/// È pensato come singleton: <see cref="Current"/> espone sempre l'ultimo stato
/// salvato, così qualunque componente può leggere le impostazioni senza ricaricare
/// il file ogni volta.
/// </para>
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// Impostazioni correntemente in vigore. È una vista in sola lettura: per modificarle
    /// si passa una copia a <see cref="Save"/>.
    /// </summary>
    RukiSettings Current { get; }

    /// <summary>
    /// (Ri)carica le impostazioni dal file. Se il file manca o è corrotto, restituisce
    /// i valori di default (e li persiste), senza mai lanciare per un file illeggibile.
    /// </summary>
    RukiSettings Load();

    /// <summary>
    /// Persiste le impostazioni fornite, aggiorna <see cref="Current"/> e notifica
    /// gli interessati tramite <see cref="Changed"/>.
    /// </summary>
    void Save(RukiSettings settings);

    /// <summary>Sollevato dopo un salvataggio andato a buon fine, con le nuove impostazioni.</summary>
    event EventHandler<RukiSettings>? Changed;
}
