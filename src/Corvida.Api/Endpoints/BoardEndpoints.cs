using System.Text.Json;
using Corvida.Api.Data;
using Corvida.Api.Hubs;
using Corvida.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Corvida.Api.Endpoints;

public static class BoardEndpoints
{
    private static readonly JsonSerializerOptions JsonOpts = new();

    public static void MapBoardEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/api/boards");

        g.MapGet("/", GetBoards);
        g.MapPost("/", CreateBoard);
        g.MapGet("/{id}", GetBoard);
        g.MapPut("/{id}", SaveBoard);
        g.MapDelete("/{id}", DeleteBoard);
    }

    static async Task<IResult> GetBoards(AppDbContext db)
    {
        var entities = await db.Boards.AsNoTracking().ToListAsync();
        return Results.Ok(entities.Select(ToModel).ToList());
    }

    static async Task<IResult> GetBoard(string id, AppDbContext db)
    {
        var entity = await db.Boards.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id);
        return entity is null ? Results.NotFound() : Results.Ok(ToModel(entity));
    }

    static async Task<IResult> CreateBoard(CreateBoardRequest req, AppDbContext db, IHubContext<KanbanHub, IKanbanHubClient> hub)
    {
        var boardId = $"{req.Name}-brd-{Guid.NewGuid().ToString("N")[..8]}";
        var groups = new List<KanbanGroup>
        {
            new() { Id = $"To-Do-grp-{Guid.NewGuid().ToString("N")[..8]}", Name = "To-Do" },
            new() { Id = $"In-Progress-grp-{Guid.NewGuid().ToString("N")[..8]}", Name = "In-Progress" },
            new() { Id = $"Done-grp-{Guid.NewGuid().ToString("N")[..8]}", Name = "Done" },
        };

        var entity = new BoardEntity
        {
            Id = boardId,
            Name = req.Name,
            GroupsJson = JsonSerializer.Serialize(groups, JsonOpts),
        };

        db.Boards.Add(entity);
        await db.SaveChangesAsync();

        var model = ToModel(entity);
        await hub.Clients.All.BoardChanged(model);
        return Results.Created($"/api/boards/{entity.Id}", model);
    }

    static async Task<IResult> SaveBoard(string id, Board board, AppDbContext db, IHubContext<KanbanHub, IKanbanHubClient> hub)
    {
        var entity = await db.Boards.FirstOrDefaultAsync(b => b.Id == id);
        if (entity is null) return Results.NotFound();

        entity.Name = board.Name;
        entity.GroupsJson = JsonSerializer.Serialize(board.Groups, JsonOpts);

        await db.SaveChangesAsync();

        var model = ToModel(entity);
        await hub.Clients.All.BoardChanged(model);
        return Results.Ok(model);
    }

    static async Task<IResult> DeleteBoard(string id, AppDbContext db, IHubContext<KanbanHub, IKanbanHubClient> hub)
    {
        var entity = await db.Boards.FirstOrDefaultAsync(b => b.Id == id);
        if (entity is null) return Results.NotFound();

        db.Boards.Remove(entity);
        await db.SaveChangesAsync();

        await hub.Clients.All.BoardDeleted(id);
        return Results.NoContent();
    }

    internal static Board ToModel(BoardEntity e) => new()
    {
        Id = e.Id,
        Name = e.Name,
        Groups = JsonSerializer.Deserialize<List<KanbanGroup>>(e.GroupsJson, JsonOpts) ?? [],
    };
}

public record CreateBoardRequest(string Name);
