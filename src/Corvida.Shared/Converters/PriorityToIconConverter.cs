using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Material.Icons;

namespace Corvida.Converters;

public class PriorityToIconConverter : IValueConverter
{
    public static readonly PriorityToIconConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value?.ToString() switch
        {
            "High"   => MaterialIconKind.Fire,
            "Medium" => MaterialIconKind.Warning,
            "Low"    => MaterialIconKind.Circle,
            _        => MaterialIconKind.Circle
        };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
