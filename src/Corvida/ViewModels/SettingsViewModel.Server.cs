using System;
using System.Diagnostics;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Corvida.Services;

namespace Corvida.ViewModels;

/// <summary>
/// The "Corvida Server (Podman)" card on the Settings page's Install tab.
/// </summary>
public partial class SettingsViewModel
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanInstallServer), nameof(IsServerInstalled), nameof(IsServerRunning),
        nameof(IsServerStopped), nameof(IsPodmanMissing), nameof(ServerInstallLabel))]
    private ServerState _serverState = ServerState.NotInstalled;

    [ObservableProperty] private string _serverStatusText = "Checking…";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsServerIdle))]
    [NotifyCanExecuteChangedFor(nameof(InstallServerCommand), nameof(StartServerCommand), nameof(StopServerCommand),
        nameof(UninstallServerCommand), nameof(UseLocalServerCommand))]
    private bool _serverBusy;

    [ObservableProperty] private string? _serverProgress;

    // decimal?: what NumericUpDown binds to.
    [ObservableProperty] private decimal? _serverWebPort = ServerInstallOptions.Default.WebHttpPort;
    [ObservableProperty] private decimal? _serverWebHttpsPort = ServerInstallOptions.Default.WebHttpsPort;
    [ObservableProperty] private decimal? _serverApiPort = ServerInstallOptions.Default.ApiPort;
    [ObservableProperty] private string _serverWebHost = ServerInstallOptions.Default.WebHost;
    [ObservableProperty] private bool _serverDeleteData;

    public bool IsServerIdle => !ServerBusy;
    public bool IsPodmanMissing => ServerState == ServerState.PodmanMissing;
    public bool CanInstallServer => ServerState is not (ServerState.Unsupported or ServerState.PodmanMissing);
    public bool IsServerInstalled => ServerState is ServerState.Stopped or ServerState.Starting
        or ServerState.Running or ServerState.Failed;
    public bool IsServerRunning => ServerState is ServerState.Running or ServerState.Starting;
    public bool IsServerStopped => ServerState is ServerState.Stopped or ServerState.Failed;
    public string ServerInstallLabel => IsServerInstalled ? "Apply settings" : "Install";

    private string LocalWebUrl => $"http://localhost:{ServerWebPort:0}";
    private string LocalApiUrl => $"http://localhost:{ServerApiPort:0}";

    private bool CanRunServerCommand() => !ServerBusy;

    [RelayCommand]
    private async Task RefreshServerStatusAsync()
    {
        try
        {
            var status = await _serverInstaller.GetStatusAsync();
            ServerState = status.State;
            ServerStatusText = status.State == ServerState.Running
                ? $"Running. Web UI: {LocalWebUrl}, API: {LocalApiUrl}"
                : status.Message;
        }
        catch (Exception ex)
        {
            ServerState = ServerState.Failed;
            ServerStatusText = ex.Message;
        }
    }

    /// <summary>Shows the installed server's ports rather than the defaults, then its state.</summary>
    private async Task LoadServerStateAsync()
    {
        if (_serverInstaller.ReadInstalledOptions() is { } installed)
        {
            ServerWebPort = installed.WebHttpPort;
            ServerWebHttpsPort = installed.WebHttpsPort;
            ServerApiPort = installed.ApiPort;
            ServerWebHost = installed.WebHost;
        }
        await RefreshServerStatusAsync();
    }

    [RelayCommand(CanExecute = nameof(CanRunServerCommand))]
    private async Task InstallServerAsync()
    {
        var wasInstalled = IsServerInstalled;
        var options = new ServerInstallOptions(
            (int)(ServerWebPort ?? 0), (int)(ServerWebHttpsPort ?? 0), (int)(ServerApiPort ?? 0), ServerWebHost.Trim());

        ServerInstallResult? result = null;
        await RunServerOperationAsync("Server Install Failed", async progress =>
            result = await _serverInstaller.InstallAsync(options, progress));
        if (result is null) return;

        var message = wasInstalled
            ? "The new settings are applied and the server has been restarted."
            : $"The Corvida server is running.\n\nWeb UI: {LocalWebUrl}\nAPI (for the desktop app and the MCP server): {LocalApiUrl}";
        if (result.Warnings.Count > 0)
            message += "\n\n" + string.Join("\n\n", result.Warnings);

        // A fresh install carries the addresses to note down, and warnings must be read: both stay
        // until acknowledged. A plain re-apply is only an outcome.
        if (wasInstalled && result.Warnings.Count == 0)
            _notifications.Success(message, "Settings Applied");
        else
            await _dialogService.ShowMessageAsync(wasInstalled ? "Settings Applied" : "Server Installed", message,
                result.Warnings.Count > 0 ? MessageKind.Warning : MessageKind.Info);

        if (!wasInstalled && !(IsServerHosted && ServerUrl.Trim() == LocalApiUrl))
            await UseLocalServerAsync();
    }

    [RelayCommand(CanExecute = nameof(CanRunServerCommand))]
    private Task StartServerAsync() =>
        RunServerOperationAsync("Server Start Failed", _ => _serverInstaller.StartAsync());

    [RelayCommand(CanExecute = nameof(CanRunServerCommand))]
    private Task StopServerAsync() =>
        RunServerOperationAsync("Server Stop Failed", _ => _serverInstaller.StopAsync());

    [RelayCommand(CanExecute = nameof(CanRunServerCommand))]
    private async Task UninstallServerAsync()
    {
        var question = ServerDeleteData
            ? "Remove the Corvida server and DELETE all of its data — every board, task, agent and user stored on it? This cannot be undone."
            : "Remove the Corvida server from this computer? Its data is kept, and a later install picks it up again.";
        if (!await _dialogService.ShowConfirmDialogAsync("Uninstall Server", question)) return;

        var deleteData = ServerDeleteData;
        await RunServerOperationAsync("Server Uninstall Failed",
            progress => _serverInstaller.UninstallAsync(deleteData, progress));
        ServerDeleteData = false;
    }

    /// <summary>Points this app at the local server; boards in the local folder are not copied over.</summary>
    [RelayCommand(CanExecute = nameof(CanRunServerCommand))]
    private async Task UseLocalServerAsync()
    {
        var confirmed = await _dialogService.ShowConfirmDialogAsync("Use This Server",
            $"Switch Corvida to the server at {LocalApiUrl}?\n\nBoards in your local folder are not copied to the server; "
            + "switch back to Local Folder at any time to see them again.");
        if (!confirmed) return;

        ServerUrl = LocalApiUrl;
        IsServerHosted = true;
        await Save();
    }

    [RelayCommand]
    private void OpenServerWebUi()
    {
        try
        {
            Process.Start(new ProcessStartInfo(LocalWebUrl) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            ServerProgress = $"Could not open a browser: {ex.Message}";
        }
    }

    private async Task RunServerOperationAsync(string failureTitle, Func<IProgress<string>, Task> operation)
    {
        ServerBusy = true;
        ServerProgress = null;
        try
        {
            // Progress<T> posts to the UI thread it was created on.
            await operation(new Progress<string>(message => ServerProgress = message));
            ServerProgress = null;
        }
        catch (Exception ex)
        {
            ServerProgress = null;
            _notifications.Error(ex.Message, failureTitle);
        }
        finally
        {
            ServerBusy = false;
            await RefreshServerStatusAsync();
        }
    }
}
