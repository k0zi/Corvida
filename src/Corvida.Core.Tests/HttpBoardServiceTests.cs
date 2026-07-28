using System.Net;
using System.Text;
using System.Text.Json;
using Corvida.Models;
using Corvida.Services;

namespace Corvida.Core.Tests;

public class HttpBoardServiceTests
{
    private readonly TestHttpMessageHandler _handler = new();
    private readonly FakeSettingsService _settings;
    private readonly HttpBoardService _sut;

    public HttpBoardServiceTests()
    {
        _settings = new FakeSettingsService();
        _settings.Settings.ServerUrl = "http://test-host";
        _sut = new HttpBoardService(new FakeHttpClientFactory(_handler), _settings);
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
    public async Task GetBoardsAsync_SendsGetToApiBoards()
    {
        _handler.Enqueue(JsonResponse(new List<Board>()));
        await _sut.GetBoardsAsync();
        Assert.Single(_handler.Requests);
        Assert.Equal(HttpMethod.Get, _handler.Requests[0].Method);
        Assert.EndsWith("/api/boards", _handler.Requests[0].RequestUri!.PathAndQuery);
    }

    [Fact]
    public async Task GetBoardsAsync_UsesConfiguredServerUrl()
    {
        _handler.Enqueue(JsonResponse(new List<Board>()));
        await _sut.GetBoardsAsync();
        Assert.StartsWith("http://test-host", _handler.Requests[0].RequestUri!.ToString());
    }

    [Fact]
    public async Task GetBoardsAsync_UsesDefaultBaseUrl_WhenServerUrlIsNull()
    {
        var settings = new FakeSettingsService();
        settings.Settings.ServerUrl = null;
        var handler = new TestHttpMessageHandler();
        var sut = new HttpBoardService(new FakeHttpClientFactory(handler), settings);

        handler.Enqueue(JsonResponse(new List<Board>()));
        await sut.GetBoardsAsync();

        Assert.StartsWith("http://localhost:5000", handler.Requests[0].RequestUri!.ToString());
    }

    [Fact]
    public async Task GetBoardsAsync_ReturnsDeserializedBoards()
    {
        var boards = new List<Board> { new() { Id = "b1", Name = "Board One" } };
        _handler.Enqueue(JsonResponse(boards));

        var result = await _sut.GetBoardsAsync();

        Assert.Single(result);
        Assert.Equal("Board One", result[0].Name);
    }

    [Fact]
    public async Task GetBoardsAsync_ThrowsInvalidOperationException_OnHttpRequestException()
    {
        var sut = new HttpBoardService(new FakeHttpClientFactory(new ThrowingHttpMessageHandler()), _settings);
        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.GetBoardsAsync());
    }

    [Fact]
    public async Task CreateBoardAsync_SendsPostToApiBoards()
    {
        _handler.Enqueue(JsonResponse(new Board { Id = "new-brd-12345678", Name = "New" }));
        await _sut.CreateBoardAsync("New");
        Assert.Equal(HttpMethod.Post, _handler.Requests[0].Method);
        Assert.EndsWith("/api/boards", _handler.Requests[0].RequestUri!.PathAndQuery);
    }

    [Fact]
    public async Task CreateBoardAsync_ReturnsDeserializedBoard()
    {
        var board = new Board { Id = "ret-brd-12345678", Name = "Returned" };
        _handler.Enqueue(JsonResponse(board));

        var result = await _sut.CreateBoardAsync("Returned");

        Assert.Equal("ret-brd-12345678", result.Id);
        Assert.Equal("Returned", result.Name);
    }

    [Fact]
    public async Task CreateBoardAsync_ThrowsInvalidOperationException_On500()
    {
        _handler.Enqueue(new HttpResponseMessage(HttpStatusCode.InternalServerError));
        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.CreateBoardAsync("Fail"));
    }

    [Fact]
    public async Task SaveBoardAsync_SendsPutToApiBoards()
    {
        _handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK));
        var board = new Board { Id = "save-brd-12345678", Name = "Saved" };
        await _sut.SaveBoardAsync(board);
        Assert.Equal(HttpMethod.Put, _handler.Requests[0].Method);
        Assert.EndsWith($"/api/boards/{board.Id}", _handler.Requests[0].RequestUri!.PathAndQuery);
    }

    [Fact]
    public async Task SaveBoardAsync_ThrowsInvalidOperationException_On500()
    {
        _handler.Enqueue(new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var board = new Board { Id = "fail-brd-12345678", Name = "Fail" };
        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.SaveBoardAsync(board));
    }

    [Fact]
    public async Task DeleteBoardAsync_SendsDeleteToApiBoards()
    {
        _handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK));
        await _sut.DeleteBoardAsync("del-brd-12345678");
        Assert.Equal(HttpMethod.Delete, _handler.Requests[0].Method);
        Assert.EndsWith("/api/boards/del-brd-12345678", _handler.Requests[0].RequestUri!.PathAndQuery);
    }

    [Fact]
    public async Task DeleteBoardAsync_DoesNotThrow_On404()
    {
        _handler.Enqueue(new HttpResponseMessage(HttpStatusCode.NotFound));
        await _sut.DeleteBoardAsync("gone-brd-12345678");
    }

    [Fact]
    public async Task DeleteBoardAsync_ThrowsInvalidOperationException_On500()
    {
        _handler.Enqueue(new HttpResponseMessage(HttpStatusCode.InternalServerError));
        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.DeleteBoardAsync("fail-brd-12345678"));
    }
}
