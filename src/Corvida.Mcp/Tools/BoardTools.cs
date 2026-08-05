using System.ComponentModel;
using System.Text.Json;
using Corvida.Models;
using Corvida.Services;
using ModelContextProtocol.Server;

namespace Corvida.Mcp.Tools;

[McpServerToolType]
public sealed class BoardTools(IBoardService boards, ITaskService tasks, IAgentService agents)
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    [McpServerTool, Description(
        "List all Kanban boards. Returns each board's ID, name, group names, and task count per group.")]
    public async Task<string> list_boards(CancellationToken cancellationToken)
    {
        var all = await boards.GetBoardsAsync();
        var result = all.Select(b => new
        {
            b.Id,
            b.Name,
            Groups = b.Groups.Select(g => new { g.Id, g.Name, TaskCount = g.TaskIds.Count })
        });
        return JsonSerializer.Serialize(result, JsonOpts);
    }

    [McpServerTool, Description(
        "Get a board by ID with its full group structure and task ID lists.")]
    public async Task<string> get_board(
        [Description("The board ID")] string boardId,
        CancellationToken cancellationToken)
    {
        var all = await boards.GetBoardsAsync();
        var board = all.FirstOrDefault(b => b.Id == boardId);
        return board is null
            ? """{"error":"Board not found"}"""
            : JsonSerializer.Serialize(board, JsonOpts);
    }

    [McpServerTool, Description(
        "Create a new board. Automatically creates To-Do, In-Progress, and Done groups.")]
    public async Task<string> create_board(
        [Description("Human-readable board name, e.g. 'My Project'")] string name,
        CancellationToken cancellationToken)
    {
        var board = await boards.CreateBoardAsync(name);
        return JsonSerializer.Serialize(new { board.Id, board.Name, GroupCount = board.Groups.Count }, JsonOpts);
    }

    [McpServerTool, Description(
        "Delete a board and all its tasks permanently. This cannot be undone.")]
    public async Task<string> delete_board(
        [Description("The board ID to delete")] string boardId,
        CancellationToken cancellationToken)
    {
        var all = await boards.GetBoardsAsync();
        var board = all.FirstOrDefault(b => b.Id == boardId);
        if (board is not null)
        {
            foreach (var group in board.Groups)
                foreach (var taskId in group.TaskIds)
                    await tasks.DeleteTaskAsync(boardId, taskId);
        }

        await boards.DeleteBoardAsync(boardId);
        return """{"ok":true}""";
    }

    [McpServerTool, Description(
        "Add a new column (group) to a board.")]
    public async Task<string> add_group(
        [Description("The board ID")] string boardId,
        [Description("Name for the new column, e.g. 'Review'")] string name,
        CancellationToken cancellationToken)
    {
        var all = await boards.GetBoardsAsync();
        var board = all.FirstOrDefault(b => b.Id == boardId);
        if (board is null) return """{"error":"Board not found"}""";

        var group = new KanbanGroup
        {
            Id = $"{name}-grp-{Guid.NewGuid().ToString("N")[..8]}",
            Name = name
        };
        board.Groups.Add(group);
        await boards.SaveBoardAsync(board);
        return JsonSerializer.Serialize(new { group.Id, group.Name }, JsonOpts);
    }

    [McpServerTool, Description(
        "Rename an existing group (column) on a board.")]
    public async Task<string> rename_group(
        [Description("The board ID")] string boardId,
        [Description("The group ID to rename")] string groupId,
        [Description("The new name")] string newName,
        CancellationToken cancellationToken)
    {
        var all = await boards.GetBoardsAsync();
        var board = all.FirstOrDefault(b => b.Id == boardId);
        var group = board?.Groups.FirstOrDefault(g => g.Id == groupId);
        if (group is null) return """{"error":"Group not found"}""";

        group.Name = newName;
        await boards.SaveBoardAsync(board!);
        return JsonSerializer.Serialize(new { group.Id, group.Name }, JsonOpts);
    }

    [McpServerTool, Description(
        "Delete a group (column) from a board. " +
        "Pass deleteTasks=true to also delete all tasks in the group, " +
        "or false to leave task files on disk while removing the group.")]
    public async Task<string> delete_group(
        [Description("The board ID")] string boardId,
        [Description("The group ID to delete")] string groupId,
        [Description("If true, also permanently delete all tasks that belong to this group")] bool deleteTasks,
        CancellationToken cancellationToken)
    {
        var all = await boards.GetBoardsAsync();
        var board = all.FirstOrDefault(b => b.Id == boardId);
        var group = board?.Groups.FirstOrDefault(g => g.Id == groupId);
        if (group is null) return """{"error":"Group not found"}""";

        if (deleteTasks)
            foreach (var taskId in group.TaskIds)
                await tasks.DeleteTaskAsync(boardId, taskId);

        board!.Groups.Remove(group);
        board.CellOrders.RemoveAll(c => c.GroupId == groupId);
        await boards.SaveBoardAsync(board);
        return JsonSerializer.Serialize(new { deleted = groupId, tasksDeleted = deleteTasks ? group.TaskIds.Count : 0 }, JsonOpts);
    }

    [McpServerTool, Description(
        "Add a agent as a member of a board, making them a swimlane row. The agent must already exist.")]
    public async Task<string> add_board_member(
        [Description("The board ID")] string boardId,
        [Description("The agent ID to add as a board member")] string agentId,
        CancellationToken cancellationToken)
    {
        var all = await boards.GetBoardsAsync();
        var board = all.FirstOrDefault(b => b.Id == boardId);
        if (board is null) return """{"error":"Board not found"}""";

        if (await agents.GetAgentAsync(agentId) is null) return """{"error":"Agent not found"}""";

        if (!board.AgentIds.Contains(agentId))
            board.AgentIds.Add(agentId);

        await boards.SaveBoardAsync(board);
        return JsonSerializer.Serialize(new { boardId, members = board.AgentIds }, JsonOpts);
    }

    [McpServerTool, Description(
        "Remove a agent from a board's membership. Tasks previously assigned to that agent on this " +
        "board fall back to Unassigned; the agent itself is not deleted.")]
    public async Task<string> remove_board_member(
        [Description("The board ID")] string boardId,
        [Description("The agent ID to remove from board membership")] string agentId,
        CancellationToken cancellationToken)
    {
        var all = await boards.GetBoardsAsync();
        var board = all.FirstOrDefault(b => b.Id == boardId);
        if (board is null) return """{"error":"Board not found"}""";

        board.AgentIds.Remove(agentId);
        ScrubAgentFromBoard(board, agentId);

        await boards.SaveBoardAsync(board);
        return JsonSerializer.Serialize(new { boardId, members = board.AgentIds }, JsonOpts);
    }

    [McpServerTool, Description(
        "Reorder a board's member list (swimlane row order). " +
        "orderedAgentIds must contain exactly the board's current members, in the desired order.")]
    public async Task<string> reorder_board_members(
        [Description("The board ID")] string boardId,
        [Description("The full member list in the desired order — must be a permutation of the board's current members")]
        List<string> orderedAgentIds,
        CancellationToken cancellationToken)
    {
        var all = await boards.GetBoardsAsync();
        var board = all.FirstOrDefault(b => b.Id == boardId);
        if (board is null) return """{"error":"Board not found"}""";

        if (orderedAgentIds.Count != board.AgentIds.Count ||
            !orderedAgentIds.OrderBy(x => x).SequenceEqual(board.AgentIds.OrderBy(x => x)))
            return """{"error":"orderedAgentIds must be a permutation of the board's current members"}""";

        board.AgentIds = orderedAgentIds;
        await boards.SaveBoardAsync(board);
        return JsonSerializer.Serialize(new { boardId, members = board.AgentIds }, JsonOpts);
    }

    // Removing a agent's board membership (or deleting them globally) leaves their assigned
    // cell-order entries orphaned; merge each into that group's Unassigned entry so tasks stay
    // visible instead of vanishing from the swimlane grid.
    private static void ScrubAgentFromBoard(Board board, string agentId)
    {
        var orphaned = board.CellOrders.Where(c => c.AgentId == agentId).ToList();
        foreach (var cell in orphaned)
        {
            var unassigned = board.CellOrders.FirstOrDefault(c => c.GroupId == cell.GroupId && c.AgentId is null);
            if (unassigned is null)
            {
                unassigned = new SwimlaneCellOrder { GroupId = cell.GroupId, AgentId = null };
                board.CellOrders.Add(unassigned);
            }
            foreach (var taskId in cell.TaskIds)
                if (!unassigned.TaskIds.Contains(taskId))
                    unassigned.TaskIds.Add(taskId);

            board.CellOrders.Remove(cell);
        }
    }
}
