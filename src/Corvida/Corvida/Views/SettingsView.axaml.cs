using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Corvida.ViewModels;

namespace Corvida.Views;

public partial class SettingsView : UserControl
{
    public SettingsView() => InitializeComponent();

    private async void BrowseFolder_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null) return;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select Data Folder",
            AllowMultiple = false
        });

        if (folders.Count > 0 && DataContext is SettingsViewModel vm)
            vm.DataPath = folders[0].Path.LocalPath;
    }
}
