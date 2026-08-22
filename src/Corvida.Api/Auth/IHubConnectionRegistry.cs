namespace Corvida.Api.Auth;

/// <summary>
/// Tracks live hub connections so they can be dropped when the auth state flips.
/// </summary>
/// <remarks>
/// SignalR authorizes once, at connection time, and <see cref="Corvida.Api.Hubs.KanbanHub"/> has no
/// server-callable methods — so nothing ever re-authorizes. Connections opened while the gate was
/// open would otherwise keep receiving every broadcast forever after it closed. Aborting requires
/// the <c>HubCallerContext</c>, which is only reachable from a registry like this one; IHubContext
/// cannot do it.
/// </remarks>
public interface IHubConnectionRegistry
{
    void Add(HubCallerContextEntry entry);
    void Remove(string connectionId);

    /// <summary>Tells every client the auth state changed, then disconnects them all.</summary>
    Task AbortAllAsync(bool authRequired);
}

public sealed record HubCallerContextEntry(string ConnectionId, Action Abort);
