using System.IO;
using System.Text;
using System.Windows;
using Ruki.Core.Localization;

namespace Ruki.App.Views;

/// <summary>
/// Mostra l'informativa sulla privacy. Il testo (IT/EN) è incorporato nell'app come risorsa e
/// scelto in base alla lingua corrente.
/// </summary>
public partial class PrivacyWindow : Window
{
    public PrivacyWindow()
    {
        InitializeComponent();
        PrivacyText.Text = LoadPrivacyText();
    }

    private static string LoadPrivacyText()
    {
        var file = string.Equals(Localizer.Language, "en", StringComparison.Ordinal) ? "privacy_en.txt" : "privacy_it.txt";
        try
        {
            var resource = Application.GetResourceStream(new Uri($"/Assets/{file}", UriKind.Relative));
            if (resource is null)
                return string.Empty;

            using var reader = new StreamReader(resource.Stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            return reader.ReadToEnd();
        }
        catch
        {
            return string.Empty;
        }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}
