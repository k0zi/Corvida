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
public class AgentEndpointTests : IAsyncLifetime
{
    private readonly PostgresContainerFixture _db;
    private ApiWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    private static readonly JsonSerializerOptions JsonOpts =
        new() { PropertyNameCaseInsensitive = true };

    public AgentEndpointTests(PostgresContainerFixture db) => _db = db;

    public async Task InitializeAsync()
    {
        _factory = new ApiWebApplicationFactory(_db.ConnectionString);
        _client = _factory.CreateClient();

        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        ctx.Agents.RemoveRange(ctx.Agents);
        ctx.Boards.RemoveRange(ctx.Boards);
        await ctx.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task GetAgents_ReturnsEmptyList()
    {
        var response = await _client.GetAsync("/api/agents");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var agents = await response.Content.ReadFromJsonAsync<List<Agent>>(JsonOpts);
        Assert.NotNull(agents);
        Assert.Empty(agents);
    }

    [Fact]
    public async Task CreateAgent_Returns201_WithAgent()
    {
        var response = await _client.PostAsJsonAsync("/api/agents", new { Name = "Ada" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var agent = await response.Content.ReadFromJsonAsync<Agent>(JsonOpts);
        Assert.NotNull(agent);
        Assert.Equal("Ada", agent.Name);
        Assert.StartsWith("Ada-agt-", agent.Id);
    }

    [Fact]
    public async Task GetAgent_Returns200_ForExistingAgent()
    {
        var created = await PostAgent("Grace");

        var response = await _client.GetAsync($"/api/agents/{created.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var agent = await response.Content.ReadFromJsonAsync<Agent>(JsonOpts);
        Assert.NotNull(agent);
        Assert.Equal(created.Id, agent.Id);
    }

    [Fact]
    public async Task GetAgent_Returns404_ForUnknownId()
    {
        var response = await _client.GetAsync("/api/agents/does-not-exist");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SaveAgent_Returns200_WithUpdatedFields()
    {
        var created = await PostAgent("Original");
        created.Name = "Renamed";
        created.Personality = "Thoughtful and thorough.";
        created.Color = "#00AA00";
        created.AvatarDataUri = "data:image/png;base64,AAAA";

        var response = await _client.PutAsJsonAsync($"/api/agents/{created.Id}", created);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<Agent>(JsonOpts);
        Assert.NotNull(updated);
        Assert.Equal("Renamed", updated.Name);
        Assert.Equal("Thoughtful and thorough.", updated.Personality);
        Assert.Equal("#00AA00", updated.Color);
        Assert.Equal("data:image/png;base64,AAAA", updated.AvatarDataUri);
    }

    [Fact]
    public async Task SaveAgent_Returns404_ForUnknownId()
    {
        var agent = new Agent { Id = "ghost", Name = "Ghost" };
        var response = await _client.PutAsJsonAsync("/api/agents/ghost", agent);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteAgent_Returns204_AndAgentIsGone()
    {
        var created = await PostAgent("To Delete");

        var deleteResponse = await _client.DeleteAsync($"/api/agents/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var getResponse = await _client.GetAsync($"/api/agents/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteAgent_Returns404_ForUnknownId()
    {
        var response = await _client.DeleteAsync("/api/agents/no-such-agent");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteAgent_ScrubsBoardMembershipAndCellOrders()
    {
        var agent = await PostAgent("Member");
        var board = await PostBoard("Board With Member");
        board.AgentIds.Add(agent.Id);
        board.CellOrders.Add(new SwimlaneCellOrder
        {
            GroupId = board.Groups[0].Id,
            AgentId = agent.Id,
            TaskIds = ["task-1"]
        });
        await _client.PutAsJsonAsync($"/api/boards/{board.Id}", board);

        await _client.DeleteAsync($"/api/agents/{agent.Id}");

        var boardResponse = await _client.GetAsync($"/api/boards/{board.Id}");
        var reloadedBoard = await boardResponse.Content.ReadFromJsonAsync<Board>(JsonOpts);
        Assert.NotNull(reloadedBoard);
        Assert.DoesNotContain(agent.Id, reloadedBoard.AgentIds);
        Assert.DoesNotContain(reloadedBoard.CellOrders, c => c.AgentId == agent.Id);
        var unassigned = Assert.Single(
            reloadedBoard.CellOrders, c => c.GroupId == board.Groups[0].Id && c.AgentId is null);
        Assert.Contains("task-1", unassigned.TaskIds);
    }

    [Fact]
    public async Task DeleteAgent_Returns409_WhenAgentHasAssignedTasks()
    {
        var agent = await PostAgent("Assignee");
        var board = await PostBoard("Board With Task");
        var task = new KanbanTask
        {
            Id = "assigned-task-1",
            BoardId = board.Id,
            GroupId = board.Groups[0].Id,
            Title = "Assigned",
            Created = DateTime.UtcNow,
            AssignedAgentId = agent.Id,
        };
        await _client.PutAsJsonAsync($"/api/boards/{board.Id}/tasks/{task.Id}", task);

        var deleteResponse = await _client.DeleteAsync($"/api/agents/{agent.Id}");
        Assert.Equal(HttpStatusCode.Conflict, deleteResponse.StatusCode);

        var getAgentResponse = await _client.GetAsync($"/api/agents/{agent.Id}");
        Assert.Equal(HttpStatusCode.OK, getAgentResponse.StatusCode);

        var taskResponse = await _client.GetAsync($"/api/boards/{board.Id}/tasks/{task.Id}");
        var reloadedTask = await taskResponse.Content.ReadFromJsonAsync<KanbanTask>(JsonOpts);
        Assert.NotNull(reloadedTask);
        Assert.Equal(agent.Id, reloadedTask.AssignedAgentId);
    }

    [Fact]
    public async Task DeleteAgent_Succeeds_AfterTaskIsUnassigned()
    {
        var agent = await PostAgent("Assignee");
        var board = await PostBoard("Board With Task");
        var task = new KanbanTask
        {
            Id = "assigned-task-2",
            BoardId = board.Id,
            GroupId = board.Groups[0].Id,
            Title = "Assigned",
            Created = DateTime.UtcNow,
            AssignedAgentId = agent.Id,
        };
        await _client.PutAsJsonAsync($"/api/boards/{board.Id}/tasks/{task.Id}", task);

        task.AssignedAgentId = null;
        await _client.PutAsJsonAsync($"/api/boards/{board.Id}/tasks/{task.Id}", task);

        var deleteResponse = await _client.DeleteAsync($"/api/agents/{agent.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
    }

    private async Task<Agent> PostAgent(string name)
    {
        var response = await _client.PostAsJsonAsync("/api/agents", new { Name = name });
        response.EnsureSuccessStatusCode();
        var agent = await response.Content.ReadFromJsonAsync<Agent>(JsonOpts);
        return agent!;
    }

    private async Task<Board> PostBoard(string name)
    {
        var response = await _client.PostAsJsonAsync("/api/boards", new { Name = name });
        response.EnsureSuccessStatusCode();
        var board = await response.Content.ReadFromJsonAsync<Board>(JsonOpts);
        return board!;
    }
}
