using System.Net;
using System.Net.Http.Json;
using Corvida.Api.Auth;
using Corvida.Api.Endpoints;
using Corvida.Api.Tests.Fixtures;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenIddict.Server.AspNetCore;
using OpenIddict.Validation.AspNetCore;

namespace Corvida.Api.Tests;

[Collection("postgres")]
public class AuthSchemeAndRateLimitTests : IAsyncLifetime
{
    private readonly PostgresContainerFixture _db;
    private ApiWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    public AuthSchemeAndRateLimitTests(PostgresContainerFixture db) => _db = db;

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

    [Fact]
    public void DispatchSchemeIsTheDefault_AndOpenIddictRegistersItsOwn()
    {
        var options = _factory.Services.GetRequiredService<IOptions<AuthenticationOptions>>().Value;

        Assert.Equal(CorvidaSchemes.Dispatch, options.DefaultScheme);

        // DefaultSignInScheme must stay null: the token endpoint names the OpenIddict scheme
        // explicitly, and setting it would trip OpenIddict's own configuration validation.
        Assert.Null(options.DefaultSignInScheme);

        var registered = options.Schemes.Select(s => s.Name).ToList();
        Assert.Contains(CorvidaSchemes.Dispatch, registered);
        Assert.Contains(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme, registered);
        Assert.Contains(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme, registered);
    }

    [Fact]
    public async Task GarbageBearerToken_IsRejected_WhenGateIsClosed()
    {
        await _client.PostAsJsonAsync("/api/auth/enable",
            new EnableAuthRequest("scheme-admin@example.com", "Passw0rd!scheme", "Admin"));

        _client.UseBearer("not-a-real-token");

        var response = await _client.GetAsync("/api/boards");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TokenEndpoint_IsRateLimited()
    {
        // The limiter partitions by client IP and the policy allows 10/minute.
        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 12; i++)
        {
            var response = await AuthTestHelpers.PostTokenAsync(_client,
                ("grant_type", "password"), ("username", "nobody@example.com"),
                ("password", "whatever12"), ("scope", "offline_access"));
            statuses.Add(response.StatusCode);
        }

        Assert.Contains(HttpStatusCode.TooManyRequests, statuses);
    }

    [Fact]
    public async Task EnableEndpoint_IsRateLimited()
    {
        // 3 per 5 minutes: the fourth attempt must be throttled even though each one fails.
        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 4; i++)
        {
            var response = await _client.PostAsJsonAsync("/api/auth/enable",
                new EnableAuthRequest($"rl-{i}@example.com", "short", "RL"));   // rejected: weak password
            statuses.Add(response.StatusCode);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, statuses[^1]);
    }

    [Fact]
    public async Task RateLimitRejection_UsesAnOAuthShapedBody()
    {
        HttpResponseMessage? throttled = null;
        for (var i = 0; i < 12 && throttled is null; i++)
        {
            var response = await AuthTestHelpers.PostTokenAsync(_client,
                ("grant_type", "password"), ("username", "nobody@example.com"),
                ("password", "whatever12"), ("scope", "offline_access"));
            if (response.StatusCode == HttpStatusCode.TooManyRequests) throttled = response;
        }

        Assert.NotNull(throttled);
        // snake_case so a client's OAuth error parser can read it.
        Assert.Contains("slow_down", await throttled.Content.ReadAsStringAsync());
    }
}
