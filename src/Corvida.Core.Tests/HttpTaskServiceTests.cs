using System.Net;
using System.Text;
using System.Text.Json;
using Corvida.Models;
using Corvida.Services;

namespace Corvida.Core.Tests;

public class HttpTaskServiceTests
{
    private readonly TestHttpMessageHandler _handler = new();
    private readonly FakeSettingsService _settings;
    private readonly HttpTaskService _sut;

    public HttpTaskServiceTests()
    {
        _settings = new FakeSettingsService();
        _settings.Settings.ServerUrl = "http://test-host";
        _sut = new HttpTaskService(new FakeHttpClientFactory(_handler), _settings);
    }

    private static HttpResponseMessage JsonResponse<T>(T obj, HttpStatusCode statusCode = HttpStatusCode.OK)
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
    public async Task GetTaskAsync_SendsGetToApiTask()
    {
        _handler.Enqueue(JsonResponse(MakeTask()));
        await _sut.GetTaskAsync("board-1", "task-1");
        Assert.Single(_handler.Requests);
        Assert.Equal(HttpMethod.Get, _handler.Requests[0].Method);
        Assert.EndsWith("/api/boards/board-1/tasks/task-1", _handler.Requests[0].RequestUri!.PathAndQuery);
    }

    [Fact]
    public async Task GetTaskAsync_ReturnsDeserializedTask_OnSuccess()
    {
        var task = MakeTask();
        _handler.Enqueue(JsonResponse(task));
        var result = await _sut.GetTaskAsync(task.BoardId, task.Id);
        Assert.NotNull(result);
        Assert.Equal(task.Id, result!.Id);
    }

    [Fact]
    public async Task GetTaskAsync_ReturnsNull_On404()
    {
        _handler.Enqueue(new HttpResponseMessage(HttpStatusCode.NotFound));
        var result = await _sut.GetTaskAsync("board-1", "missing");
        Assert.Null(result);
    }

    [Fact]
    public async Task GetTaskAsync_ThrowsInvalidOperationException_On500()
    {
        _handler.Enqueue(new HttpResponseMessage(HttpStatusCode.InternalServerError));
        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.GetTaskAsync("board-1", "task-1"));
    }

    [Fact]
    public async Task GetTaskAsync_ThrowsInvalidOperationException_OnConnectionFailure()
    {
        var sut = new HttpTaskService(new FakeHttpClientFactory(new ThrowingHttpMessageHandler()), _settings);
        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.GetTaskAsync("board-1", "task-1"));
    }

    [Fact]
    public async Task SaveTaskAsync_SendsPutToApiTask()
    {
        var task = MakeTask("board-1", "task-1");
        _handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK));
        await _sut.SaveTaskAsync(task);
        Assert.Equal(HttpMethod.Put, _handler.Requests[0].Method);
        Assert.EndsWith($"/api/boards/{task.BoardId}/tasks/{task.Id}", _handler.Requests[0].RequestUri!.PathAndQuery);
    }

    [Fact]
    public async Task SaveTaskAsync_UriContainsBoardId()
    {
        var task = MakeTask("my-board", "my-task");
        _handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK));
        await _sut.SaveTaskAsync(task);
        Assert.Contains("my-board", _handler.Requests[0].RequestUri!.ToString());
    }

    [Fact]
    public async Task SaveTaskAsync_UriContainsTaskId()
    {
        var task = MakeTask("my-board", "my-task");
        _handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK));
        await _sut.SaveTaskAsync(task);
        Assert.Contains("my-task", _handler.Requests[0].RequestUri!.ToString());
    }

    [Fact]
    public async Task SaveTaskAsync_ThrowsInvalidOperationException_On500()
    {
        _handler.Enqueue(new HttpResponseMessage(HttpStatusCode.InternalServerError));
        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.SaveTaskAsync(MakeTask()));
    }

    [Fact]
    public async Task DeleteTaskAsync_SendsDeleteToApiTask()
    {
        _handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK));
        await _sut.DeleteTaskAsync("board-1", "task-1");
        Assert.Equal(HttpMethod.Delete, _handler.Requests[0].Method);
        Assert.EndsWith("/api/boards/board-1/tasks/task-1", _handler.Requests[0].RequestUri!.PathAndQuery);
    }

    [Fact]
    public async Task DeleteTaskAsync_DoesNotThrow_On404()
    {
        _handler.Enqueue(new HttpResponseMessage(HttpStatusCode.NotFound));
        await _sut.DeleteTaskAsync("board-1", "gone-task");
    }

    [Fact]
    public async Task DeleteTaskAsync_ThrowsInvalidOperationException_On500()
    {
        _handler.Enqueue(new HttpResponseMessage(HttpStatusCode.InternalServerError));
        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.DeleteTaskAsync("board-1", "task-1"));
    }
}
