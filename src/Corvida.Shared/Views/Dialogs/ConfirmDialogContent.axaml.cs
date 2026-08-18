using System;
using Avalonia.Controls;

namespace Corvida.Views.Dialogs;

public partial class ConfirmDialogContent : UserControl
{
    private readonly Action<bool> _complete;

    public ConfirmDialogContent(string message, Action<bool> complete)
    {
        InitializeComponent();
        _complete = complete;
        MessageText.Text = message;
    }

    private void Yes_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => _complete(true);

    private void No_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => _complete(false);
}
