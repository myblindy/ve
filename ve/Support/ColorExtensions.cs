using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Text;

namespace ve.Support
{
    static class ColorExtensions
    {
        internal static int Distance2(this Color color1, Color color2)
        {
            var dr = color1.R - color2.R;
            var dg = color1.G - color2.G;
            var db = color1.B - color2.B;

            return dr * dr + dg * dg + db * db;
        }

        internal static Color FarthestColor(this Color forecolor, Color back1, Color back2)
        {
            var d1 = forecolor.Distance2(back1);
            var d2 = forecolor.Distance2(back2);

            return d1 > d2 ? back1 : back2;
        }
    }
}
