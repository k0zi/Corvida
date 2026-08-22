using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace Corvida.Views.Dialogs;

/// <summary>
/// Prompts for a password.
/// </summary>
/// <remarks>
/// Deliberately separate from <see cref="InputDialogContent"/> rather than a flag on it: that one
/// trims its input and rejects empty values, both of which are wrong for a password.
/// </remarks>
public partial class PasswordDialogContent : UserControl
{
    private readonly Action<string?> _complete;

    public PasswordDialogContent(string prompt, string confirmLabel, Action<string?> complete)
    {
        InitializeComponent();

        _complete = complete;
        PromptText.Text = prompt;
        ConfirmButton.Content = confirmLabel;
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        PasswordBox.Focus();
    }

    private void Ok_Click(object? sender, RoutedEventArgs e) => Submit();

    private void Cancel_Click(object? sender, RoutedEventArgs e) => _complete(null);

    private void PasswordBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) Submit();
        else if (e.Key == Key.Escape) _complete(null);
    }

    // No trimming: leading and trailing whitespace can be part of a password.
    private void Submit() => _complete(PasswordBox.Text ?? string.Empty);
}
