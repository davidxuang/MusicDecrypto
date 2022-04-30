using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace MusicDecrypto.Avalonia.Pages
{
    public partial class HomePage : UserControl
    {
        public HomePage()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
