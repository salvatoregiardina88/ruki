using System.Windows;
using System.Windows.Media;

namespace Ruki.App.Views;

/// <summary>Mostra un'immagine a piena risoluzione (scorribile), per leggere i dettagli del debug.</summary>
public partial class ImageViewerWindow : Window
{
    public ImageViewerWindow(ImageSource image)
    {
        InitializeComponent();
        ViewerImage.Source = image;
    }
}
