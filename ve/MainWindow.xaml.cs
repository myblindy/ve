﻿using Avalonia;
using Avalonia.Controls;
using Avalonia.Diagnostics.ViewModels;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Model;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using ve.FFmpeg.Support;

namespace ve
{
    public class MainWindow : Window
    {
        public class MainWindowViewModel
        {
            public ObservableCollection<MediaFileModel> MediaFiles { get; } = new ObservableCollection<MediaFileModel>();
            public ObservableCollection<SectionModel> Sections { get; } = new ObservableCollection<SectionModel>();
        }

        MainWindowViewModel ViewModel => (MainWindowViewModel)DataContext;

        public MainWindow()
        {
            FFmpegSetup.Initialize();

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
                ViewModel.MediaFiles.Add(new MediaFileModel
                {
                    Decoder = new FFmpegVideoStreamDecoder(files[0])
                });
            }
        }
    }
}
