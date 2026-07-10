using Corvida.Models;
using Corvida.Services;

namespace Corvida.Core.Tests;

public class TaskServiceTests : IDisposable
{
    private readonly TempDir _dir = new();
    private readonly FakeSettingsService _settings;
    private readonly TaskService _sut;

    public TaskServiceTests()
    {
        _settings = new FakeSettingsService();
        _settings.Settings.DataPath = _dir.Path;
        _sut = new TaskService(_settings);
    }

    public void Dispose() => _dir.Dispose();

    private static KanbanTask MakeTask(string boardId = "board-1", string taskId = "task-1") => new()
    {
        Id = taskId,
        BoardId = boardId,
        GroupId = "grp-1",
        Title = "Test Task",
        Description = "A test description",
        Priority = "High",
        Created = new DateTime(2025, 3, 1, 12, 0, 0, DateTimeKind.Utc)
    };

    [Fact]
    public async Task GetTaskAsync_ReturnsNull_WhenFileDoesNotExist()
    {
        var result = await _sut.GetTaskAsync("board-1", "missing-task");
        Assert.Null(result);
    }

    [Fact]
    public async Task SaveTaskAsync_CreatesMarkdownFile()
    {
        var task = MakeTask();
        await _sut.SaveTaskAsync(task);

        var path = Path.Combine(_dir.Path, "boards", task.BoardId, "tasks", task.Id + ".md");
        Assert.True(File.Exists(path));
    }

    [Fact]
    public async Task GetTaskAsync_ReturnsSavedTask_WithCorrectId()
    {
        var task = MakeTask();
        await _sut.SaveTaskAsync(task);
        var result = await _sut.GetTaskAsync(task.BoardId, task.Id);
        Assert.NotNull(result);
        Assert.Equal(task.Id, result!.Id);
    }

    [Fact]
    public async Task GetTaskAsync_ReturnsSavedTask_WithCorrectTitle()
    {
        var task = MakeTask();
        await _sut.SaveTaskAsync(task);
        var result = await _sut.GetTaskAsync(task.BoardId, task.Id);
        Assert.Equal(task.Title, result!.Title);
    }

    [Fact]
    public async Task GetTaskAsync_ReturnsSavedTask_WithCorrectDescription()
    {
        var task = MakeTask();
        await _sut.SaveTaskAsync(task);
        var result = await _sut.GetTaskAsync(task.BoardId, task.Id);
        Assert.Equal(task.Description, result!.Description);
    }

    [Fact]
    public async Task GetTaskAsync_ReturnsSavedTask_WithCorrectPriority()
    {
        var task = MakeTask();
        await _sut.SaveTaskAsync(task);
        var result = await _sut.GetTaskAsync(task.BoardId, task.Id);
        Assert.Equal(task.Priority, result!.Priority);
    }

    [Fact]
    public async Task GetTaskAsync_ReturnsSavedTask_WithCorrectGroupId()
    {
        var task = MakeTask();
        await _sut.SaveTaskAsync(task);
        var result = await _sut.GetTaskAsync(task.BoardId, task.Id);
        Assert.Equal(task.GroupId, result!.GroupId);
    }

    [Fact]
    public async Task GetTaskAsync_ReturnsSavedTask_WithCorrectBoardId()
    {
        var task = MakeTask();
        await _sut.SaveTaskAsync(task);
        var result = await _sut.GetTaskAsync(task.BoardId, task.Id);
        Assert.Equal(task.BoardId, result!.BoardId);
    }

    [Fact]
    public async Task GetTaskAsync_ReturnsSavedTask_PreservesPlannedStart()
    {
        var task = MakeTask();
        task.PlannedStart = new DateTime(2025, 4, 1, 9, 0, 0, DateTimeKind.Utc);
        await _sut.SaveTaskAsync(task);
        var result = await _sut.GetTaskAsync(task.BoardId, task.Id);
        Assert.Equal(task.PlannedStart, result!.PlannedStart);
    }

    [Fact]
    public async Task GetTaskAsync_ReturnsSavedTask_PreservesPlannedEnd()
    {
        var task = MakeTask();
        task.PlannedEnd = new DateTime(2025, 4, 30, 17, 0, 0, DateTimeKind.Utc);
        await _sut.SaveTaskAsync(task);
        var result = await _sut.GetTaskAsync(task.BoardId, task.Id);
        Assert.Equal(task.PlannedEnd, result!.PlannedEnd);
    }

    [Fact]
    public async Task DeleteTaskAsync_RemovesFile()
    {
        var task = MakeTask();
        await _sut.SaveTaskAsync(task);
        var path = Path.Combine(_dir.Path, "boards", task.BoardId, "tasks", task.Id + ".md");
        Assert.True(File.Exists(path));

        await _sut.DeleteTaskAsync(task.BoardId, task.Id);

        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task DeleteTaskAsync_IsNoOp_WhenFileDoesNotExist()
    {
        await _sut.DeleteTaskAsync("board-1", "nonexistent-task");
    }

    [Fact]
    public async Task GetTaskAsync_AfterDelete_ReturnsNull()
    {
        var task = MakeTask();
        await _sut.SaveTaskAsync(task);
        await _sut.DeleteTaskAsync(task.BoardId, task.Id);
        var result = await _sut.GetTaskAsync(task.BoardId, task.Id);
        Assert.Null(result);
    }
}
