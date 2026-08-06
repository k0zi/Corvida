using System.Text.Json;
using Corvida.Api.Data;
using Corvida.Api.Hubs;
using Corvida.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Corvida.Api.Endpoints;

public static class AgentEndpoints
{
    private static readonly JsonSerializerOptions JsonOpts = new();

    public static void MapAgentEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/api/agents");

        g.MapGet("/", GetAgents);
        g.MapGet("/{id}", GetAgent);
        g.MapPost("/", CreateAgent);
        g.MapPut("/{id}", SaveAgent);
        g.MapDelete("/{id}", DeleteAgent);
    }

    static async Task<IResult> GetAgents(AppDbContext db)
    {
        var entities = await db.Agents.AsNoTracking().ToListAsync();
        return Results.Ok(entities.Select(ToModel).ToList());
    }

    static async Task<IResult> GetAgent(string id, AppDbContext db)
    {
        var entity = await db.Agents.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id);
        return entity is null ? Results.NotFound() : Results.Ok(ToModel(entity));
    }

    static async Task<IResult> CreateAgent(CreateAgentRequest req, AppDbContext db, IHubContext<KanbanHub, IKanbanHubClient> hub)
    {
        var entity = new AgentEntity
        {
            Id = $"{req.Name}-agt-{Guid.NewGuid().ToString("N")[..8]}",
            Name = req.Name,
        };

        db.Agents.Add(entity);
        await db.SaveChangesAsync();

        var model = ToModel(entity);
        await hub.Clients.All.AgentChanged(model);
        return Results.Created($"/api/agents/{entity.Id}", model);
    }

    static async Task<IResult> SaveAgent(string id, Agent agent, AppDbContext db, IHubContext<KanbanHub, IKanbanHubClient> hub)
    {
        var entity = await db.Agents.FirstOrDefaultAsync(a => a.Id == id);
        if (entity is null) return Results.NotFound();

        entity.Name = agent.Name;
        entity.Description = agent.Description;
        entity.Personality = agent.Personality;
        entity.Color = agent.Color;
        entity.AvatarDataUri = agent.AvatarDataUri;

        await db.SaveChangesAsync();

        var model = ToModel(entity);
        await hub.Clients.All.AgentChanged(model);
        return Results.Ok(model);
    }

    // AgentIdsJson/CellOrdersJson are jsonb blobs on BoardEntity, not FK-backed, so they must
    // be scrubbed manually here. Deletion is refused outright while any task is still assigned
    // to the agent, so the AssignedAgentId FK's OnDelete(SetNull) is a safety net that should
    // never actually fire in practice.
    static async Task<IResult> DeleteAgent(string id, AppDbContext db, IHubContext<KanbanHub, IKanbanHubClient> hub)
    {
        var entity = await db.Agents.FirstOrDefaultAsync(a => a.Id == id);
        if (entity is null) return Results.NotFound();

        var hasAssignedTasks = await db.Tasks.AnyAsync(t => t.AssignedAgentId == id);
        if (hasAssignedTasks)
            return Results.Conflict(new { error = "Agent has assigned tasks and cannot be deleted. Unassign their tasks first." });

        var boards = await db.Boards.ToListAsync();
        var touched = new List<BoardEntity>();

        foreach (var board in boards)
        {
            var agentIds = JsonSerializer.Deserialize<List<string>>(board.AgentIdsJson, JsonOpts) ?? [];
            var boardChanged = agentIds.Remove(id);
            if (boardChanged)
                board.AgentIdsJson = JsonSerializer.Serialize(agentIds, JsonOpts);

            var cellOrders = JsonSerializer.Deserialize<List<SwimlaneCellOrder>>(board.CellOrdersJson, JsonOpts) ?? [];
            var orphaned = cellOrders.Where(c => c.AgentId == id).ToList();
            foreach (var cell in orphaned)
            {
                var unassigned = cellOrders.FirstOrDefault(c => c.GroupId == cell.GroupId && c.AgentId is null);
                if (unassigned is null)
                {
                    unassigned = new SwimlaneCellOrder { GroupId = cell.GroupId, AgentId = null };
                    cellOrders.Add(unassigned);
                }
                foreach (var taskId in cell.TaskIds)
                    if (!unassigned.TaskIds.Contains(taskId))
                        unassigned.TaskIds.Add(taskId);

                cellOrders.Remove(cell);
                boardChanged = true;
            }
            if (orphaned.Count > 0)
                board.CellOrdersJson = JsonSerializer.Serialize(cellOrders, JsonOpts);

            if (boardChanged)
                touched.Add(board);
        }

        db.Agents.Remove(entity);
        await db.SaveChangesAsync();

        await hub.Clients.All.AgentDeleted(id);
        foreach (var board in touched)
            await hub.Clients.All.BoardChanged(BoardEndpoints.ToModel(board));

        return Results.NoContent();
    }

    internal static Agent ToModel(AgentEntity e) => new()
    {
        Id = e.Id,
        Name = e.Name,
        Description = e.Description,
        Personality = e.Personality,
        Color = e.Color,
        AvatarDataUri = e.AvatarDataUri,
    };
}

public record CreateAgentRequest(string Name);
