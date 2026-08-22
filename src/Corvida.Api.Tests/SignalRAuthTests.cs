using System.Net;
using System.Net.Http.Json;
using Corvida.Api.Auth;
using Corvida.Api.Endpoints;
using Corvida.Api.Tests.Fixtures;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Corvida.Api.Tests;

[Collection("postgres")]
public class SignalRAuthTests : IAsyncLifetime
{
    private const string AdminEmail = "hub-admin@example.com";
    private const string AdminPassword = "Passw0rd!hub";

    private readonly PostgresContainerFixture _db;
    private ApiWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    public SignalRAuthTests(PostgresContainerFixture db) => _db = db;

    public async Task InitializeAsync()
    {
        _factory = new ApiWebApplicationFactory(_db.ConnectionString);
        _client = _factory.CreateClient();
        await ResetAsync();
    }

    public async Task DisposeAsync()
    {
        await ResetAsync();
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    private async Task ResetAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        await ctx.Database.ExecuteSqlRawAsync(
            """UPDATE auth."SystemSettings" SET "AuthRequired" = false WHERE "Id" = 1""");
        await AuthTestHelpers.ClearUsersAsync(_factory);
        _factory.Services.GetRequiredService<IAuthGateState>().Invalidate();
    }

    private Task EnableAuthAsync() =>
        _client.PostAsJsonAsync("/api/auth/enable",
            new EnableAuthRequest(AdminEmail, AdminPassword, "Hub Admin"));

    private HubConnection BuildConnection(string? accessToken)
    {
        return new HubConnectionBuilder()
            .WithUrl(new Uri(_factory.Server.BaseAddress, "hubs/kanban"), options =>
            {
                // TestServer has no real socket; route the client through its in-memory handler.
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
                if (accessToken is not null)
                    options.AccessTokenProvider = () => Task.FromResult<string?>(accessToken);
            })
            .Build();
    }

    [Fact]
    public async Task Hub_AcceptsAnonymousConnections_WhenGateIsOpen()
    {
        await using var connection = BuildConnection(accessToken: null);

        await connection.StartAsync();

        Assert.Equal(HubConnectionState.Connected, connection.State);
    }

    [Fact]
    public async Task Hub_RejectsAnonymousConnections_WhenGateIsClosed()
    {
        await EnableAuthAsync();

        await using var connection = BuildConnection(accessToken: null);

        await Assert.ThrowsAnyAsync<Exception>(() => connection.StartAsync());
        Assert.NotEqual(HubConnectionState.Connected, connection.State);
    }

    [Fact]
    public async Task Hub_AcceptsAuthenticatedConnections_WhenGateIsClosed()
    {
        await EnableAuthAsync();
        var token = await AuthTestHelpers.GetTokenAsync(_client, AdminEmail, AdminPassword);

        await using var connection = BuildConnection(token.AccessToken);

        await connection.StartAsync();

        Assert.Equal(HubConnectionState.Connected, connection.State);
    }

    [Fact]
    public async Task Negotiate_AcceptsTheTokenFromTheQueryString()
    {
        // Browser WebSocket clients cannot send an Authorization header, so SignalR falls back to
        // ?access_token=. OpenIddict's validation stack reads that by default — this asserts the
        // behaviour is actually on, since disabling it would break the web client silently.
        await EnableAuthAsync();
        var token = await AuthTestHelpers.GetTokenAsync(_client, AdminEmail, AdminPassword);

        var withToken = await _client.PostAsync(
            $"/hubs/kanban/negotiate?negotiateVersion=1&access_token={Uri.EscapeDataString(token.AccessToken)}",
            content: null);
        var withoutToken = await _client.PostAsync("/hubs/kanban/negotiate?negotiateVersion=1", content: null);

        Assert.Equal(HttpStatusCode.OK, withToken.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, withoutToken.StatusCode);
    }
}
