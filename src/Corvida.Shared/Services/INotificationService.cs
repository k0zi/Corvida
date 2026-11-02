namespace Corvida.Services;

/// <summary>
/// Short, non-blocking notices that close themselves — the outcome of an action the user just
/// took ("Exported", "Could not delete"). Anything the user has to read before going on, or must
/// be able to copy, belongs in <see cref="IDialogService.ShowMessageAsync"/> instead.
/// </summary>
public interface INotificationService
{
    void Success(string message, string? title = null);
    void Info(string message, string? title = null);
    void Warning(string message, string? title = null);
    void Error(string message, string? title = null);
}
