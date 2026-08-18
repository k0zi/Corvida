using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Interactivity;
using Markdown.Avalonia;

namespace Corvida.Controls;

public enum EditorMode { Edit, Split, View }

public partial class MarkdownEditor : UserControl
{
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<MarkdownEditor, string>(nameof(Text),
            defaultValue: string.Empty, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<EditorMode> ModeProperty =
        AvaloniaProperty.Register<MarkdownEditor, EditorMode>(nameof(Mode),
            defaultValue: EditorMode.Edit);

    public string Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public EditorMode Mode { get => GetValue(ModeProperty); set => SetValue(ModeProperty, value); }

    public MarkdownEditor() => InitializeComponent();

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ModeProperty)
            ApplyMode((EditorMode)change.NewValue!);
    }

    private void OnEditClick(object? sender, RoutedEventArgs e)  => Mode = EditorMode.Edit;
    private void OnSplitClick(object? sender, RoutedEventArgs e) => Mode = EditorMode.Split;
    private void OnViewClick(object? sender, RoutedEventArgs e)  => Mode = EditorMode.View;

    private void ApplyMode(EditorMode mode)
    {
        var grid     = this.FindControl<Grid>("ContentGrid")!;
        var editor   = this.FindControl<TextBox>("EditorBox")!;
        var splitter = this.FindControl<GridSplitter>("Splitter")!;
        var preview  = this.FindControl<MarkdownScrollViewer>("PreviewBox")!;

        grid.ColumnDefinitions[0].Width = mode == EditorMode.View
            ? new GridLength(0) : GridLength.Star;
        grid.ColumnDefinitions[1].Width = mode == EditorMode.Split
            ? new GridLength(4) : new GridLength(0);
        grid.ColumnDefinitions[2].Width = mode == EditorMode.Edit
            ? new GridLength(0) : GridLength.Star;

        editor.IsVisible   = mode != EditorMode.View;
        splitter.IsVisible = mode == EditorMode.Split;
        preview.IsVisible  = mode != EditorMode.Edit;

        SetActive(this.FindControl<Button>("EditModeBtn")!,  mode == EditorMode.Edit);
        SetActive(this.FindControl<Button>("SplitModeBtn")!, mode == EditorMode.Split);
        SetActive(this.FindControl<Button>("ViewModeBtn")!,  mode == EditorMode.View);
    }

    private static void SetActive(Button btn, bool active)
    {
        btn.Classes.Remove(active ? "Basic" : "Flat");
        if (!btn.Classes.Contains(active ? "Flat" : "Basic"))
            btn.Classes.Add(active ? "Flat" : "Basic");
    }
}
