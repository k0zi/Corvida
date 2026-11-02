using System.Collections.Generic;
using System.Threading.Tasks;
using Corvida.Models;

namespace Corvida.Services;

public enum MessageKind
{
    Info,
    Warning,
    Error,
}

public interface IDialogService
{
    Task<string?> ShowInputDialogAsync(string title, string prompt, string placeholder = "");
    Task<bool> ShowConfirmDialogAsync(string title, string message);

    /// <summary>
    /// A message the user has to acknowledge — for text too long or too important for a toast
    /// (<see cref="INotificationService"/>), e.g. addresses to note down or a list of skipped files.
    /// </summary>
    Task ShowMessageAsync(string title, string message, MessageKind kind = MessageKind.Info);

    /// <summary>Prompts for a password. Returns null if the user cancels.</summary>
    Task<string?> ShowPasswordDialogAsync(string title, string prompt, string confirmLabel = "Confirm");
    Task<string?> ShowPickerDialogAsync(string title, IReadOnlyList<string> options);
    Task<List<string>?> ShowBoardMembersDialogAsync(IReadOnlyList<Agent> allAgents, List<string> currentMemberIds);
}
