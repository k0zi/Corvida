using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;
using Corvida.Models;

namespace Corvida.Converters;

/// <summary>(task, set of blocked task IDs) → whether the task waits for an unfinished prerequisite.</summary>
public class TaskBlockedConverter : IMultiValueConverter
{
    public static readonly TaskBlockedConverter Instance = new();

    public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture) =>
        values is [KanbanTask task, IReadOnlySet<string> blocked, ..] && blocked.Contains(task.Id);
}
