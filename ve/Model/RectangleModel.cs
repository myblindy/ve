



using ReactiveUI;
using System;
using Avalonia.Media;

namespace Model {
public partial class RectangleModel : ReactiveObject
{
	    private int __X ;
	
    public int X
    {
        get => __X;
        set => this.RaiseAndSetIfChanged(ref __X, value);
    }

    private int __Y ;
	
    public int Y
    {
        get => __Y;
        set => this.RaiseAndSetIfChanged(ref __Y, value);
    }

    private int __Width ;
	
    public int Width
    {
        get => __Width;
        set => this.RaiseAndSetIfChanged(ref __Width, value);
    }

    private int __Height ;
	
    public int Height
    {
        get => __Height;
        set => this.RaiseAndSetIfChanged(ref __Height, value);
    }

	} }
