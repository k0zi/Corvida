using Corvida.Api.Auth;
using Corvida.Models;
using Microsoft.AspNetCore.SignalR;

namespace Corvida.Api.Hubs;

public interface IKanbanHubClient
{
    Task BoardChanged(Board board);
    Task BoardDeleted(string boardId);
    Task TaskChanged(string boardId, KanbanTask task);
    Task TaskDeleted(string boardId, string taskId);
    Task AgentChanged(Agent agent);
    Task AgentDeleted(string agentId);

    /// <summary>
    /// Sent when required authentication is switched on or off, immediately before existing
    /// connections are dropped, so clients can show a login screen instead of a connection error.
    /// </summary>
    Task AuthStateChanged(bool authRequired);
}

public class KanbanHub(IHubConnectionRegistry registry) : Hub<IKanbanHubClient>
{
    public override Task OnConnectedAsync()
    {
        registry.Add(new HubCallerContextEntry(Context.ConnectionId, Context.Abort));
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        registry.Remove(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}
