using Avalonia;
using Avalonia.Controls;
using Avalonia.Diagnostics.ViewModels;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using ve.FFmpeg;
using ve.FFmpeg.Support;
using ve.Model;

namespace ve
{
    public class MainWindow : Window
    {
        MainWindowViewModel ViewModel => (MainWindowViewModel)DataContext;

        public MainWindow()
        {
            FFmpegSetup.Initialize();

            // test
            var vm = new MainWindowViewModel();
            var mf = new MediaFileModel { Decoder = new FFmpegVideoStreamDecoder(@"Z:\Marius\cp_loading_icon.mp4") };
            vm.MediaFiles.Add(mf);
            vm.AddSection(mf, TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(6.5));

            vm.Camera.AddKeyFrame(new RectangleModel { X = 0, Y = 0, Width = 200, Height = 100 }, TimeSpan.Zero);

            OutputRenderer.Start(vm, @"c:\stuff\temp.webm", 22);
            Environment.Exit(0);

            InitializeComponent();
            DataContext = new MainWindowViewModel();

#if DEBUG
            this.AttachDevTools();
#endif
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public async void AddMediaFile()
        {
            var dlg = new OpenFileDialog
            {
                Filters = new List<FileDialogFilter>
                {
                    new FileDialogFilter { Name = "Video Files", Extensions = new List<string> {"avi", "mkv", "mp4",  "mpg" } },
                    new FileDialogFilter { Name = "Image Files", Extensions = new List<string> {"jpg", "jpeg", "gif", "png", "bmp" } },
                    new FileDialogFilter { Name = "All Files", Extensions = new List<string> { "*" } }
                }
            };

            var files = await dlg.ShowAsync(this);

            if (files.Any())
            {
                var mf = new MediaFileModel
                {
                    Decoder = new FFmpegVideoStreamDecoder(files[0]),
                    BackgroundBrush = new SolidColorBrush(((uint)ViewModel.Random.Next(int.MaxValue)) & 0x00FFFFFF | 0xFF000000)
                };

                ViewModel.MediaFiles.Add(mf);
                ViewModel.AddSection(mf);
            }
        }
    }

    internal class MainWindowViewModel : ReactiveObject
    {
        public ObservableCollection<MediaFileModel> MediaFiles { get; } = new ObservableCollection<MediaFileModel>();
        public ObservableCollection<SectionModel> Sections { get; } = new ObservableCollection<SectionModel>();
        public KeyFrameModel<RectangleModel> Camera { get; } = new KeyFrameModel<RectangleModel>();

        internal readonly Random Random = new Random();

        public void AddSection(MediaFileModel mf, TimeSpan start = default, TimeSpan end = default) =>
            Sections.Add(new SectionModel
            {
                MediaFile = mf,
                Start = TimeSpan.FromSeconds(Math.Min(start.TotalSeconds, mf.Decoder.LengthSeconds)),
                End = TimeSpan.FromSeconds(Math.Min(mf.Decoder.LengthSeconds, end.TotalSeconds)),
            });
    }
}
