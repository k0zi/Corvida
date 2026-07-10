using Corvida.Api.Data;
using Corvida.Models;
using Microsoft.EntityFrameworkCore;

namespace Corvida.Api.Endpoints;

public static class TaskEndpoints
{
    public static void MapTaskEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/api/boards/{boardId}/tasks");

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

    static async Task<IResult> UpsertTask(string boardId, string taskId, KanbanTask task, AppDbContext db)
    {
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

        await db.SaveChangesAsync();
        return Results.Ok(ToModel(entity));
    }

    static async Task<IResult> DeleteTask(string boardId, string taskId, AppDbContext db)
    {
        var entity = await db.Tasks.FirstOrDefaultAsync(t => t.BoardId == boardId && t.Id == taskId);
        if (entity is null) return Results.NotFound();

        db.Tasks.Remove(entity);
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

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
    };
}
