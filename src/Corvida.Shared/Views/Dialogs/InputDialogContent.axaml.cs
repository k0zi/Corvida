using System;
using Avalonia.Controls;
using Avalonia.Input;

namespace Corvida.Views.Dialogs;

public partial class InputDialogContent : UserControl, IDialogBody
{
    private readonly Action<string?> _complete;

    public InputDialogContent(string prompt, string placeholder, Action<string?> complete)
    {
        InitializeComponent();
        _complete = complete;
        PromptText.Text = prompt;
        InputBox.PlaceholderText = placeholder;
        InputBox.Focus();
    }

    protected override void OnLoaded(Avalonia.Interactivity.RoutedEventArgs e)
    {
        base.OnLoaded(e);
        InputBox.Focus();
    }

    private void Ok_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Submit();

    private void Cancel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => _complete(null);

    private void InputBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) Submit();
        else if (e.Key == Key.Escape) _complete(null);
    }

    public string ConfirmText => "OK";

    public void Confirm() => Submit();

    public void UseHostButtons()
    {
        ButtonRow.IsVisible = false;
        Root.Margin = default;
    }

    private void Submit()
    {
        var text = InputBox.Text?.Trim();
        if (!string.IsNullOrEmpty(text)) _complete(text);
    }
}
