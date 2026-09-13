using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace OctoTask.UI.Converters
{
    public class SearchHighlightConverter : IMultiValueConverter
    {
        private static readonly SolidColorBrush HighlightBrush = new SolidColorBrush(Color.FromRgb(0x3d, 0x2e, 0x00));

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 2 || values[0] is not string text || values[1] is not string filter)
                return Brushes.Transparent;

            if (string.IsNullOrWhiteSpace(filter) || string.IsNullOrWhiteSpace(text))
                return Brushes.Transparent;

            return text.ToLowerInvariant().Contains(filter.ToLowerInvariant()) ? HighlightBrush : Brushes.Transparent;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
