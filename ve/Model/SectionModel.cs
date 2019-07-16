



using ReactiveUI;
using System;
using Avalonia.Media;

namespace Model {
public class SectionModel : ReactiveObject
{
	    private MediaFileModel __MediaFile ;
	
    public MediaFileModel MediaFile
    {
        get => __MediaFile;
        set => this.RaiseAndSetIfChanged(ref __MediaFile, value);
    }

    private TimeSpan __Start ;
	
    public TimeSpan Start
    {
        get => __Start;
        set => this.RaiseAndSetIfChanged(ref __Start, value);
    }

    private TimeSpan __End ;
	
    public TimeSpan End
    {
        get => __End;
        set => this.RaiseAndSetIfChanged(ref __End, value);
    }

    private Brush __BackgroundBrush ;
	
    public Brush BackgroundBrush
    {
        get => __BackgroundBrush;
        set => this.RaiseAndSetIfChanged(ref __BackgroundBrush, value);
    }

	} }
