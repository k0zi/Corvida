using System.Threading.Tasks;
using Corvida.Models;
using Corvida.Services;

namespace Corvida.Web.Services;

/// <summary>
/// Browser build has no local filesystem, so the server URL is fixed at build time
/// instead of being loaded from/saved to disk like the desktop <see cref="SettingsService"/>.
/// </summary>
public class StaticSettingsService : ISettingsService
{
    private const string CorvidaApiUrl = "http://localhost:5083";

    public AppSettings Settings { get; } = new()
    {
        StorageMode = StorageMode.ServerHosted,
        ServerUrl = CorvidaApiUrl,
    };

    public Task LoadAsync() => Task.CompletedTask;

    public Task SaveAsync() => Task.CompletedTask;
}
