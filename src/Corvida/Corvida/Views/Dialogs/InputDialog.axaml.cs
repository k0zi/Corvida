using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using SukiUI.Controls;

namespace Corvida.Views.Dialogs;

public partial class InputDialog : SukiWindow
{
    public string? Result { get; private set; }

    public InputDialog(string title, string prompt, string placeholder = "")
    {
        InitializeComponent();
        Title = title;
        this.FindControl<TextBlock>("PromptText")!.Text = prompt;
        var inputBox = this.FindControl<TextBox>("InputBox")!;
        inputBox.Watermark = placeholder;
        inputBox.Focus();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void Ok_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Submit();

    private void Cancel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close();

    private void InputBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) Submit();
        else if (e.Key == Key.Escape) Close();
    }

    private void Submit()
    {
        var text = this.FindControl<TextBox>("InputBox")!.Text?.Trim();
        if (!string.IsNullOrEmpty(text))
        {
            Result = text;
            Close();
        }
    }
}
