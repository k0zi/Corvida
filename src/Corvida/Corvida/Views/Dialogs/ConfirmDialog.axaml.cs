using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using SukiUI.Controls;

namespace Corvida.Views.Dialogs;

public partial class ConfirmDialog : SukiWindow
{
    public bool Confirmed { get; private set; }

    public ConfirmDialog(string title, string message)
    {
        InitializeComponent();
        Title = title;
        this.FindControl<TextBlock>("MessageText")!.Text = message;
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void Yes_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Confirmed = true;
        Close();
    }

    private void No_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close();
}
