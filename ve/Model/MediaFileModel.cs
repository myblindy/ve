



using ReactiveUI;
using System;

namespace Model {
public class MediaFileModel : ReactiveObject
{
	    private string __FullPath ;
	
    public string FullPath
    {
        get => __FullPath;
        set => this.RaiseAndSetIfChanged(ref __FullPath, value);
    }

    private TimeSpan __Length ;
	
    public TimeSpan Length
    {
        get => __Length;
        set => this.RaiseAndSetIfChanged(ref __Length, value);
    }

	} }
