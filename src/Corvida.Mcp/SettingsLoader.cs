using Corvida.Services;
using Microsoft.Extensions.Hosting;

namespace Corvida.Mcp;

public sealed class SettingsLoader(ISettingsService settings) : IHostedService
{
    public Task StartAsync(CancellationToken _) => settings.LoadAsync();
    public Task StopAsync(CancellationToken _)  => Task.CompletedTask;
}
