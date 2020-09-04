using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ve.Model;
using System.Collections.ObjectModel;

namespace ve.Controls
{
    public class Graph : UserControl
    {
        public static readonly StyledProperty<ObservableCollection<SectionModel>> SectionsProperty =
            AvaloniaProperty.Register<Graph, ObservableCollection<SectionModel>>("Sections");
        public ObservableCollection<SectionModel> Sections => GetValue(SectionsProperty);

        public static readonly StyledProperty<double> ZoomProperty = AvaloniaProperty.Register<Graph, double>("Zoom", 50);
        public double Zoom
        {
            get => GetValue(ZoomProperty);
            set => SetValue(ZoomProperty, value);
        }

        public Graph() => InitializeComponent();

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
