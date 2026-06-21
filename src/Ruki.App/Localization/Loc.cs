using System.ComponentModel;
using Ruki.Core.Localization;

namespace Ruki.App.Localization;

/// <summary>
/// Servizio di localizzazione dell'interfaccia. Espone le stringhe tradotte tramite un
/// indicizzatore (<c>[chiave]</c>), così che la UI possa farci binding e aggiornarsi
/// automaticamente quando si cambia lingua a runtime.
/// <para>
/// È un singleton (<see cref="Instance"/>) perché la lingua è una scelta globale dell'app.
/// Implementa <see cref="INotifyPropertyChanged"/>: al cambio di lingua notifica l'indicizzatore
/// e WPF rivaluta tutti i binding <c>{loc:Loc Chiave}</c>.
/// </para>
/// <para>
/// La lingua corrente vive nel <see cref="Localizer"/> di Core (fonte di verità unica condivisa con
/// l'infrastruttura): questa classe è la "vista" WPF, con notifiche e indicizzatore per il binding.
/// </para>
/// </summary>
public sealed class Loc : INotifyPropertyChanged
{
    /// <summary>Istanza condivisa usata dai binding XAML e dai ViewModel.</summary>
    public static Loc Instance { get; } = new();

    private Loc() { }

    /// <summary>Lingua corrente ("it" o "en"). Cambiandola si aggiorna tutta la UI in binding.</summary>
    public string Language
    {
        get => Localizer.Language;
        set
        {
            var lang = string.Equals(value, "en", StringComparison.OrdinalIgnoreCase) ? "en" : "it";
            if (Localizer.Language == lang)
                return;

            Localizer.Language = lang;
            // "Item[]" è la stringa speciale con cui WPF segnala il cambio di un indicizzatore:
            // così tutti i binding sull'indicizzatore vengono rivalutati.
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Language)));
        }
    }

    /// <summary>Traduzione della chiave (tabella UI) nella lingua corrente (la chiave stessa se mancante).</summary>
    public string this[string key] => Strings.Get(Localizer.Language, key);

    /// <summary>Comodo accesso da codice: <c>Loc.T("Chiave")</c>.</summary>
    public static string T(string key) => Instance[key];

    /// <summary>Come <see cref="T(string)"/>, ma con formattazione dei valori (es. "Imparato: {0}").</summary>
    public static string T(string key, params object?[] args)
        => string.Format(Instance[key], args);

    public event PropertyChangedEventHandler? PropertyChanged;
}
