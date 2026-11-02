using System;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Corvida.Views.Dialogs;

/// <summary>The overlay form of <c>IDialogService.ShowMessageAsync</c>: a message and an OK button.</summary>
public partial class MessageDialogContent : UserControl
{
    private readonly Action<bool> _complete;

    public MessageDialogContent(string message, Action<bool> complete)
    {
        InitializeComponent();
        _complete = complete;
        MessageText.Text = message;
    }

    private void Ok_Click(object? sender, RoutedEventArgs e) => _complete(true);
}
