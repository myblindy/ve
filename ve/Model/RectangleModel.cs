



using ReactiveUI;
using System;
using Avalonia.Media;
using System.Collections.Generic;

namespace ve.Model {
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

namespace ve.Model
{
	public partial class RectangleModel: IKeyFrameInterpolation<RectangleModel>
	{
		public RectangleModel InterpolateWith(RectangleModel other, double ratio) =>
			new RectangleModel
			{
				X = (int)((1 - ratio) * X + ratio * other.X),
				Y = (int)((1 - ratio) * Y + ratio * other.Y),
				Height = (int)((1 - ratio) * Height + ratio * other.Height),
				Width = (int)((1 - ratio) * Width + ratio * other.Width),
			};


        public override bool Equals(object obj) => obj is RectangleModel model && X == model.X && Y == model.Y && Width == model.Width && Height == model.Height;
        public override int GetHashCode() => HashCode.Combine(X, Y, Width, Height);

        public RectangleModel() {}
		public RectangleModel(int x, int y, int w, int h) { X = x; Y = y; Width = w; Height = h; }

        public static bool operator ==(RectangleModel left, RectangleModel right) => EqualityComparer<RectangleModel>.Default.Equals(left, right);
        public static bool operator !=(RectangleModel left, RectangleModel right) => !(left == right);
	}
}