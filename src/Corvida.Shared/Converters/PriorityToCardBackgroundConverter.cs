using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Corvida.Converters;

public class PriorityToCardBackgroundConverter : IValueConverter
{
    public static readonly PriorityToCardBackgroundConverter Instance = new();

    private static readonly IBrush High = new SolidColorBrush(Color.Parse("#33F44336"));
    private static readonly IBrush Medium = new SolidColorBrush(Color.Parse("#33FFA000"));
    private static readonly IBrush Default = new SolidColorBrush(Color.Parse("#20808080"));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value?.ToString() switch
        {
            "High"   => High,
            "Medium" => Medium,
            _        => Default
        };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
