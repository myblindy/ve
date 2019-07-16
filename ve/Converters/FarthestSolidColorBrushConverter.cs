using Avalonia.Data.Converters;
using Avalonia.Media;
using MoreLinq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using ve.Support;

namespace ve.Converters
{
    class FarthestSolidColorBrushConverter : IMultiValueConverter
    {
        public object Convert(IList<object> values, Type targetType, object parameter, CultureInfo culture)
        {
            var color = ((SolidColorBrush)values[0]).Color;

            return (SolidColorBrush)values.Skip(1).MaxBy(c => ((SolidColorBrush)c).Color.Distance2(color)).First();
        }
    }
}
