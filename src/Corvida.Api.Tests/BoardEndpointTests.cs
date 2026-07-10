using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Corvida.Api.Data;
using Corvida.Api.Tests.Fixtures;
using Corvida.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Corvida.Api.Tests;

[Collection("postgres")]
public class BoardEndpointTests : IAsyncLifetime
{
    private readonly PostgresContainerFixture _db;
    private ApiWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    private static readonly JsonSerializerOptions JsonOpts =
        new() { PropertyNameCaseInsensitive = true };

    public BoardEndpointTests(PostgresContainerFixture db) => _db = db;

    public async Task InitializeAsync()
    {
        _factory = new ApiWebApplicationFactory(_db.ConnectionString);
        _client = _factory.CreateClient();

        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        ctx.Boards.RemoveRange(ctx.Boards);
        await ctx.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task GetBoards_ReturnsEmptyList()
    {
        var response = await _client.GetAsync("/api/boards");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var boards = await response.Content.ReadFromJsonAsync<List<Board>>(JsonOpts);
        Assert.NotNull(boards);
        Assert.Empty(boards);
    }

    [Fact]
    public async Task CreateBoard_Returns201_WithBoardAndThreeDefaultGroups()
    {
        var response = await _client.PostAsJsonAsync("/api/boards", new { Name = "Sprint 1" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var board = await response.Content.ReadFromJsonAsync<Board>(JsonOpts);
        Assert.NotNull(board);
        Assert.Equal("Sprint 1", board.Name);
        Assert.NotEmpty(board.Id);
        Assert.Equal(3, board.Groups.Count);
        Assert.Contains(board.Groups, g => g.Name == "To-Do");
        Assert.Contains(board.Groups, g => g.Name == "In-Progress");
        Assert.Contains(board.Groups, g => g.Name == "Done");
    }

    [Fact]
    public async Task GetBoard_Returns200_ForExistingBoard()
    {
        var created = await PostBoard("My Board");

        var response = await _client.GetAsync($"/api/boards/{created.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var board = await response.Content.ReadFromJsonAsync<Board>(JsonOpts);
        Assert.NotNull(board);
        Assert.Equal(created.Id, board.Id);
        Assert.Equal("My Board", board.Name);
        Assert.Equal(3, board.Groups.Count);
    }

    [Fact]
    public async Task GetBoard_Returns404_ForUnknownId()
    {
        var response = await _client.GetAsync("/api/boards/does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SaveBoard_Returns200_WithUpdatedBoard()
    {
        var created = await PostBoard("Original Name");
        created.Name = "Updated Name";

        var response = await _client.PutAsJsonAsync($"/api/boards/{created.Id}", created);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<Board>(JsonOpts);
        Assert.NotNull(updated);
        Assert.Equal("Updated Name", updated.Name);
        Assert.Equal(created.Id, updated.Id);
    }

    [Fact]
    public async Task SaveBoard_Returns404_ForUnknownId()
    {
        var board = new Board { Id = "ghost", Name = "Ghost" };

        var response = await _client.PutAsJsonAsync("/api/boards/ghost", board);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteBoard_Returns204_AndBoardIsGone()
    {
        var created = await PostBoard("To Delete");

        var deleteResponse = await _client.DeleteAsync($"/api/boards/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var getResponse = await _client.GetAsync($"/api/boards/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteBoard_Returns404_ForUnknownId()
    {
        var response = await _client.DeleteAsync("/api/boards/no-such-board");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteBoard_CascadesTaskDeletion()
    {
        var board = await PostBoard("Board With Task");
        var group = board.Groups[0];

        var task = new KanbanTask
        {
            Id = "cascade-task-1",
            BoardId = board.Id,
            GroupId = group.Id,
            Title = "Will Be Deleted",
            Created = DateTime.UtcNow,
        };
        await _client.PutAsJsonAsync($"/api/boards/{board.Id}/tasks/{task.Id}", task);

        await _client.DeleteAsync($"/api/boards/{board.Id}");

        var taskResponse = await _client.GetAsync($"/api/boards/{board.Id}/tasks/{task.Id}");
        Assert.Equal(HttpStatusCode.NotFound, taskResponse.StatusCode);
    }

    private async Task<Board> PostBoard(string name)
    {
        var response = await _client.PostAsJsonAsync("/api/boards", new { Name = name });
        response.EnsureSuccessStatusCode();
        var board = await response.Content.ReadFromJsonAsync<Board>(JsonOpts);
        return board!;
    }
}
