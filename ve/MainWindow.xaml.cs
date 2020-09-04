using Avalonia;
using Avalonia.Controls;
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
            var mf = new MediaFileModel { Decoder = new FFmpegVideoStreamDecoder(@"E:\vids\Gfriend\shows\G-ING\200108 [G-ING] Ready for Rehearsal - GFRIEND (여자친구).mkv") };
            vm.MediaFiles.Add(mf);
            vm.AddSection(mf, TimeSpan.FromSeconds(26.7), TimeSpan.FromSeconds(31));

            const int width = 600, height = 980;
            vm.Camera.AddKeyFrame(new RectangleModel { X = 900, Y = 100, Width = width, Height = height }, TimeSpan.Zero);
            vm.Camera.AddKeyFrame(new RectangleModel { X = 0, Y = 100, Width = width, Height = height }, TimeSpan.FromSeconds(1));
            //vm.Camera.AddKeyFrame(new RectangleModel { X = 0, Y = 200, Width = 200, Height = 100 }, TimeSpan.FromSeconds(2));
            //vm.Camera.AddKeyFrame(new RectangleModel { X = 0, Y = 0, Width = 200, Height = 100 }, TimeSpan.FromSeconds(3));

            OutputRenderer.Start(vm, @"e:\temp\yuju-test.webm", 22, width, height);
            Environment.Exit(0);

            InitializeComponent();
            DataContext = new MainWindowViewModel();
        }

        private void InitializeComponent() =>
            AvaloniaXamlLoader.Load(this);

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

    public class MainWindowViewModel : ReactiveObject
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
