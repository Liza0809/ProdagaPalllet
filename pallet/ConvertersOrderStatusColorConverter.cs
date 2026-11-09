using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace PalletApp.Converters
{
    /// <summary>
    /// Возвращает цвет для статуса заказа.
    /// </summary>
    public class OrderStatusColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int statusId)
            {
                switch (statusId)
                {
                    case 1: return new SolidColorBrush(Color.FromRgb(0x6A, 0x1B, 0x9A)); // Оформлен — фиолетовый
                    case 2: return new SolidColorBrush(Color.FromRgb(0x19, 0x76, 0xD2)); // Оплачен — синий
                    case 3: return new SolidColorBrush(Color.FromRgb(0x00, 0x89, 0x6A)); // Отгружен — тёмно-зелёный
                    case 4: return new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32)); // Готов — зелёный
                    case 6: return new SolidColorBrush(Color.FromRgb(0xD3, 0x2F, 0x2F)); // Отменён — красный
                }
            }
            return new SolidColorBrush(Color.FromRgb(0x6E, 0x6E, 0x6E));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}