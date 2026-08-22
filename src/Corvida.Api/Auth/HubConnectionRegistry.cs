using System.Collections.Concurrent;
using Corvida.Api.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace Corvida.Api.Auth;

/// <inheritdoc />
internal sealed class HubConnectionRegistry(
    IHubContext<KanbanHub, IKanbanHubClient> hub,
    ILogger<HubConnectionRegistry> logger) : IHubConnectionRegistry
{
    private readonly ConcurrentDictionary<string, HubCallerContextEntry> _connections = new();

    public void Add(HubCallerContextEntry entry) => _connections[entry.ConnectionId] = entry;

    public void Remove(string connectionId) => _connections.TryRemove(connectionId, out _);

    public async Task AbortAllAsync(bool authRequired)
    {
        // Broadcast before aborting, so clients show a login screen rather than a
        // "server unreachable" error.
        try
        {
            await hub.Clients.All.AuthStateChanged(authRequired);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not broadcast the auth state change");
        }

        foreach (var entry in _connections.Values)
        {
            try
            {
                entry.Abort();
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Could not abort connection {ConnectionId}", entry.ConnectionId);
            }
        }

        _connections.Clear();
    }
}
