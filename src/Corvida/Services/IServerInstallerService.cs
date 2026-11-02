using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Corvida.Services;

/// <summary>
/// Installs the Corvida server (API, web UI, PostgreSQL) on this computer as rootless Podman containers
/// managed by systemd user units (Quadlet). Nothing here needs root: the units live in the user's own
/// config folder and the images are updated by Podman, not by the app.
/// </summary>
public interface IServerInstallerService
{
    Task<ServerStatus> GetStatusAsync(CancellationToken ct = default);

    /// <summary>The settings in the installed units, or null when the server is not installed.</summary>
    ServerInstallOptions? ReadInstalledOptions();

    /// <summary>
    /// Writes the units with <paramref name="options"/>, pulls the images and (re)starts the server.
    /// Also used to apply changed options to an existing install.
    /// </summary>
    Task<ServerInstallResult> InstallAsync(ServerInstallOptions options, IProgress<string> progress, CancellationToken ct = default);

    Task StartAsync(CancellationToken ct = default);
    Task StopAsync(CancellationToken ct = default);

    /// <summary>
    /// Stops the server and removes its units. The database volume and its password are kept unless
    /// <paramref name="deleteData"/> is set, so a later install picks the same boards back up.
    /// </summary>
    Task UninstallAsync(bool deleteData, IProgress<string> progress, CancellationToken ct = default);
}

public sealed record ServerInstallOptions(int WebHttpPort, int WebHttpsPort, int ApiPort, string WebHost)
{
    public static ServerInstallOptions Default { get; } = new(8183, 8283, 5083, "localhost");
}

public enum ServerState
{
    /// <summary>Not Linux, or a Podman too old for Quadlet.</summary>
    Unsupported,
    PodmanMissing,
    NotInstalled,
    Stopped,
    Starting,
    Running,
    Failed,
}

public sealed record ServerStatus(ServerState State, string Message);

/// <param name="Warnings">Things that worked out differently from what the user would expect, e.g. that the
/// server stops on logout because lingering could not be enabled.</param>
public sealed record ServerInstallResult(IReadOnlyList<string> Warnings);
