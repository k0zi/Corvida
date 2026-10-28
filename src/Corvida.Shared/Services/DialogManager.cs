using System;
using System.Collections.ObjectModel;
using Avalonia.Controls;

namespace Corvida.Services;

/// <summary>One open in-app dialog: a title and the control shown under it.</summary>
public sealed class DialogRequest(string title, Control content, Action onDismissed)
{
    public string Title { get; } = title;
    public Control Content { get; } = content;
    internal Action OnDismissed { get; } = onDismissed;
}

/// <summary>
/// The dialogs currently open, drawn by <c>DialogHost</c> as overlays inside the shell rather than
/// as separate windows — the browser host runs under <c>ISingleViewApplicationLifetime</c>, where a
/// second window is impossible. The last one in <see cref="Dialogs"/> is on top.
/// </summary>
public sealed class DialogManager
{
    public ObservableCollection<DialogRequest> Dialogs { get; } = [];

    public DialogRequest Show(string title, Control content, Action? onDismissed = null)
    {
        var request = new DialogRequest(title, content, onDismissed ?? (() => { }));
        Dialogs.Add(request);
        return request;
    }

    /// <summary>Closes the dialog; does nothing if it is already closed.</summary>
    public void Dismiss(DialogRequest request)
    {
        if (Dialogs.Remove(request)) request.OnDismissed();
    }
}
