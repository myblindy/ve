



using ReactiveUI;
using System;
using Avalonia.Media;

namespace Model {
public class MediaFileModel : ReactiveObject
{
	    private ve.FFmpeg.Support.FFmpegVideoStreamDecoder __Decoder ;
	
    public ve.FFmpeg.Support.FFmpegVideoStreamDecoder Decoder
    {
        get => __Decoder;
        set => this.RaiseAndSetIfChanged(ref __Decoder, value);
    }

    private Brush __BackgroundBrush ;
	
    public Brush BackgroundBrush
    {
        get => __BackgroundBrush;
        set => this.RaiseAndSetIfChanged(ref __BackgroundBrush, value);
    }

	} }
