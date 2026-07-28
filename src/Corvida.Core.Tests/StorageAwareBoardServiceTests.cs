using System.Net;
using System.Text;
using System.Text.Json;
using Corvida.Models;
using Corvida.Services;

namespace Corvida.Core.Tests;

public class StorageAwareBoardServiceTests : IDisposable
{
    private readonly TempDir _dir = new();
    private readonly FakeSettingsService _settings;
    private readonly TestHttpMessageHandler _handler = new();
    private readonly StorageAwareBoardService _sut;

    public StorageAwareBoardServiceTests()
    {
        _settings = new FakeSettingsService();
        _settings.Settings.DataPath = _dir.Path;
        _settings.Settings.ServerUrl = "http://test";

        var localSvc = new BoardService(_settings);
        var httpSvc = new HttpBoardService(new FakeHttpClientFactory(_handler), _settings);
        _sut = new StorageAwareBoardService(localSvc, httpSvc, _settings);
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
    public async Task GetBoardsAsync_UsesLocalService_WhenLocalFolder()
    {
        _settings.Settings.StorageMode = StorageMode.LocalFolder;
        var result = await _sut.GetBoardsAsync();
        Assert.Empty(result);
        Assert.Empty(_handler.Requests);
    }

    [Fact]
    public async Task GetBoardsAsync_UsesHttpService_WhenServerHosted()
    {
        _settings.Settings.StorageMode = StorageMode.ServerHosted;
        _handler.Enqueue(JsonResponse(new List<Board> { new() { Id = "b1", Name = "Remote" } }));

        var result = await _sut.GetBoardsAsync();

        Assert.Single(_handler.Requests);
        Assert.Single(result);
        Assert.Equal("Remote", result[0].Name);
    }

    [Fact]
    public async Task CreateBoardAsync_UsesLocalService_WhenLocalFolder()
    {
        _settings.Settings.StorageMode = StorageMode.LocalFolder;
        var board = await _sut.CreateBoardAsync("LocalBoard");

        var boardDir = Path.Combine(_dir.Path, "boards", board.Id);
        Assert.True(Directory.Exists(boardDir));
        Assert.Empty(_handler.Requests);
    }

    [Fact]
    public async Task CreateBoardAsync_UsesHttpService_WhenServerHosted()
    {
        _settings.Settings.StorageMode = StorageMode.ServerHosted;
        _handler.Enqueue(JsonResponse(new Board { Id = "remote-brd-12345678", Name = "Remote" }));

        await _sut.CreateBoardAsync("Remote");

        Assert.Single(_handler.Requests);
        Assert.Equal(HttpMethod.Post, _handler.Requests[0].Method);
    }

    [Fact]
    public async Task DeleteBoardAsync_UsesLocalService_WhenLocalFolder()
    {
        _settings.Settings.StorageMode = StorageMode.LocalFolder;
        var board = await _sut.CreateBoardAsync("ToDelete");
        var boardDir = Path.Combine(_dir.Path, "boards", board.Id);
        Assert.True(Directory.Exists(boardDir));

        await _sut.DeleteBoardAsync(board.Id);

        Assert.False(Directory.Exists(boardDir));
        Assert.Empty(_handler.Requests);
    }

    [Fact]
    public async Task DeleteBoardAsync_UsesHttpService_WhenServerHosted()
    {
        _settings.Settings.StorageMode = StorageMode.ServerHosted;
        _handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK));

        await _sut.DeleteBoardAsync("remote-brd-12345678");

        Assert.Single(_handler.Requests);
        Assert.Equal(HttpMethod.Delete, _handler.Requests[0].Method);
    }

    [Fact]
    public async Task Routing_ChangesBasedOnStorageMode()
    {
        _settings.Settings.StorageMode = StorageMode.LocalFolder;
        await _sut.GetBoardsAsync();
        Assert.Empty(_handler.Requests);

        _settings.Settings.StorageMode = StorageMode.ServerHosted;
        _handler.Enqueue(JsonResponse(new List<Board>()));
        await _sut.GetBoardsAsync();
        Assert.Single(_handler.Requests);
    }
}
