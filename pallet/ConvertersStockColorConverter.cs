using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace PalletApp.Converters
{
    /// <summary>
    /// Если остаток меньше 20 — красный, если 0 — тоже красный, иначе серый.
    /// </summary>
    public class StockColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int qty)
            {
                if (qty <= 0) return Brushes.Red;      // нет в наличии
                if (qty < 20) return Brushes.Red;      // мало
            }
            return new SolidColorBrush(Color.FromRgb(0x6E, 0x6E, 0x6E)); // серый
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}