using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ve.Controls
{
    public class GraphTimePanel : UserControl
    {
        public GraphTimePanel()
        {
            this.InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
