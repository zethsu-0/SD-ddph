using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace ddph.Converters
{
    public sealed class ProductImageConverter : IValueConverter
    {
        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not string source || string.IsNullOrWhiteSpace(source))
            {
                return null;
            }

            return CreateImageSource(source);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }

        public static BitmapImage? CreateImageSource(string source)
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();

                if (TryGetDataUriBytes(source, out var bytes))
                {
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = new MemoryStream(bytes);
                }
                else
                {
                    bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                    bitmap.UriSource = new Uri(source, UriKind.RelativeOrAbsolute);
                }

                bitmap.EndInit();
                if (bitmap.CanFreeze && bitmap.IsDownloading == false)
                {
                    bitmap.Freeze();
                }
                return bitmap;
            }
            catch
            {
                return null;
            }
        }

        private static bool TryGetDataUriBytes(string source, out byte[] bytes)
        {
            bytes = Array.Empty<byte>();

            if (!source.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var commaIndex = source.IndexOf(',');
            if (commaIndex < 0 ||
                !source[..commaIndex].Contains(";base64", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            bytes = System.Convert.FromBase64String(source[(commaIndex + 1)..]);
            return true;
        }
    }
}
