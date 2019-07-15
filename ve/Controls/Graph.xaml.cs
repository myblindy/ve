using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Model;
using System.Collections.ObjectModel;

namespace ve.Controls
{
    public class Graph : UserControl
    {
        public ObservableCollection<SectionModel> Sections { get; }

        public Graph()
        {
            this.InitializeComponent();
            DataContext = this;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
