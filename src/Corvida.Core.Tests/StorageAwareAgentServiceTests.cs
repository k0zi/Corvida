using System.Net;
using System.Text;
using System.Text.Json;
using Corvida.Models;
using Corvida.Services;

namespace Corvida.Core.Tests;

public class StorageAwareAgentServiceTests : IDisposable
{
    private readonly TempDir _dir = new();
    private readonly FakeSettingsService _settings;
    private readonly TestHttpMessageHandler _handler = new();
    private readonly StorageAwareAgentService _sut;

    public StorageAwareAgentServiceTests()
    {
        _settings = new FakeSettingsService();
        _settings.Settings.DataPath = _dir.Path;
        _settings.Settings.ServerUrl = "http://test";

        var boards = new BoardService(_settings);
        var tasks = new TaskService(_settings);
        var localSvc = new AgentService(_settings, boards, tasks);
        var httpSvc = new HttpAgentService(new FakeHttpClientFactory(_handler), _settings);
        _sut = new StorageAwareAgentService(localSvc, httpSvc, _settings);
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

    [Fact]
    public async Task GetAgentsAsync_UsesLocalService_WhenLocalFolder()
    {
        _settings.Settings.StorageMode = StorageMode.LocalFolder;
        var result = await _sut.GetAgentsAsync();
        Assert.Empty(result);
        Assert.Empty(_handler.Requests);
    }

    [Fact]
    public async Task GetAgentsAsync_UsesHttpService_WhenServerHosted()
    {
        _settings.Settings.StorageMode = StorageMode.ServerHosted;
        _handler.Enqueue(JsonResponse(new List<Agent> { new() { Id = "u1", Name = "Remote" } }));

        var result = await _sut.GetAgentsAsync();

        Assert.Single(_handler.Requests);
        Assert.Single(result);
        Assert.Equal("Remote", result[0].Name);
    }

    [Fact]
    public async Task CreateAgentAsync_UsesLocalService_WhenLocalFolder()
    {
        _settings.Settings.StorageMode = StorageMode.LocalFolder;
        var agent = await _sut.CreateAgentAsync("LocalAgent");

        var path = Path.Combine(_dir.Path, "agents", agent.Id + ".md");
        Assert.True(File.Exists(path));
        Assert.Empty(_handler.Requests);
    }

    [Fact]
    public async Task CreateAgentAsync_UsesHttpService_WhenServerHosted()
    {
        _settings.Settings.StorageMode = StorageMode.ServerHosted;
        _handler.Enqueue(JsonResponse(new Agent { Id = "remote-agt-12345678", Name = "Remote" }));

        await _sut.CreateAgentAsync("Remote");

        Assert.Single(_handler.Requests);
        Assert.Equal(HttpMethod.Post, _handler.Requests[0].Method);
    }

    [Fact]
    public async Task Routing_ChangesBasedOnStorageMode()
    {
        _settings.Settings.StorageMode = StorageMode.LocalFolder;
        await _sut.GetAgentsAsync();
        Assert.Empty(_handler.Requests);

        _settings.Settings.StorageMode = StorageMode.ServerHosted;
        _handler.Enqueue(JsonResponse(new List<Agent>()));
        await _sut.GetAgentsAsync();
        Assert.Single(_handler.Requests);
    }
}
