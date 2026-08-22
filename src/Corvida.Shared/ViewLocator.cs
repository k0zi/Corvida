using System;
using System.Reflection;
using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Corvida.ViewModels;

namespace Corvida;

/// <summary>
/// Given a view model, returns the corresponding view if possible.
/// </summary>
[RequiresUnreferencedCode(
    "Default implementation of ViewLocator involves reflection which may be trimmed away.",
    Url = "https://docs.avaloniaui.net/docs/concepts/view-locator")]
public class ViewLocator : IDataTemplate
{
    public Control? Build(object? param)
    {
        if (param is null)
            return null;

        var viewModelType = param.GetType();
        var name = viewModelType.FullName!.Replace("ViewModel", "View", StringComparison.Ordinal);

        // Type.GetType only probes the calling assembly (this one) and the core library, so on its
        // own it cannot see views that live in a host project — Corvida's SettingsView and the
        // Skills views are in Corvida.dll and used to render as "Not Found". Look in the view
        // model's own assembly first, which is where its view belongs, then fall back to the
        // entry assembly.
        var type = Type.GetType(name)
                   ?? viewModelType.Assembly.GetType(name)
                   ?? Assembly.GetEntryAssembly()?.GetType(name);

        if (type != null)
        {
            return (Control)Activator.CreateInstance(type)!;
        }

        return new TextBlock { Text = "Not Found: " + name };
    }

    public bool Match(object? data)
    {
        return data is ViewModelBase;
    }
}