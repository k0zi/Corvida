using System.Net;
using System.Text;
using System.Text.Json;
using Corvida.Models;
using Corvida.Services;

namespace Corvida.Core.Tests;

public class HttpAgentServiceTests
{
    private readonly TestHttpMessageHandler _handler = new();
    private readonly FakeSettingsService _settings;
    private readonly HttpAgentService _sut;

    public HttpAgentServiceTests()
    {
        _settings = new FakeSettingsService();
        _settings.Settings.ServerUrl = "http://test-host";
        _sut = new HttpAgentService(new FakeHttpClientFactory(_handler), _settings);
    }

    private static HttpResponseMessage JsonResponse<T>(T obj, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var json = JsonSerializer.Serialize(obj);
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    [Fact]
    public async Task GetAgentsAsync_SendsGetToApiAgents()
    {
        _handler.Enqueue(JsonResponse(new List<Agent>()));
        await _sut.GetAgentsAsync();
        Assert.Single(_handler.Requests);
        Assert.Equal(HttpMethod.Get, _handler.Requests[0].Method);
        Assert.EndsWith("/api/agents", _handler.Requests[0].RequestUri!.PathAndQuery);
    }

    [Fact]
    public async Task GetAgentsAsync_ReturnsDeserializedAgents()
    {
        var agents = new List<Agent> { new() { Id = "u1", Name = "Ada" } };
        _handler.Enqueue(JsonResponse(agents));

        var result = await _sut.GetAgentsAsync();

        Assert.Single(result);
        Assert.Equal("Ada", result[0].Name);
    }

    [Fact]
    public async Task GetAgentsAsync_ThrowsInvalidOperationException_OnHttpRequestException()
    {
        var sut = new HttpAgentService(new FakeHttpClientFactory(new ThrowingHttpMessageHandler()), _settings);
        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.GetAgentsAsync());
    }

    [Fact]
    public async Task GetAgentAsync_ReturnsNull_On404()
    {
        _handler.Enqueue(new HttpResponseMessage(HttpStatusCode.NotFound));
        var result = await _sut.GetAgentAsync("missing");
        Assert.Null(result);
    }

    [Fact]
    public async Task CreateAgentAsync_SendsPostToApiAgents()
    {
        _handler.Enqueue(JsonResponse(new Agent { Id = "new-agt-12345678", Name = "New" }));
        await _sut.CreateAgentAsync("New");
        Assert.Equal(HttpMethod.Post, _handler.Requests[0].Method);
        Assert.EndsWith("/api/agents", _handler.Requests[0].RequestUri!.PathAndQuery);
    }

    [Fact]
    public async Task CreateAgentAsync_ThrowsInvalidOperationException_On500()
    {
        _handler.Enqueue(new HttpResponseMessage(HttpStatusCode.InternalServerError));
        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.CreateAgentAsync("Fail"));
    }

    [Fact]
    public async Task SaveAgentAsync_SendsPutToApiAgents()
    {
        _handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK));
        var agent = new Agent { Id = "save-agt-12345678", Name = "Saved" };
        await _sut.SaveAgentAsync(agent);
        Assert.Equal(HttpMethod.Put, _handler.Requests[0].Method);
        Assert.EndsWith($"/api/agents/{agent.Id}", _handler.Requests[0].RequestUri!.PathAndQuery);
    }

    [Fact]
    public async Task DeleteAgentAsync_SendsDeleteToApiAgents()
    {
        _handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK));
        await _sut.DeleteAgentAsync("del-agt-12345678");
        Assert.Equal(HttpMethod.Delete, _handler.Requests[0].Method);
        Assert.EndsWith("/api/agents/del-agt-12345678", _handler.Requests[0].RequestUri!.PathAndQuery);
    }

    [Fact]
    public async Task DeleteAgentAsync_DoesNotThrow_On404()
    {
        _handler.Enqueue(new HttpResponseMessage(HttpStatusCode.NotFound));
        await _sut.DeleteAgentAsync("gone-agt-12345678");
    }

    [Fact]
    public async Task DeleteAgentAsync_ThrowsWithServerMessage_On409()
    {
        _handler.Enqueue(JsonResponse(new { error = "Agent has assigned tasks and cannot be deleted." }, HttpStatusCode.Conflict));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.DeleteAgentAsync("busy-agt-12345678"));
        Assert.Equal("Agent has assigned tasks and cannot be deleted.", ex.Message);
    }
}
