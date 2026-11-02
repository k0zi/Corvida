using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Input;

namespace Corvida.Views.Dialogs;

public partial class PickerDialogContent : UserControl, IDialogBody
{
    private readonly Action<string?> _complete;

    public PickerDialogContent(IReadOnlyList<string> options, Action<string?> complete)
    {
        InitializeComponent();
        _complete = complete;
        OptionsList.ItemsSource = options;
    }

    private void Ok_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Confirm();

    private void Cancel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => _complete(null);

    private void OptionsList_DoubleTapped(object? sender, TappedEventArgs e) => Confirm();

    public string ConfirmText => "OK";

    public void UseHostButtons()
    {
        ButtonRow.IsVisible = false;
        Root.Margin = default;
        // Sized by its list instead of the overlay's fixed box; long lists scroll.
        Height = double.NaN;
        OptionsList.MaxHeight = 320;
    }

    public void Confirm()
    {
        if (OptionsList.SelectedItem is string selected) _complete(selected);
    }
}
