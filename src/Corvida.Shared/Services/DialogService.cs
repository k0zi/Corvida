using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using Corvida.Models;
using Corvida.Views.Dialogs;
using SukiUI.Dialogs;

namespace Corvida.Services;

public class DialogService : IDialogService
{
    private readonly ISukiDialogManager _dialogManager;

    public DialogService(ISukiDialogManager dialogManager)
    {
        _dialogManager = dialogManager;
    }

    public Task<string?> ShowInputDialogAsync(string title, string prompt, string placeholder = "") =>
        ShowAsync<string?>(title, complete => new InputDialogContent(prompt, placeholder, complete), null);

    public Task<bool> ShowConfirmDialogAsync(string title, string message) =>
        ShowAsync<bool>(title, complete => new ConfirmDialogContent(message, complete), false);

    public Task<string?> ShowPickerDialogAsync(string title, IReadOnlyList<string> options) =>
        ShowAsync<string?>(title, complete => new PickerDialogContent(options, complete), null);

    public Task<List<string>?> ShowBoardMembersDialogAsync(IReadOnlyList<Agent> allAgents, List<string> currentMemberIds) =>
        ShowAsync<List<string>?>("Manage Board Members", complete => new BoardMembersDialogContent(allAgents, currentMemberIds, complete), null);

    private Task<TResult> ShowAsync<TResult>(string title, Func<Action<TResult>, Control> createContent, TResult defaultResult)
    {
        var tcs = new TaskCompletionSource<TResult>();
        SukiDialogBuilder? builder = null;

        void Complete(TResult result)
        {
            tcs.TrySetResult(result);
            if (builder is not null) _dialogManager.TryDismissDialog(builder.Dialog);
        }

        var content = createContent(Complete);
        builder = _dialogManager.CreateDialog()
            .WithTitle(title)
            .WithContent(content)
            .OnDismissed(_ => tcs.TrySetResult(defaultResult));
        builder.TryShow();

        return tcs.Task;
    }
}
