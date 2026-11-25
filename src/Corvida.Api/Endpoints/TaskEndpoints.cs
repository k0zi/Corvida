using System.Text.Json;
using Corvida.Api.Auth;
using Corvida.Api.Data;
using Corvida.Api.Hubs;
using Corvida.Models;
using Corvida.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Corvida.Api.Endpoints;

public static class TaskEndpoints
{
    private static readonly JsonSerializerOptions JsonOpts = new();

    public static void MapTaskEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/api/boards/{boardId}/tasks").RequireAuthorization(Policies.User);

        g.MapGet("/{taskId}", GetTask);
        g.MapPut("/{taskId}", UpsertTask);
        g.MapDelete("/{taskId}", DeleteTask);
    }

    static async Task<IResult> GetTask(string boardId, string taskId, AppDbContext db)
    {
        var entity = await db.Tasks.AsNoTracking()
            .FirstOrDefaultAsync(t => t.BoardId == boardId && t.Id == taskId);
        return entity is null ? Results.NotFound() : Results.Ok(ToModel(entity));
    }

    static async Task<IResult> UpsertTask(string boardId, string taskId, KanbanTask task, AppDbContext db, IHubContext<KanbanHub, IKanbanHubClient> hub)
    {
        if (await IsBoardArchived(boardId, db)) return Results.Conflict("This board is archived and cannot be modified.");

        // Every client checks this too, but the MCP server and any other HTTP caller only meet it here.
        var dependsOn = task.DependsOn ?? [];
        if (dependsOn.Count > 0)
        {
            var others = (await db.Tasks.AsNoTracking()
                    .Where(t => t.BoardId == boardId && t.Id != taskId)
                    .ToListAsync())
                .Select(ToModel)
                .ToDictionary(t => t.Id);

            var candidate = new KanbanTask { Id = taskId, DependsOn = dependsOn };
            candidate.DependsOn = TaskDependencies.Normalize(candidate, others.Keys);
            if (TaskDependencies.HasCycleThrough(others, candidate))
                return Results.BadRequest("Dependency cycle: the task would end up waiting for itself.");
            dependsOn = candidate.DependsOn;
        }

        var entity = await db.Tasks.FirstOrDefaultAsync(t => t.BoardId == boardId && t.Id == taskId);

        if (entity is null)
        {
            entity = new TaskEntity { Id = taskId, BoardId = boardId };
            db.Tasks.Add(entity);
        }

        entity.GroupId = task.GroupId;
        entity.Title = task.Title;
        entity.Description = task.Description;
        entity.Priority = task.Priority;
        entity.Created = task.Created;
        entity.PlannedStart = task.PlannedStart;
        entity.PlannedEnd = task.PlannedEnd;
        entity.AssignedAgentId = task.AssignedAgentId;
        entity.DependsOnJson = JsonSerializer.Serialize(dependsOn, JsonOpts);
        entity.EstimateOptimistic = task.EstimateOptimistic;
        entity.EstimateMostLikely = task.EstimateMostLikely;
        entity.EstimatePessimistic = task.EstimatePessimistic;

        await db.SaveChangesAsync();

        var model = ToModel(entity);
        await hub.Clients.All.TaskChanged(boardId, model);
        return Results.Ok(model);
    }

    static async Task<IResult> DeleteTask(string boardId, string taskId, AppDbContext db, IHubContext<KanbanHub, IKanbanHubClient> hub)
    {
        if (await IsBoardArchived(boardId, db)) return Results.Conflict("This board is archived and cannot be modified.");

        var entity = await db.Tasks.FirstOrDefaultAsync(t => t.BoardId == boardId && t.Id == taskId);
        if (entity is null) return Results.NotFound();

        db.Tasks.Remove(entity);

        // A deleted task must not linger as a prerequisite of the others.
        var dependents = new List<KanbanTask>();
        var others = await db.Tasks.Where(t => t.BoardId == boardId && t.Id != taskId).ToListAsync();
        foreach (var other in others)
        {
            var model = ToModel(other);
            if (TaskDependencies.RemoveReferences([model], taskId).Count == 0) continue;
            other.DependsOnJson = JsonSerializer.Serialize(model.DependsOn, JsonOpts);
            dependents.Add(model);
        }

        await db.SaveChangesAsync();

        await hub.Clients.All.TaskDeleted(boardId, taskId);
        foreach (var dependent in dependents)
            await hub.Clients.All.TaskChanged(boardId, dependent);
        return Results.NoContent();
    }

    private static async Task<bool> IsBoardArchived(string boardId, AppDbContext db) =>
        await db.Boards.AsNoTracking()
            .Where(b => b.Id == boardId)
            .Select(b => b.IsArchived)
            .FirstOrDefaultAsync();

    private static KanbanTask ToModel(TaskEntity e) => new()
    {
        Id = e.Id,
        Title = e.Title,
        Description = e.Description,
        GroupId = e.GroupId,
        BoardId = e.BoardId,
        Created = e.Created,
        Priority = e.Priority,
        PlannedStart = e.PlannedStart,
        PlannedEnd = e.PlannedEnd,
        AssignedAgentId = e.AssignedAgentId,
        DependsOn = JsonSerializer.Deserialize<List<string>>(e.DependsOnJson, JsonOpts) ?? [],
        EstimateOptimistic = e.EstimateOptimistic,
        EstimateMostLikely = e.EstimateMostLikely,
        EstimatePessimistic = e.EstimatePessimistic,
    };
}
