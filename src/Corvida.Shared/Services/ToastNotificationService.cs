using KD.Avalonia.Rice.Notifications;

namespace Corvida.Services;

/// <summary>
/// Shows notifications as Rice toasts. <see cref="RiceToast"/> finds its own host — the active
/// window on the desktop, the main view's top level in the browser — and may be called from any
/// thread, so this needs no reference to the shell.
/// </summary>
public sealed class ToastNotificationService : INotificationService
{
    public void Success(string message, string? title = null) => RiceToast.ShowSuccess(message, title);

    public void Info(string message, string? title = null) => RiceToast.ShowInfo(message, title);

    public void Warning(string message, string? title = null) => RiceToast.ShowWarning(message, title);

    public void Error(string message, string? title = null) => RiceToast.ShowError(message, title);
}
