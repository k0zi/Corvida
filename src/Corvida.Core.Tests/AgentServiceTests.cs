using Corvida.Models;
using Corvida.Services;

namespace Corvida.Core.Tests;

public class AgentServiceTests : IDisposable
{
    private readonly TempDir _dir = new();
    private readonly FakeSettingsService _settings;
    private readonly BoardService _boards;
    private readonly TaskService _tasks;
    private readonly AgentService _sut;

    public AgentServiceTests()
    {
        _settings = new FakeSettingsService();
        _settings.Settings.DataPath = _dir.Path;
        _boards = new BoardService(_settings);
        _tasks = new TaskService(_settings);
        _sut = new AgentService(_settings, _boards, _tasks);
    }

    public void Dispose() => _dir.Dispose();

    [Fact]
    public async Task GetAgentsAsync_ReturnsEmpty_WhenAgentsDirDoesNotExist()
    {
        var result = await _sut.GetAgentsAsync();
        Assert.Empty(result);
    }

    [Fact]
    public async Task CreateAgentAsync_ReturnsAgentWithCorrectName()
    {
        var agent = await _sut.CreateAgentAsync("Ada");
        Assert.Equal("Ada", agent.Name);
    }

    [Fact]
    public async Task CreateAgentAsync_IdStartsWithNameAndSuffix()
    {
        var agent = await _sut.CreateAgentAsync("Ada");
        Assert.StartsWith("Ada-agt-", agent.Id);
    }

    [Fact]
    public async Task CreateAgentAsync_WritesAgentFileToDisk()
    {
        var agent = await _sut.CreateAgentAsync("Ada");
        var path = Path.Combine(_dir.Path, "agents", agent.Id + ".md");
        Assert.True(File.Exists(path));
    }

    [Fact]
    public async Task GetAgentsAsync_ReturnsSavedAgent_AfterCreate()
    {
        var created = await _sut.CreateAgentAsync("Ada");
        var agents = await _sut.GetAgentsAsync();
        Assert.Single(agents);
        Assert.Equal(created.Id, agents[0].Id);
    }

    [Fact]
    public async Task GetAgentAsync_ReturnsNull_WhenNotFound()
    {
        var result = await _sut.GetAgentAsync("nonexistent-agt-00000000");
        Assert.Null(result);
    }

    [Fact]
    public async Task SaveAgentAsync_PersistsPersonalityAndColor()
    {
        var agent = await _sut.CreateAgentAsync("Ada");
        agent.Personality = "Curious and precise.";
        agent.Color = "#00FF00";
        await _sut.SaveAgentAsync(agent);

        var reloaded = await _sut.GetAgentAsync(agent.Id);
        Assert.NotNull(reloaded);
        Assert.Equal("Curious and precise.", reloaded!.Personality);
        Assert.Equal("#00FF00", reloaded.Color);
    }

    [Fact]
    public async Task DeleteAgentAsync_RemovesAgentFile()
    {
        var agent = await _sut.CreateAgentAsync("Ada");
        await _sut.DeleteAgentAsync(agent.Id);

        var path = Path.Combine(_dir.Path, "agents", agent.Id + ".md");
        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task DeleteAgentAsync_RemovesAgentFromBoardMembership()
    {
        var agent = await _sut.CreateAgentAsync("Ada");
        var board = await _boards.CreateBoardAsync("Project");
        board.AgentIds.Add(agent.Id);
        await _boards.SaveBoardAsync(board);

        await _sut.DeleteAgentAsync(agent.Id);

        var boards = await _boards.GetBoardsAsync();
        Assert.DoesNotContain(agent.Id, boards[0].AgentIds);
    }

    [Fact]
    public async Task DeleteAgentAsync_Throws_WhenAgentHasAssignedTasks()
    {
        var agent = await _sut.CreateAgentAsync("Ada");
        var board = await _boards.CreateBoardAsync("Project");
        var group = board.Groups[0];
        var task = new KanbanTask
        {
            Id = "task-1",
            BoardId = board.Id,
            GroupId = group.Id,
            AssignedAgentId = agent.Id
        };
        group.TaskIds.Add(task.Id);
        await _boards.SaveBoardAsync(board);
        await _tasks.SaveTaskAsync(task);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.DeleteAgentAsync(agent.Id));

        var reloadedTask = await _tasks.GetTaskAsync(board.Id, task.Id);
        Assert.Equal(agent.Id, reloadedTask!.AssignedAgentId);
        var reloadedAgent = await _sut.GetAgentAsync(agent.Id);
        Assert.NotNull(reloadedAgent);
    }

    [Fact]
    public async Task DeleteAgentAsync_Succeeds_AfterTaskIsUnassigned()
    {
        var agent = await _sut.CreateAgentAsync("Ada");
        var board = await _boards.CreateBoardAsync("Project");
        var group = board.Groups[0];
        var task = new KanbanTask
        {
            Id = "task-1",
            BoardId = board.Id,
            GroupId = group.Id,
            AssignedAgentId = agent.Id
        };
        group.TaskIds.Add(task.Id);
        await _boards.SaveBoardAsync(board);
        await _tasks.SaveTaskAsync(task);

        task.AssignedAgentId = null;
        await _tasks.SaveTaskAsync(task);

        await _sut.DeleteAgentAsync(agent.Id);

        var reloadedAgent = await _sut.GetAgentAsync(agent.Id);
        Assert.Null(reloadedAgent);
    }

    [Fact]
    public async Task DeleteAgentAsync_MergesCellOrderIntoUnassignedRow()
    {
        var agent = await _sut.CreateAgentAsync("Ada");
        var board = await _boards.CreateBoardAsync("Project");
        var group = board.Groups[0];
        board.AgentIds.Add(agent.Id);
        board.CellOrders.Add(new SwimlaneCellOrder
        {
            GroupId = group.Id,
            AgentId = agent.Id,
            TaskIds = ["task-1", "task-2"]
        });
        await _boards.SaveBoardAsync(board);

        await _sut.DeleteAgentAsync(agent.Id);

        var reloadedBoard = (await _boards.GetBoardsAsync())[0];
        Assert.DoesNotContain(reloadedBoard.CellOrders, c => c.AgentId == agent.Id);
        var unassigned = Assert.Single(reloadedBoard.CellOrders, c => c.GroupId == group.Id && c.AgentId is null);
        Assert.Contains("task-1", unassigned.TaskIds);
        Assert.Contains("task-2", unassigned.TaskIds);
    }
}
