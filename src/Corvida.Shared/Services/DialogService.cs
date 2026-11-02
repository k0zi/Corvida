using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using CommunityToolkit.Mvvm.Input;
using Corvida.Models;
using Corvida.Views.Dialogs;
using KD.Avalonia.Rice.Dialogs;

namespace Corvida.Services;

/// <summary>
/// Shows dialogs as <see cref="RiceDialog"/> windows on the desktop, and as overlays drawn by
/// <c>DialogHost</c> in the browser: <see cref="RiceDialog"/> opens a real window, which
/// <c>ISingleViewApplicationLifetime</c> cannot do. The dialog bodies are shared by both.
/// </summary>
public class DialogService : IDialogService
{
    // RiceDialogWindow's DockPanel margin (24 left + 20 right), added to a body's own width.
    private const double WindowHorizontalPadding = 44;

    // The icon column of RiceDialogWindow: a 48 px icon with an 18 px gap.
    private const double IconColumnWidth = 66;

    private readonly DialogManager _dialogManager;

    public DialogService(DialogManager dialogManager)
    {
        _dialogManager = dialogManager;
    }

    private static bool UseWindows =>
        Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime;

    public Task<string?> ShowInputDialogAsync(string title, string prompt, string placeholder = "") =>
        ShowAsync<string?>(title, complete => new InputDialogContent(prompt, placeholder, complete), null);

    public Task<string?> ShowPasswordDialogAsync(string title, string prompt, string confirmLabel = "Confirm") =>
        ShowAsync<string?>(title, complete => new PasswordDialogContent(prompt, confirmLabel, complete), null);

    public Task<string?> ShowPickerDialogAsync(string title, IReadOnlyList<string> options) =>
        ShowAsync<string?>(title, complete => new PickerDialogContent(options, complete), null);

    public Task<List<string>?> ShowBoardMembersDialogAsync(IReadOnlyList<Agent> allAgents, List<string> currentMemberIds) =>
        ShowAsync<List<string>?>("Manage Board Members", complete => new BoardMembersDialogContent(allAgents, currentMemberIds, complete), null);

    public async Task<bool> ShowConfirmDialogAsync(string title, string message)
    {
        if (!UseWindows)
            return await ShowOverlayAsync<bool>(title, complete => new ConfirmDialogContent(message, complete), false);

        // No is the default: these are mostly "delete …?" questions, and Enter should not destroy anything.
        var result = await RiceDialog.ShowAsync(new DialogOptions
        {
            Title = title,
            Icon = DialogIcon.Question,
            Content = new RiceDialogFrame(title, MessageText(message)),
            Width = 360 + WindowHorizontalPadding + IconColumnWidth,
            Buttons =
            [
                new DialogButton(KD.Avalonia.Rice.RiceStrings.Yes, DialogResult.Yes) { IsAccent = true },
                new DialogButton(KD.Avalonia.Rice.RiceStrings.No, DialogResult.No) { IsDefault = true, IsCancel = true },
            ],
        });
        return result is DialogResult.Yes;
    }

    public async Task ShowMessageAsync(string title, string message, MessageKind kind = MessageKind.Info)
    {
        if (!UseWindows)
        {
            await ShowOverlayAsync<bool>(title, complete => new MessageDialogContent(message, complete), false);
            return;
        }

        await RiceDialog.ShowAsync(new DialogOptions
        {
            Title = title,
            Icon = kind switch
            {
                MessageKind.Warning => DialogIcon.Warning,
                MessageKind.Error => DialogIcon.Error,
                _ => DialogIcon.Information,
            },
            Content = new RiceDialogFrame(title, MessageText(message)),
            Width = 400 + WindowHorizontalPadding + IconColumnWidth,
            Buttons = [new DialogButton(KD.Avalonia.Rice.RiceStrings.Ok, DialogResult.Ok) { IsAccent = true, IsDefault = true, IsCancel = true }],
        });
    }

    private Task<TResult> ShowAsync<TResult>(string title, Func<Action<TResult>, Control> createContent, TResult defaultResult) =>
        UseWindows
            ? ShowWindowAsync(title, createContent, defaultResult)
            : ShowOverlayAsync(title, createContent, defaultResult);

    private static async Task<TResult> ShowWindowAsync<TResult>(string title, Func<Action<TResult>, Control> createContent, TResult defaultResult)
    {
        var tcs = new TaskCompletionSource<TResult>();
        RiceDialogFrame? frame = null;

        void Complete(TResult result)
        {
            tcs.TrySetResult(result);
            frame?.RequestClose();
        }

        var content = createContent(Complete);
        frame = new RiceDialogFrame(title, content);

        var buttons = new List<DialogButton>();
        if (content is IDialogBody body)
        {
            body.UseHostButtons();
            buttons.Add(new DialogButton(body.ConfirmText)
            {
                IsAccent = true,
                IsDefault = true,
                // The body completes (and so closes the window) only once its input is valid.
                ClosesDialog = false,
                Command = new RelayCommand(body.Confirm),
            });
            buttons.Add(DialogButton.For(DialogResult.Cancel));
        }

        await RiceDialog.ShowAsync(new DialogOptions
        {
            Title = title,
            Content = frame,
            Buttons = buttons,
            Width = double.IsNaN(content.Width) ? 420 : content.Width + WindowHorizontalPadding,
        });

        // Closed by Cancel / Esc rather than by the body.
        tcs.TrySetResult(defaultResult);
        return await tcs.Task;
    }

    private Task<TResult> ShowOverlayAsync<TResult>(string title, Func<Action<TResult>, Control> createContent, TResult defaultResult)
    {
        var tcs = new TaskCompletionSource<TResult>();
        DialogRequest? request = null;

        void Complete(TResult result)
        {
            tcs.TrySetResult(result);
            if (request is not null) _dialogManager.Dismiss(request);
        }

        var content = createContent(Complete);
        request = _dialogManager.Show(title, content, () => tcs.TrySetResult(defaultResult));

        return tcs.Task;
    }

    private static SelectableTextBlock MessageText(string message) => new()
    {
        Text = message,
        TextWrapping = Avalonia.Media.TextWrapping.Wrap,
        VerticalAlignment = VerticalAlignment.Center,
    };
}
