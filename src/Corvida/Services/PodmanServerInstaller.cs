using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Corvida.Services;

/// <summary>
/// Installs the Corvida server as rootless Podman Quadlet units in <c>~/.config/containers/systemd/</c>.
/// The units are the ones in packaging/quadlet/, embedded into the app, with the ports and host name the
/// user chose substituted in; the README's manual install copies the same files.
/// </summary>
public sealed partial class PodmanServerInstaller(IHttpClientFactory httpClientFactory) : IServerInstallerService
{
    private const string ResourcePrefix = "Corvida.Quadlet.";
    private const string WebUnit = "corvida-web.container";
    private const string ApiUnit = "corvida-api.container";

    // The names the units themselves use (ContainerName=, VolumeName=, NetworkName=, Secret=).
    private const string SecretName = "corvida-db-password";
    private const string DbVolume = "corvida-db";
    private const string NetworkName = "corvida";
    private static readonly string[] Volumes = [DbVolume, "corvida-web-certs"];
    private static readonly string[] Services = ["corvida-web.service", "corvida-api.service", "corvida-db.service"];

    private static readonly Version MinimumPodman = new(4, 4);
    private static readonly TimeSpan ApiStartupWait = TimeSpan.FromSeconds(90);

    private const string Header =
        "# Written by Corvida (Settings -> Install). Change the ports and host name there: this file is\n" +
        "# rewritten whenever those settings are applied.\n";

    private static string UnitDirectory
    {
        get
        {
            var config = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            if (string.IsNullOrEmpty(config))
                config = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
            return Path.Combine(config, "containers", "systemd");
        }
    }

    public async Task<ServerStatus> GetStatusAsync(CancellationToken ct = default)
    {
        if (!OperatingSystem.IsLinux())
            return new(ServerState.Unsupported,
                "The server can only be installed from here on Linux, where it runs as systemd user services.");

        var version = await Podman(["version", "--format", "{{.Client.Version}}"], ct);
        if (version.ExitCode == ProcessRunner.NotFound)
            return new(ServerState.PodmanMissing, "Podman is not installed.");
        if (!version.Succeeded)
            return new(ServerState.Unsupported, $"Podman does not work: {version.Error}");

        var parsed = ParseVersion(version.StdOut);
        if (parsed is not null && parsed < MinimumPodman)
            return new(ServerState.Unsupported,
                $"Podman {version.StdOut.Trim()} is too old: Quadlet needs Podman {MinimumPodman} or newer.");

        if (!File.Exists(Path.Combine(UnitDirectory, WebUnit)))
            return new(ServerState.NotInstalled, "Not installed.");

        // is-active prints one state per unit, in order, and exits non-zero unless all of them are active.
        var states = (await Systemctl(["is-active", .. Services], ct)).StdOut
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var byService = Services.Zip(states).ToList();

        var failed = byService.FirstOrDefault(s => s.Second == "failed").First;
        if (failed is not null)
            return new(ServerState.Failed, $"{failed} failed. See: journalctl --user -u {failed}");
        if (byService.Count == Services.Length && byService.All(s => s.Second == "active"))
            return new(ServerState.Running, "Running.");
        if (byService.Any(s => s.Second is "activating" or "reloading"))
            return new(ServerState.Starting, "Starting…");
        return new(ServerState.Stopped, "Installed, not running.");
    }

    public ServerInstallOptions? ReadInstalledOptions()
    {
        var webPath = Path.Combine(UnitDirectory, WebUnit);
        if (!File.Exists(webPath)) return null;

        var web = File.ReadAllText(webPath);
        var apiPath = Path.Combine(UnitDirectory, ApiUnit);
        var api = File.Exists(apiPath) ? File.ReadAllText(apiPath) : string.Empty;
        var defaults = ServerInstallOptions.Default;

        return new ServerInstallOptions(
            MatchPort(web, WebHttpPortLine(), defaults.WebHttpPort),
            MatchPort(web, WebHttpsPortLine(), defaults.WebHttpsPort),
            MatchPort(api, ApiPortLine(), defaults.ApiPort),
            WebHostLine().Match(web) is { Success: true } m ? m.Groups[1].Value : defaults.WebHost);

        static int MatchPort(string text, Regex regex, int fallback) =>
            regex.Match(text) is { Success: true } m && int.TryParse(m.Groups[1].Value, out var port) ? port : fallback;
    }

    public async Task<ServerInstallResult> InstallAsync(
        ServerInstallOptions options, IProgress<string> progress, CancellationToken ct = default)
    {
        Validate(options);

        var status = await GetStatusAsync(ct);
        if (status.State is ServerState.Unsupported or ServerState.PodmanMissing)
            throw new InvalidOperationException(status.Message);

        var warnings = new List<string>();
        var templates = LoadTemplates();

        progress.Report("Writing the service definitions…");
        Directory.CreateDirectory(UnitDirectory);
        foreach (var (name, template) in templates)
            await File.WriteAllTextAsync(Path.Combine(UnitDirectory, name), Header + Render(name, template, options), ct);

        progress.Report("Preparing the database password…");
        await EnsureSecretAsync(ct);

        // Pulled here rather than on first start: the progress is visible, and a failed download is
        // reported as such instead of as a service that timed out.
        foreach (var image in templates.Values.SelectMany(ImageNames).Distinct())
        {
            progress.Report($"Downloading {image}… (the first time this can take a few minutes)");
            var pull = await Podman(["pull", image], ct);
            if (!pull.Succeeded)
                throw new InvalidOperationException($"Could not download {image}:\n{pull.Error}");
        }

        progress.Report("Starting the server…");
        await Check(Systemctl(["daemon-reload"], ct), "Reloading systemd failed");
        // Restart, not start: applying new ports to a running server must replace its containers.
        await Check(Systemctl(["restart", .. Services], ct),
            "The server did not start. See: journalctl --user -u corvida-api");

        progress.Report("Waiting for the API to answer…");
        if (!await WaitForApiAsync(options.ApiPort, ct))
            warnings.Add($"The API did not answer within {ApiStartupWait.TotalSeconds:0} seconds. It may still be "
                         + "starting; if not, see: journalctl --user -u corvida-api");

        if (await EnsureLingerAsync(ct) is { } lingerWarning)
            warnings.Add(lingerWarning);

        return new ServerInstallResult(warnings);
    }

    public async Task StartAsync(CancellationToken ct = default) =>
        // The web unit Requires= the API, which Requires= the database, so this starts all three.
        await Check(Systemctl(["start", Services[0]], ct),
            "The server did not start. See: journalctl --user -u corvida-api");

    public async Task StopAsync(CancellationToken ct = default) =>
        await Check(Systemctl(["stop", .. Services], ct), "Stopping the server failed");

    public async Task UninstallAsync(bool deleteData, IProgress<string> progress, CancellationToken ct = default)
    {
        progress.Report("Stopping the server…");
        // Ignored: units that never started, or are already gone, are not an error here.
        await Systemctl(["stop", .. Services], ct);

        progress.Report("Removing the service definitions…");
        foreach (var name in LoadTemplates().Keys)
            File.Delete(Path.Combine(UnitDirectory, name));
        await Systemctl(["daemon-reload"], ct);

        if (!deleteData) return;

        progress.Report("Deleting the server data…");
        foreach (var volume in Volumes)
            await RemoveIgnoringMissing(["volume", "rm", "--force", volume], ct);
        await RemoveIgnoringMissing(["secret", "rm", SecretName], ct);
        await RemoveIgnoringMissing(["network", "rm", NetworkName], ct);
    }

    /// <summary>
    /// The database takes its password from the secret only when its volume is first initialised. A new
    /// secret beside an old volume would lock the API out of the existing data, so that case is refused.
    /// </summary>
    private async Task EnsureSecretAsync(CancellationToken ct)
    {
        if ((await Podman(["secret", "inspect", SecretName], ct)).Succeeded) return;

        if ((await Podman(["volume", "exists", DbVolume], ct)).Succeeded)
            throw new InvalidOperationException(
                $"The database volume {DbVolume} exists, but its password (the Podman secret {SecretName}) is gone, "
                + "so the server could not log in to it. Restore the secret, or uninstall with \"Also delete the "
                + "server data\" to start over.");

        // No trailing newline: it would become part of the password and the API's copy would not match.
        var password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(24));
        await Check(ProcessRunner.RunAsync("podman", ["secret", "create", SecretName, "-"], password, ct),
            "Creating the database password failed");
    }

    private async Task<bool> WaitForApiAsync(int port, CancellationToken ct)
    {
        using var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(3);
        var deadline = DateTime.UtcNow + ApiStartupWait;

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                // Any answer will do: behind a closed login gate it is a 401, which still means "up".
                await client.GetAsync($"http://127.0.0.1:{port}/api/boards", ct);
                return true;
            }
            catch (HttpRequestException) { }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested) { }

            await Task.Delay(TimeSpan.FromSeconds(2), ct);
        }
        return false;
    }

    /// <summary>
    /// Without lingering, systemd stops a user's services when their last session ends, which would take
    /// the server down with every logout. Returns a warning when it cannot be turned on.
    /// </summary>
    private static async Task<string?> EnsureLingerAsync(CancellationToken ct)
    {
        var user = Environment.UserName;
        var show = await ProcessRunner.RunAsync("loginctl", ["show-user", user, "--property=Linger"], ct: ct);
        if (show.StdOut.Trim() == "Linger=yes") return null;

        var enable = await ProcessRunner.RunAsync("loginctl", ["enable-linger", user], ct: ct);
        return enable.Succeeded
            ? null
            : $"The server stops when you log out. To keep it running, run: sudo loginctl enable-linger {user}";
    }

    private static void Validate(ServerInstallOptions options)
    {
        int[] ports = [options.WebHttpPort, options.WebHttpsPort, options.ApiPort];
        // Rootless Podman cannot bind the privileged ports below 1024.
        if (ports.Any(p => p is < 1024 or > 65535))
            throw new ArgumentException("Ports must be between 1024 and 65535.");
        if (ports.Distinct().Count() != ports.Length)
            throw new ArgumentException("The three ports must be different.");
        if (!HostName().IsMatch(options.WebHost))
            throw new ArgumentException($"\"{options.WebHost}\" is not a valid host name.");
    }

    /// <summary>
    /// Substitutes the user's settings into a unit. Each substitution must find its line exactly once, so
    /// an edit to packaging/quadlet/ that moves a value fails loudly here instead of being silently ignored.
    /// </summary>
    internal static string Render(string name, string template, ServerInstallOptions options) => name switch
    {
        WebUnit => template
            .ReplaceLine("PublishPort=8183:80", $"PublishPort={options.WebHttpPort}:80")
            .ReplaceLine("PublishPort=8283:443", $"PublishPort={options.WebHttpsPort}:443")
            .ReplaceLine("Environment=CORVIDA_WEB_HOST=localhost", $"Environment=CORVIDA_WEB_HOST={options.WebHost}"),
        ApiUnit => template
            .ReplaceLine("PublishPort=5083:5083", $"PublishPort={options.ApiPort}:5083"),
        _ => template,
    };

    private static Dictionary<string, string> LoadTemplates()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var templates = new Dictionary<string, string>();
        foreach (var resource in assembly.GetManifestResourceNames().Where(n => n.StartsWith(ResourcePrefix)))
        {
            using var stream = assembly.GetManifestResourceStream(resource)!;
            using var reader = new StreamReader(stream);
            templates[resource[ResourcePrefix.Length..]] = reader.ReadToEnd();
        }
        if (!templates.ContainsKey(WebUnit) || !templates.ContainsKey(ApiUnit))
            throw new InvalidOperationException("This build of Corvida does not include the server unit files.");
        return templates;
    }

    private static IEnumerable<string> ImageNames(string unit) =>
        ImageLine().Matches(unit).Select(m => m.Groups[1].Value.Trim());

    private static Version? ParseVersion(string text) =>
        LeadingVersion().Match(text) is { Success: true } m && Version.TryParse(m.Value, out var v) ? v : null;

    private static Task<ProcessResult> Podman(IEnumerable<string> args, CancellationToken ct) =>
        ProcessRunner.RunAsync("podman", args, ct: ct);

    private static Task<ProcessResult> Systemctl(IEnumerable<string> args, CancellationToken ct) =>
        ProcessRunner.RunAsync("systemctl", ["--user", .. args], ct: ct);

    private static async Task Check(Task<ProcessResult> run, string what)
    {
        var result = await run;
        if (!result.Succeeded)
            throw new InvalidOperationException($"{what}:\n{result.Error}");
    }

    private static async Task RemoveIgnoringMissing(string[] args, CancellationToken ct)
    {
        var result = await Podman(args, ct);
        if (!result.Succeeded && !result.Error.Contains("no such", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"podman {string.Join(' ', args)} failed:\n{result.Error}");
    }

    [GeneratedRegex(@"^PublishPort=(\d+):80$", RegexOptions.Multiline)]
    private static partial Regex WebHttpPortLine();

    [GeneratedRegex(@"^PublishPort=(\d+):443$", RegexOptions.Multiline)]
    private static partial Regex WebHttpsPortLine();

    [GeneratedRegex(@"^PublishPort=(\d+):5083$", RegexOptions.Multiline)]
    private static partial Regex ApiPortLine();

    [GeneratedRegex(@"^Environment=CORVIDA_WEB_HOST=(\S+)$", RegexOptions.Multiline)]
    private static partial Regex WebHostLine();

    [GeneratedRegex(@"^Image=(.+)$", RegexOptions.Multiline)]
    private static partial Regex ImageLine();

    [GeneratedRegex(@"^\d+\.\d+(\.\d+)?")]
    private static partial Regex LeadingVersion();

    // Letters, digits, dots and hyphens: it ends up in a unit file and in the certificate's subject.
    [GeneratedRegex(@"^[A-Za-z0-9](?:[A-Za-z0-9.-]{0,251}[A-Za-z0-9])?$")]
    private static partial Regex HostName();
}

internal static class QuadletTextExtensions
{
    /// <summary>Replaces the one line equal to <paramref name="line"/>; throws unless there is exactly one.</summary>
    public static string ReplaceLine(this string text, string line, string replacement)
    {
        var lines = text.Split('\n');
        var matches = lines.Select((l, i) => (l, i)).Where(x => x.l.TrimEnd('\r') == line).ToList();
        if (matches.Count != 1)
            throw new InvalidOperationException(
                $"Expected the line \"{line}\" exactly once in the unit template, found it {matches.Count} times.");
        lines[matches[0].i] = replacement;
        return string.Join('\n', lines);
    }
}
