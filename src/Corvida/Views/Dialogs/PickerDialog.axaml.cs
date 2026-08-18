using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using SukiUI.Controls;

namespace Corvida.Views.Dialogs;

public partial class PickerDialog : SukiWindow
{
    public string? Selected { get; private set; }

    public PickerDialog(string title, IReadOnlyList<string> options)
    {
        InitializeComponent();
        Title = title;
        this.FindControl<ListBox>("OptionsList")!.ItemsSource = options;
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void Ok_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Confirm();

    private void Cancel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close();

    private void OptionsList_DoubleTapped(object? sender, TappedEventArgs e) => Confirm();

    private void Confirm()
    {
        var selected = this.FindControl<ListBox>("OptionsList")!.SelectedItem as string;
        if (selected is null) return;
        Selected = selected;
        Close();
    }
}
