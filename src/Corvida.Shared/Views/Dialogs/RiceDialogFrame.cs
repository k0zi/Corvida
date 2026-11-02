using System;
using Avalonia.Controls;
using Avalonia.Media;
using KD.Avalonia.Rice.Dialogs;

namespace Corvida.Views.Dialogs;

/// <summary>
/// The content of a <see cref="RiceDialogWindow"/>: a heading — the window has no title bar, so
/// <c>DialogOptions.Title</c> is never displayed — over the dialog's own body. Implementing
/// <see cref="IDialogContent"/> lets the body close the window once it has a valid result.
/// </summary>
internal sealed class RiceDialogFrame : StackPanel, IDialogContent
{
    private bool _closed;

    public RiceDialogFrame(string title, Control body)
    {
        Spacing = 12;
        Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 16,
            FontWeight = FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap,
        });
        Children.Add(body);
    }

    public event EventHandler<object?>? CloseRequested;

    /// <summary>Closes the window once; later calls (e.g. Enter and a click together) are ignored.</summary>
    public void RequestClose()
    {
        if (_closed) return;
        _closed = true;
        CloseRequested?.Invoke(this, null);
    }
}
