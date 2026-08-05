using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using Corvida.Models;
using Corvida.Services;
using ModelContextProtocol.Server;

namespace Corvida.Mcp.Tools;

[McpServerToolType]
public sealed class TaskTools(IBoardService boards, ITaskService tasks)
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    [McpServerTool, Description(
        "List all tasks for a board. Fetches full details for every task across all groups.")]
    public async Task<string> list_tasks(
        [Description("The board ID")] string boardId,
        CancellationToken cancellationToken)
    {
        var all = await boards.GetBoardsAsync();
        var board = all.FirstOrDefault(b => b.Id == boardId);
        if (board is null) return """{"error":"Board not found"}""";

        var result = new List<KanbanTask>();
        foreach (var group in board.Groups)
            foreach (var taskId in group.TaskIds)
            {
                var t = await tasks.GetTaskAsync(boardId, taskId);
                if (t is not null) result.Add(t);
            }

        return JsonSerializer.Serialize(result, JsonOpts);
    }

    [McpServerTool, Description(
        "Get a single task's full details by board ID and task ID.")]
    public async Task<string> get_task(
        [Description("The board ID the task belongs to")] string boardId,
        [Description("The task ID")] string taskId,
        CancellationToken cancellationToken)
    {
        var task = await tasks.GetTaskAsync(boardId, taskId);
        return task is null
            ? """{"error":"Task not found"}"""
            : JsonSerializer.Serialize(task, JsonOpts);
    }

    [McpServerTool, Description(
        "Create a new task in a specific group on a board.")]
    public async Task<string> create_task(
        [Description("The board ID")] string boardId,
        [Description("The group (column) ID to place the task in")] string groupId,
        [Description("Task title")] string title,
        [Description("Task description (markdown supported)")] string description,
        [Description("Priority: Low, Medium, or High")] string priority,
        [Description("Optional planned start date in ISO-8601 format, e.g. 2026-06-15")] string? plannedStart,
        [Description("Optional planned end date in ISO-8601 format, e.g. 2026-06-30")] string? plannedEnd,
        CancellationToken cancellationToken)
    {
        var allBoards = await boards.GetBoardsAsync();
        var board = allBoards.FirstOrDefault(b => b.Id == boardId);
        var group = board?.Groups.FirstOrDefault(g => g.Id == groupId);
        if (group is null) return """{"error":"Group not found"}""";

        var task = new KanbanTask
        {
            Id = Guid.NewGuid().ToString("N")[..12],
            BoardId = boardId,
            GroupId = groupId,
            Title = title,
            Description = description,
            Priority = priority,
            Created = DateTime.UtcNow,
            PlannedStart = plannedStart is not null ? ParseUtc(plannedStart) : null,
            PlannedEnd   = plannedEnd   is not null ? ParseUtc(plannedEnd)   : null,
        };

        await tasks.SaveTaskAsync(task);
        group.TaskIds.Add(task.Id);
        RelocateInCellOrders(board!, task.Id, groupId, null);
        await boards.SaveBoardAsync(board!);

        return JsonSerializer.Serialize(new { task.Id, task.Title, task.GroupId }, JsonOpts);
    }

    [McpServerTool, Description(
        "Update fields on an existing task. Only non-null arguments are applied; omit a field to leave it unchanged.")]
    public async Task<string> update_task(
        [Description("The board ID")] string boardId,
        [Description("The task ID to update")] string taskId,
        [Description("New title, or null to keep existing")] string? title,
        [Description("New description (markdown), or null to keep existing")] string? description,
        [Description("New priority (Low/Medium/High), or null to keep existing")] string? priority,
        [Description("New planned start date ISO-8601, or null to keep existing")] string? plannedStart,
        [Description("New planned end date ISO-8601, or null to keep existing")] string? plannedEnd,
        CancellationToken cancellationToken)
    {
        var task = await tasks.GetTaskAsync(boardId, taskId);
        if (task is null) return """{"error":"Task not found"}""";

        if (title        is not null) task.Title        = title;
        if (description  is not null) task.Description  = description;
        if (priority     is not null) task.Priority     = priority;
        if (plannedStart is not null) task.PlannedStart = ParseUtc(plannedStart);
        if (plannedEnd   is not null) task.PlannedEnd   = ParseUtc(plannedEnd);

        await tasks.SaveTaskAsync(task);
        return JsonSerializer.Serialize(task, JsonOpts);
    }

    [McpServerTool, Description(
        "Delete a task permanently and remove it from its group's task list.")]
    public async Task<string> delete_task(
        [Description("The board ID")] string boardId,
        [Description("The task ID to delete")] string taskId,
        CancellationToken cancellationToken)
    {
        await tasks.DeleteTaskAsync(boardId, taskId);

        var allBoards = await boards.GetBoardsAsync();
        var board = allBoards.FirstOrDefault(b => b.Id == boardId);
        if (board is not null)
        {
            var changed = false;
            foreach (var group in board.Groups)
                changed |= group.TaskIds.Remove(taskId);
            foreach (var cell in board.CellOrders.ToList())
            {
                if (!cell.TaskIds.Remove(taskId)) continue;
                changed = true;
                if (cell.TaskIds.Count == 0) board.CellOrders.Remove(cell);
            }
            if (changed) await boards.SaveBoardAsync(board);
        }

        return """{"ok":true}""";
    }

    [McpServerTool, Description(
        "Move a task from one group (column) to another on the same board.")]
    public async Task<string> move_task(
        [Description("The board ID")] string boardId,
        [Description("The task ID to move")] string taskId,
        [Description("The destination group ID")] string toGroupId,
        CancellationToken cancellationToken)
    {
        var allBoards = await boards.GetBoardsAsync();
        var board = allBoards.FirstOrDefault(b => b.Id == boardId);
        if (board is null) return """{"error":"Board not found"}""";

        var destGroup = board.Groups.FirstOrDefault(g => g.Id == toGroupId);
        if (destGroup is null) return """{"error":"Destination group not found"}""";

        KanbanGroup? sourceGroup = null;
        foreach (var group in board.Groups)
            if (group.TaskIds.Remove(taskId)) { sourceGroup = group; break; }

        if (sourceGroup is null) return """{"error":"Task not found in any group"}""";

        destGroup.TaskIds.Add(taskId);

        var task = await tasks.GetTaskAsync(boardId, taskId);
        if (task is not null)
        {
            task.GroupId = toGroupId;
            await tasks.SaveTaskAsync(task);
            RelocateInCellOrders(board, taskId, toGroupId, task.AssignedAgentId);
        }

        await boards.SaveBoardAsync(board);
        return JsonSerializer.Serialize(new { taskId, from = sourceGroup.Id, to = toGroupId }, JsonOpts);
    }

    [McpServerTool, Description(
        "Assign a task to a agent (vertical placement in the board's swimlane grid), " +
        "or pass agentId=null to unassign it back to the Unassigned row.")]
    public async Task<string> assign_agent(
        [Description("The board ID")] string boardId,
        [Description("The task ID to assign")] string taskId,
        [Description("The agent ID to assign the task to, or null to unassign")] string? agentId,
        CancellationToken cancellationToken)
    {
        var allBoards = await boards.GetBoardsAsync();
        var board = allBoards.FirstOrDefault(b => b.Id == boardId);
        if (board is null) return """{"error":"Board not found"}""";

        var task = await tasks.GetTaskAsync(boardId, taskId);
        if (task is null) return """{"error":"Task not found"}""";

        task.AssignedAgentId = agentId;
        await tasks.SaveTaskAsync(task);

        RelocateInCellOrders(board, taskId, task.GroupId, agentId);
        await boards.SaveBoardAsync(board);

        return JsonSerializer.Serialize(new { taskId, assignedAgentId = agentId }, JsonOpts);
    }

    // The single source of truth for "which cell (group, agent) a task visually sits in" is
    // Board.CellOrders. Both a group move and a agent (re)assignment need to relocate the task's
    // entry there — this keeps the two in sync instead of duplicating the removal/insertion logic.
    private static void RelocateInCellOrders(Board board, string taskId, string groupId, string? agentId)
    {
        foreach (var cell in board.CellOrders.ToList())
        {
            if (cell.TaskIds.Remove(taskId) && cell.TaskIds.Count == 0)
                board.CellOrders.Remove(cell);
        }

        var target = board.CellOrders.FirstOrDefault(c => c.GroupId == groupId && c.AgentId == agentId);
        if (target is null)
        {
            target = new SwimlaneCellOrder { GroupId = groupId, AgentId = agentId };
            board.CellOrders.Add(target);
        }
        if (!target.TaskIds.Contains(taskId))
            target.TaskIds.Add(taskId);
    }

    private static DateTime ParseUtc(string value) => DateTime.Parse(
        value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
}
