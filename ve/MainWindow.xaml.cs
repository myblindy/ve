using Avalonia;
using Avalonia.Controls;
using Avalonia.Diagnostics.ViewModels;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Model;
using System;
using System.Collections.ObjectModel;

namespace ve
{
    public class MainWindow : Window
    {
        public class MainWindowViewModel
        {
            public ObservableCollection<MediaFileModel> MediaFiles { get; } = new ObservableCollection<MediaFileModel>();
            public ObservableCollection<SectionModel> Sections { get; } = new ObservableCollection<SectionModel>();
        }

        public MainWindow()
        {
            InitializeComponent();

            var model = new MainWindowViewModel();
            model.Sections.Add(new SectionModel
            {
                Start = TimeSpan.Zero,
                End = TimeSpan.FromSeconds(25.5),
                BackgroundBrush = new SolidColorBrush(Color.FromRgb(50, 150, 100)),
                MediaFile = new MediaFileModel { FullPath = @"c:\stuff\file.mp4" },
            });
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
