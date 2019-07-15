using Avalonia;
using Avalonia.Controls;
using Avalonia.Diagnostics.ViewModels;
using Avalonia.Markup.Xaml;
using Model;
using System.Collections.ObjectModel;

namespace ve
{
    public class MainWindow : Window
    {
        public class MainWindowViewModel : ViewModelBase
        {
            public ObservableCollection<MediaFileModel> MediaFiles { get; } = new ObservableCollection<MediaFileModel>();
            public ObservableCollection<SectionModel> Sections { get; } = new ObservableCollection<SectionModel>();
        }

        public MainWindow()
        {
            InitializeComponent();

            var model = new MainWindowViewModel();
            model.Sections.Add(new SectionModel());
            DataContext = model;
#if DEBUG
            this.AttachDevTools();
#endif
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
