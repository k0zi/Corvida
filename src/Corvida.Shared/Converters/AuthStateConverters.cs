using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Material.Icons;

namespace Corvida.Converters;

/// <summary>Renders the auth switch state as readable text.</summary>
public sealed class BoolToAuthStateConverter : IValueConverter
{
    public static readonly BoolToAuthStateConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? "Login is required" : "Login is not required";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Open padlock while the server is unprotected, closed once login is required.</summary>
public sealed class BoolToLockIconConverter : IValueConverter
{
    public static readonly BoolToLockIconConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? MaterialIconKind.LockCheck : MaterialIconKind.LockOpenVariantOutline;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
