using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Corvida.Converters;

public class PriorityToBrushConverter : IValueConverter
{
    public static readonly PriorityToBrushConverter Instance = new();

    private static readonly IBrush High = new SolidColorBrush(Color.Parse("#F44336"));
    private static readonly IBrush Medium = new SolidColorBrush(Color.Parse("#FFA000"));
    private static readonly IBrush Low = new SolidColorBrush(Color.Parse("#4CAF50"));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value?.ToString() switch
        {
            "High"   => High,
            "Medium" => Medium,
            "Low"    => Low,
            _        => Brushes.Gray
        };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
