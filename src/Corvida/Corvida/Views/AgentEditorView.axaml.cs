using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Corvida.ViewModels;

namespace Corvida.Views;

public partial class AgentEditorView : UserControl
{
    public AgentEditorView() => InitializeComponent();

    private async void ChooseAvatar_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not AgentEditorViewModel vm) return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Choose Avatar",
            AllowMultiple = false,
            FileTypeFilter = new[] { FilePickerFileTypes.ImageAll },
        });
        if (files.Count == 0) return;

        await using var stream = await files[0].OpenReadAsync();
        using var scaled = Bitmap.DecodeToWidth(stream, 256);
        using var ms = new MemoryStream();
        scaled.Save(ms);
        vm.AvatarDataUri = $"data:image/png;base64,{Convert.ToBase64String(ms.ToArray())}";
    }
}
