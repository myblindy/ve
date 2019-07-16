using Avalonia.Data.Converters;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ve.Converters
{
    class SectionStartEndToSecondsWidthConverter : IMultiValueConverter
    {
        public object Convert(IList<object> values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values[2] is double zoom)
            {
                var start = (TimeSpan)values[0];
                var end = (TimeSpan)values[1];

                return zoom * (end - start).TotalSeconds;
            }
            else
                return 0;
        }
    }
}
