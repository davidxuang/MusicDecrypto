using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace MusicDecrypto.Avalonia.Controls;

public partial class MatchDialogContent : UserControl
{
    public MatchDialogContent()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
