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
public class TaskEndpointTests : IAsyncLifetime
{
    private readonly PostgresContainerFixture _db;
    private ApiWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;
    private Board _seedBoard = null!;

    private static readonly JsonSerializerOptions JsonOpts =
        new() { PropertyNameCaseInsensitive = true };

    public TaskEndpointTests(PostgresContainerFixture db) => _db = db;

    public async Task InitializeAsync()
    {
        _factory = new ApiWebApplicationFactory(_db.ConnectionString);
        _client = _factory.CreateClient();

        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        ctx.Boards.RemoveRange(ctx.Boards);
        await ctx.SaveChangesAsync();

        var boardResponse = await _client.PostAsJsonAsync("/api/boards", new { Name = "Test Board" });
        boardResponse.EnsureSuccessStatusCode();
        _seedBoard = (await boardResponse.Content.ReadFromJsonAsync<Board>(JsonOpts))!;
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task GetTask_Returns404_WhenNotFound()
    {
        var response = await _client.GetAsync($"/api/boards/{_seedBoard.Id}/tasks/no-such-task");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpsertTask_Returns200_AndCreatesTask()
    {
        var task = MakeTask("new-task-1");

        var response = await _client.PutAsJsonAsync(
            $"/api/boards/{_seedBoard.Id}/tasks/{task.Id}", task);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var returned = await response.Content.ReadFromJsonAsync<KanbanTask>(JsonOpts);
        Assert.NotNull(returned);
        Assert.Equal("new-task-1", returned.Id);
        Assert.Equal("Task Title", returned.Title);
        Assert.Equal(_seedBoard.Id, returned.BoardId);
    }

    [Fact]
    public async Task GetTask_Returns200_WithAllFields()
    {
        var task = MakeTask("get-task-1");
        await _client.PutAsJsonAsync($"/api/boards/{_seedBoard.Id}/tasks/{task.Id}", task);

        var response = await _client.GetAsync($"/api/boards/{_seedBoard.Id}/tasks/{task.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var returned = await response.Content.ReadFromJsonAsync<KanbanTask>(JsonOpts);
        Assert.NotNull(returned);
        Assert.Equal(task.Id, returned.Id);
        Assert.Equal(task.Title, returned.Title);
        Assert.Equal(task.Description, returned.Description);
        Assert.Equal(task.Priority, returned.Priority);
        Assert.Equal(task.GroupId, returned.GroupId);
        Assert.Equal(_seedBoard.Id, returned.BoardId);
    }

    [Fact]
    public async Task UpsertTask_Returns200_AndUpdatesExistingTask()
    {
        var task = MakeTask("update-task-1");
        await _client.PutAsJsonAsync($"/api/boards/{_seedBoard.Id}/tasks/{task.Id}", task);

        task.Title = "Updated Title";
        var response = await _client.PutAsJsonAsync(
            $"/api/boards/{_seedBoard.Id}/tasks/{task.Id}", task);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var returned = await response.Content.ReadFromJsonAsync<KanbanTask>(JsonOpts);
        Assert.NotNull(returned);
        Assert.Equal("Updated Title", returned.Title);
    }

    [Fact]
    public async Task UpsertTask_PersistsOptionalDates()
    {
        var task = MakeTask("dates-task-1");
        task.PlannedStart = new DateTime(2026, 7, 1, 9, 0, 0, DateTimeKind.Utc);
        task.PlannedEnd = new DateTime(2026, 7, 31, 17, 0, 0, DateTimeKind.Utc);

        await _client.PutAsJsonAsync($"/api/boards/{_seedBoard.Id}/tasks/{task.Id}", task);

        var response = await _client.GetAsync($"/api/boards/{_seedBoard.Id}/tasks/{task.Id}");
        var returned = await response.Content.ReadFromJsonAsync<KanbanTask>(JsonOpts);
        Assert.NotNull(returned);
        Assert.Equal(task.PlannedStart, returned.PlannedStart);
        Assert.Equal(task.PlannedEnd, returned.PlannedEnd);
    }

    [Fact]
    public async Task DeleteTask_Returns204_AndTaskIsGone()
    {
        var task = MakeTask("delete-task-1");
        await _client.PutAsJsonAsync($"/api/boards/{_seedBoard.Id}/tasks/{task.Id}", task);

        var deleteResponse = await _client.DeleteAsync(
            $"/api/boards/{_seedBoard.Id}/tasks/{task.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var getResponse = await _client.GetAsync($"/api/boards/{_seedBoard.Id}/tasks/{task.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteTask_Returns404_ForUnknownTask()
    {
        var response = await _client.DeleteAsync(
            $"/api/boards/{_seedBoard.Id}/tasks/ghost-task");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private KanbanTask MakeTask(string id) => new()
    {
        Id = id,
        BoardId = _seedBoard.Id,
        GroupId = _seedBoard.Groups[0].Id,
        Title = "Task Title",
        Description = "Task description.",
        Priority = "High",
        Created = new DateTime(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc),
    };
}
