using System.Collections.Generic;
using System.Threading.Tasks;
using Corvida.Models;

namespace Corvida.Services;

public interface IDialogService
{
    Task<string?> ShowInputDialogAsync(string title, string prompt, string placeholder = "");
    Task<bool> ShowConfirmDialogAsync(string title, string message);
    Task<string?> ShowPickerDialogAsync(string title, IReadOnlyList<string> options);
    Task<List<string>?> ShowBoardMembersDialogAsync(IReadOnlyList<Agent> allAgents, List<string> currentMemberIds);
}
