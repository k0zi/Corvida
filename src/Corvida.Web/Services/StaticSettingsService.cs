using System.Threading.Tasks;
using Corvida.Models;
using Corvida.Services;

namespace Corvida.Web.Services;

/// <summary>
/// The browser build has no local filesystem, so settings are fixed for the lifetime of the page
/// rather than loaded from and saved to disk like the desktop <see cref="SettingsService"/>.
/// </summary>
/// <remarks>
/// The API address is resolved once at startup — see <c>Program.ResolveApiBaseUrlAsync</c> — so the
/// same published bundle works wherever it is served from.
/// </remarks>
public class StaticSettingsService(string apiBaseUrl) : ISettingsService
{
    public AppSettings Settings { get; } = new()
    {
        StorageMode = StorageMode.ServerHosted,
        ServerUrl = apiBaseUrl,
    };

    public Task LoadAsync() => Task.CompletedTask;

    public Task SaveAsync() => Task.CompletedTask;
}
