using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ve.Controls
{
    public class GraphTimePanel : UserControl
    {
        public GraphTimePanel()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
