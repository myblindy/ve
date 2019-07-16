using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Model;
using System.Collections.ObjectModel;

namespace ve.Controls
{
    public class Graph : UserControl
    {
        public static readonly AvaloniaProperty<ObservableCollection<SectionModel>> SectionsProperty =
            AvaloniaProperty.Register<Graph, ObservableCollection<SectionModel>>("Sections");
        public ObservableCollection<SectionModel> Sections
        {
            get => GetValue(SectionsProperty);
            set => SetValue(SectionsProperty, value);
        }

        public static readonly AvaloniaProperty<double> ZoomProperty = AvaloniaProperty.Register<Graph, double>("Zoom", 50);
        public double Zoom
        {
            get => GetValue(ZoomProperty);
            set => SetValue(ZoomProperty, value);
        }

        public Graph()
        {
            this.InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
