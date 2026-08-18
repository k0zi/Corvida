using System;
using System.Globalization;
using System.IO;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;

namespace Corvida.Converters;

public class Base64ToBitmapConverter : IValueConverter
{
    public static readonly Base64ToBitmapConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string dataUri || string.IsNullOrEmpty(dataUri)) return null;

        var comma = dataUri.IndexOf(',');
        if (comma < 0) return null;

        try
        {
            var bytes = System.Convert.FromBase64String(dataUri[(comma + 1)..]);
            using var ms = new MemoryStream(bytes);
            return new Bitmap(ms);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
