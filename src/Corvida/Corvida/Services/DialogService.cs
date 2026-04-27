using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Corvida.Views.Dialogs;

namespace Corvida.Services;

public class DialogService : IDialogService
{
    private Window GetMainWindow() =>
        ((IClassicDesktopStyleApplicationLifetime)Application.Current!.ApplicationLifetime!).MainWindow!;

    public async Task<string?> ShowInputDialogAsync(string title, string prompt, string placeholder = "")
    {
        var dialog = new InputDialog(title, prompt, placeholder);
        await dialog.ShowDialog(GetMainWindow());
        return dialog.Result;
    }

    public async Task<bool> ShowConfirmDialogAsync(string title, string message)
    {
        var dialog = new ConfirmDialog(title, message);
        await dialog.ShowDialog(GetMainWindow());
        return dialog.Confirmed;
    }

    public async Task<string?> ShowPickerDialogAsync(string title, IReadOnlyList<string> options)
    {
        var dialog = new PickerDialog(title, options);
        await dialog.ShowDialog(GetMainWindow());
        return dialog.Selected;
    }
}
