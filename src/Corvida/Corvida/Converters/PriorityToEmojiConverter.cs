using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Corvida.Converters;

public class PriorityToEmojiConverter : IValueConverter
{
    public static readonly PriorityToEmojiConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value?.ToString() switch
        {
            "High"   => "🔴 High",
            "Medium" => "🟡 Medium",
            "Low"    => "🟢 Low",
            var v    => v ?? string.Empty
        };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value?.ToString() switch
        {
            "🔴 High"   => "High",
            "🟡 Medium" => "Medium",
            "🟢 Low"    => "Low",
            var v       => v ?? string.Empty
        };
}
