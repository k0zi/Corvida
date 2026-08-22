using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Corvida.Api.Auth;
using Corvida.Api.Endpoints;
using Corvida.Api.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Corvida.Api.Tests;

[Collection("postgres")]
public class AuthGateTests : IAsyncLifetime
{
    private readonly PostgresContainerFixture _db;
    private ApiWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public AuthGateTests(PostgresContainerFixture db) => _db = db;

    public async Task InitializeAsync()
    {
        _factory = new ApiWebApplicationFactory(_db.ConnectionString);
        _client = _factory.CreateClient();
        await SetAuthRequiredAsync(false);
    }

    public async Task DisposeAsync()
    {
        // The gate is process-global state on a shared container; leaving it closed would break
        // every test class that runs after this one.
        await SetAuthRequiredAsync(false);
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    private async Task SetAuthRequiredAsync(bool value)
    {
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        await ctx.Database.ExecuteSqlAsync(
            $"UPDATE auth.\"SystemSettings\" SET \"AuthRequired\" = {value} WHERE \"Id\" = 1");

        // The flag is cached, so the change is not visible until the cache is dropped.
        _factory.Services.GetRequiredService<IAuthGateState>().Invalidate();
    }

    [Fact]
    public async Task Status_IsAnonymous_AndReportsOpenByDefault()
    {
        var response = await _client.GetAsync("/api/auth/status");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());

        var status = await response.Content.ReadFromJsonAsync<AuthStatusResponse>(JsonOpts);
        Assert.NotNull(status);
        Assert.False(status.AuthRequired);
    }

    [Fact]
    public async Task WhenGateIsOpen_BoardsAreReachableAnonymously()
    {
        var response = await _client.GetAsync("/api/boards");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task WhenGateIsClosed_BoardsRejectAnonymousCallers()
    {
        await SetAuthRequiredAsync(true);

        var response = await _client.GetAsync("/api/boards");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task WhenGateIsClosed_StatusStaysAnonymous()
    {
        await SetAuthRequiredAsync(true);

        var response = await _client.GetAsync("/api/auth/status");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var status = await response.Content.ReadFromJsonAsync<AuthStatusResponse>(JsonOpts);
        Assert.NotNull(status);
        Assert.True(status.AuthRequired);
    }

    [Fact]
    public async Task GateReopens_WhenFlagIsClearedAgain()
    {
        await SetAuthRequiredAsync(true);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/api/boards")).StatusCode);

        await SetAuthRequiredAsync(false);

        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/api/boards")).StatusCode);
    }

    [Fact]
    public async Task WhenGateIsClosed_AgentsAndTasksAreAlsoGated()
    {
        await SetAuthRequiredAsync(true);

        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/api/agents")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await _client.GetAsync("/api/boards/any-board/tasks/any-task")).StatusCode);
    }

    [Fact]
    public async Task HealthEndpoints_StayAnonymous_WhenGateIsClosed()
    {
        // The fallback authorization policy applies to endpoints without explicit metadata, so
        // without AllowAnonymous the container/Aspire health probes would start failing the
        // moment authentication is switched on.
        await SetAuthRequiredAsync(true);

        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/health")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/alive")).StatusCode);
    }

    [Fact]
    public async Task UnmatchedRoute_IsGated_WhenGateIsClosed()
    {
        // Documents a deliberate consequence of SetFallbackPolicy: with the gate closed an
        // unknown path answers 401 rather than 404, so the API leaks no route information.
        await SetAuthRequiredAsync(true);

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await _client.GetAsync("/api/no-such-thing")).StatusCode);
    }
}
