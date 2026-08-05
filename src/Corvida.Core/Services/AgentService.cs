using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Corvida.Models;

namespace Corvida.Services;

public class AgentService : IAgentService
{
    private readonly ISettingsService _settings;
    private readonly IBoardService _boards;
    private readonly ITaskService _tasks;

    public AgentService(ISettingsService settings, IBoardService boards, ITaskService tasks)
    {
        _settings = settings;
        _boards = boards;
        _tasks = tasks;
    }

    private string AgentsRoot => Path.Combine(_settings.Settings.DataPath, "agents");

    private string AgentFile(string agentId) => Path.Combine(AgentsRoot, agentId + ".md");

    public async Task<List<Agent>> GetAgentsAsync()
    {
        var result = new List<Agent>();
        if (!Directory.Exists(AgentsRoot)) return result;

        foreach (var file in Directory.GetFiles(AgentsRoot, "*.md"))
        {
            var text = await File.ReadAllTextAsync(file);
            result.Add(AgentMarkdownSerializer.Parse(text));
        }

        return result;
    }

    public async Task<Agent?> GetAgentAsync(string agentId)
    {
        var path = AgentFile(agentId);
        if (!File.Exists(path)) return null;

        var text = await File.ReadAllTextAsync(path);
        return AgentMarkdownSerializer.Parse(text);
    }

    public async Task<Agent> CreateAgentAsync(string name)
    {
        var agent = new Agent
        {
            Id = name + "-agt-" + System.Guid.NewGuid().ToString("N")[..8],
            Name = name
        };
        await SaveAgentAsync(agent);
        return agent;
    }

    public async Task SaveAgentAsync(Agent agent)
    {
        Directory.CreateDirectory(AgentsRoot);
        await File.WriteAllTextAsync(AgentFile(agent.Id), AgentMarkdownSerializer.Serialize(agent));
    }

    // No relational cascade exists in file storage, so deleting an agent must manually
    // scrub board membership and per-cell task ordering across every (non-archived) board.
    // Archived boards are skipped since IBoardService/ITaskService both refuse writes
    // against them, matching the app's existing archived-board invariant.
    // Deletion is refused outright while any task is still assigned to the agent.
    public async Task DeleteAgentAsync(string agentId)
    {
        var boards = await _boards.GetBoardsAsync();

        foreach (var board in boards)
        foreach (var group in board.Groups)
        foreach (var taskId in group.TaskIds)
        {
            var task = await _tasks.GetTaskAsync(board.Id, taskId);
            if (task is not null && task.AssignedAgentId == agentId)
                throw new InvalidOperationException(
                    "Agent has assigned tasks and cannot be deleted. Unassign their tasks first.");
        }

        var path = AgentFile(agentId);
        if (File.Exists(path)) File.Delete(path);

        foreach (var board in boards)
        {
            var boardChanged = board.AgentIds.Remove(agentId);

            var orphanedCells = board.CellOrders.Where(c => c.AgentId == agentId).ToList();
            foreach (var cell in orphanedCells)
            {
                var unassigned = board.CellOrders.FirstOrDefault(
                    c => c.GroupId == cell.GroupId && c.AgentId is null);
                if (unassigned is null)
                {
                    unassigned = new SwimlaneCellOrder { GroupId = cell.GroupId, AgentId = null };
                    board.CellOrders.Add(unassigned);
                }
                foreach (var taskId in cell.TaskIds)
                    if (!unassigned.TaskIds.Contains(taskId))
                        unassigned.TaskIds.Add(taskId);

                board.CellOrders.Remove(cell);
                boardChanged = true;
            }

            if (boardChanged)
                await _boards.SaveBoardAsync(board);
        }
    }
}
