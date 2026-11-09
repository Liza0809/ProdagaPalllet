using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace PalletApp.Converters
{
    public class ImageFallbackConverter : IValueConverter
    {
        private static readonly BitmapImage _fallback = LoadImage("/zag.png");

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string path = value as string;

            if (string.IsNullOrWhiteSpace(path))
                return _fallback;

            var img = LoadImage(path);
            return img ?? _fallback;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();

        private static BitmapImage LoadImage(string relativePath)
        {
            try
            {
                var uri = new Uri($"pack://application:,,,{relativePath}", UriKind.Absolute);
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = uri;
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                return bmp;
            }
            catch
            {
                return null;
            }
        }
    }
}