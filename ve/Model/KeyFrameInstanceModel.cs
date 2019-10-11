



using ReactiveUI;
using System;
using Avalonia.Media;

namespace ve.Model {
public partial class KeyFrameInstanceModel<T> : ReactiveObject where T: IKeyFrameInterpolation<T>
{
	    private T __InnerObject ;
	
    public T InnerObject
    {
        get => __InnerObject;
        set => this.RaiseAndSetIfChanged(ref __InnerObject, value);
    }

    private TimeSpan __Timestamp ;
	
    public TimeSpan Timestamp
    {
        get => __Timestamp;
        set => this.RaiseAndSetIfChanged(ref __Timestamp, value);
    }

	} }
