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
}

public class KanbanHub : Hub<IKanbanHubClient>;
