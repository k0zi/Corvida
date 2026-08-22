using System.Net;
using Corvida.Exceptions;
using Corvida.Models;
using Corvida.Services;

namespace Corvida.Core.Tests;

/// <summary>
/// A 401/403 arrives as an <see cref="HttpRequestException"/>, exactly like a connection failure,
/// so these assert that auth failures are reported as auth failures rather than as
/// "Check your Server URL setting".
/// </summary>
public class HttpServiceAuthErrorTests
{
    private readonly TestHttpMessageHandler _handler = new();
    private readonly FakeSettingsService _settings;

    public HttpServiceAuthErrorTests()
    {
        _settings = new FakeSettingsService();
        _settings.Settings.ServerUrl = "http://test-host";
    }

    private HttpBoardService Boards => new(new FakeHttpClientFactory(_handler), _settings);
    private HttpTaskService Tasks => new(new FakeHttpClientFactory(_handler), _settings);
    private HttpAgentService Agents => new(new FakeHttpClientFactory(_handler), _settings);

    private void Respond(HttpStatusCode code) => _handler.Enqueue(new HttpResponseMessage(code));

    // --- 401 on the GetFromJsonAsync code path ---

    [Fact]
    public async Task GetBoardsAsync_Returns401_ThrowsAuthenticationException()
    {
        Respond(HttpStatusCode.Unauthorized);

        var ex = await Assert.ThrowsAsync<CorvidaAuthenticationException>(() => Boards.GetBoardsAsync());

        Assert.Contains("sign in", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Server URL", ex.Message);
    }

    [Fact]
    public async Task GetAgentsAsync_Returns401_ThrowsAuthenticationException()
    {
        Respond(HttpStatusCode.Unauthorized);

        await Assert.ThrowsAsync<CorvidaAuthenticationException>(() => Agents.GetAgentsAsync());
    }

    [Fact]
    public async Task GetTaskAsync_Returns401_ThrowsAuthenticationException()
    {
        Respond(HttpStatusCode.Unauthorized);

        await Assert.ThrowsAsync<CorvidaAuthenticationException>(() => Tasks.GetTaskAsync("b1", "t1"));
    }

    // --- 401 on the EnsureSuccessStatusCode code path ---

    [Fact]
    public async Task SaveBoardAsync_Returns401_ThrowsAuthenticationException()
    {
        Respond(HttpStatusCode.Unauthorized);

        await Assert.ThrowsAsync<CorvidaAuthenticationException>(
            () => Boards.SaveBoardAsync(new Board { Id = "b1", Name = "B" }));
    }

    [Fact]
    public async Task CreateAgentAsync_Returns401_ThrowsAuthenticationException()
    {
        Respond(HttpStatusCode.Unauthorized);

        await Assert.ThrowsAsync<CorvidaAuthenticationException>(() => Agents.CreateAgentAsync("Ada"));
    }

    // --- 403 ---

    [Fact]
    public async Task GetBoardsAsync_Returns403_ThrowsAuthorizationException()
    {
        Respond(HttpStatusCode.Forbidden);

        var ex = await Assert.ThrowsAsync<CorvidaAuthorizationException>(() => Boards.GetBoardsAsync());

        Assert.Contains("permission", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DeleteBoardAsync_Returns403_ThrowsAuthorizationException()
    {
        Respond(HttpStatusCode.Forbidden);

        await Assert.ThrowsAsync<CorvidaAuthorizationException>(() => Boards.DeleteBoardAsync("b1"));
    }

    // --- both stay catchable as InvalidOperationException, so existing UI handlers keep working ---

    [Fact]
    public async Task AuthExceptions_AreStillInvalidOperationExceptions()
    {
        Respond(HttpStatusCode.Unauthorized);

        var ex = await Assert.ThrowsAnyAsync<InvalidOperationException>(() => Boards.GetBoardsAsync());

        Assert.IsType<CorvidaAuthenticationException>(ex);
    }

    // --- regression guard: a genuine connection failure keeps its original message ---

    [Fact]
    public async Task ConnectionFailure_StillReportsServerUrlMessage()
    {
        var sut = new HttpBoardService(
            new FakeHttpClientFactory(new ThrowingHttpMessageHandler()), _settings);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.GetBoardsAsync());

        Assert.Contains("Cannot reach Corvida server at http://test-host", ex.Message);
        Assert.Contains("Check your Server URL setting", ex.Message);
    }

    [Fact]
    public async Task ArchivedBoardConflict_StillReportsArchivedMessage()
    {
        Respond(HttpStatusCode.Conflict);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Boards.SaveBoardAsync(new Board { Id = "b1", Name = "B" }));

        Assert.Contains("archived", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
