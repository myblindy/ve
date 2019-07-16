using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using ve.Support;

namespace ve.Controls
{
    public class GraphSectionItem : UserControl
    {
        public static readonly AvaloniaProperty<string> TitleProperty = AvaloniaProperty.Register<GraphSectionItem, string>("Title");
        public string Title
        {
            get => GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public GraphSectionItem()
        {
            this.InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
