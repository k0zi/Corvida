using System.Net;
using System.Text;
using System.Text.Json;
using Corvida.Models;
using Corvida.Services;

namespace Corvida.Core.Tests;

public class StorageAwareTaskServiceTests : IDisposable
{
    private readonly TempDir _dir = new();
    private readonly FakeSettingsService _settings;
    private readonly TestHttpMessageHandler _handler = new();
    private readonly StorageAwareTaskService _sut;

    public StorageAwareTaskServiceTests()
    {
        _settings = new FakeSettingsService();
        _settings.Settings.DataPath = _dir.Path;
        _settings.Settings.ServerUrl = "http://test";

        var localSvc = new TaskService(_settings);
        var httpSvc = new HttpTaskService(new FakeHttpClientFactory(_handler), _settings);
        _sut = new StorageAwareTaskService(localSvc, httpSvc, _settings);
    }

    public void Dispose() => _dir.Dispose();

    private HttpResponseMessage JsonResponse<T>(T obj, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var json = JsonSerializer.Serialize(obj);
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private static KanbanTask MakeTask(string boardId = "board-1", string taskId = "task-1") => new()
    {
        Id = taskId,
        BoardId = boardId,
        GroupId = "grp-1",
        Title = "Test Task",
        Priority = "Medium",
        Created = new DateTime(2025, 3, 1, 12, 0, 0, DateTimeKind.Utc)
    };

    [Fact]
    public async Task GetTaskAsync_UsesLocalService_WhenLocalFolder()
    {
        _settings.Settings.StorageMode = StorageMode.LocalFolder;
        var result = await _sut.GetTaskAsync("board-1", "missing");
        Assert.Null(result);
        Assert.Empty(_handler.Requests);
    }

    [Fact]
    public async Task GetTaskAsync_UsesHttpService_WhenServerHosted()
    {
        _settings.Settings.StorageMode = StorageMode.ServerHosted;
        _handler.Enqueue(new HttpResponseMessage(HttpStatusCode.NotFound));

        var result = await _sut.GetTaskAsync("board-1", "task-1");

        Assert.Null(result);
        Assert.Single(_handler.Requests);
        Assert.Equal(HttpMethod.Get, _handler.Requests[0].Method);
    }

    [Fact]
    public async Task SaveTaskAsync_UsesLocalService_WhenLocalFolder()
    {
        _settings.Settings.StorageMode = StorageMode.LocalFolder;
        var task = MakeTask();
        await _sut.SaveTaskAsync(task);

        var path = Path.Combine(_dir.Path, "boards", task.BoardId, "tasks", task.Id + ".md");
        Assert.True(File.Exists(path));
        Assert.Empty(_handler.Requests);
    }

    [Fact]
    public async Task SaveTaskAsync_UsesHttpService_WhenServerHosted()
    {
        _settings.Settings.StorageMode = StorageMode.ServerHosted;
        _handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK));

        await _sut.SaveTaskAsync(MakeTask());

        Assert.Single(_handler.Requests);
        Assert.Equal(HttpMethod.Put, _handler.Requests[0].Method);
    }

    [Fact]
    public async Task DeleteTaskAsync_UsesLocalService_WhenLocalFolder()
    {
        _settings.Settings.StorageMode = StorageMode.LocalFolder;
        var task = MakeTask();
        await _sut.SaveTaskAsync(task);
        var path = Path.Combine(_dir.Path, "boards", task.BoardId, "tasks", task.Id + ".md");
        Assert.True(File.Exists(path));

        await _sut.DeleteTaskAsync(task.BoardId, task.Id);

        Assert.False(File.Exists(path));
        Assert.Empty(_handler.Requests);
    }

    [Fact]
    public async Task DeleteTaskAsync_UsesHttpService_WhenServerHosted()
    {
        _settings.Settings.StorageMode = StorageMode.ServerHosted;
        _handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK));

        await _sut.DeleteTaskAsync("board-1", "task-1");

        Assert.Single(_handler.Requests);
        Assert.Equal(HttpMethod.Delete, _handler.Requests[0].Method);
    }

    [Fact]
    public async Task Routing_ChangesBasedOnStorageMode()
    {
        _settings.Settings.StorageMode = StorageMode.LocalFolder;
        await _sut.GetTaskAsync("board-1", "task-1");
        Assert.Empty(_handler.Requests);

        _settings.Settings.StorageMode = StorageMode.ServerHosted;
        _handler.Enqueue(new HttpResponseMessage(HttpStatusCode.NotFound));
        await _sut.GetTaskAsync("board-1", "task-1");
        Assert.Single(_handler.Requests);
    }
}
