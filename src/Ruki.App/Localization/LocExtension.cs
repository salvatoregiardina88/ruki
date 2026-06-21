using System.Windows.Data;
using System.Windows.Markup;

namespace Ruki.App.Localization;

/// <summary>
/// Estensione di markup per usare le stringhe localizzate in XAML: <c>{loc:Loc Chiave}</c>.
/// <para>
/// Restituisce un binding sull'indicizzatore di <see cref="Loc.Instance"/>: il testo si traduce
/// in base alla lingua corrente e si aggiorna da solo quando la lingua cambia a runtime.
/// </para>
/// </summary>
[MarkupExtensionReturnType(typeof(string))]
public sealed class LocExtension : MarkupExtension
{
    public LocExtension() { }

    public LocExtension(string key) => Key = key;

    /// <summary>Chiave della stringa da tradurre (vedi <see cref="Strings"/>).</summary>
    public string Key { get; set; } = string.Empty;

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var binding = new Binding($"[{Key}]")
        {
            Source = Loc.Instance,
            Mode = BindingMode.OneWay,
        };
        return binding.ProvideValue(serviceProvider);
    }
}
