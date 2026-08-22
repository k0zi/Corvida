using System.Net;
using System.Net.Http.Json;
using Corvida.Api.Auth;
using Corvida.Api.Endpoints;
using Corvida.Api.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Corvida.Api.Tests;

[Collection("postgres")]
public class ApiKeyTests : IAsyncLifetime
{
    private const string AdminEmail = "keys-admin@example.com";
    private const string AdminPassword = "Passw0rd!keys";

    private readonly PostgresContainerFixture _db;
    private ApiWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    public ApiKeyTests(PostgresContainerFixture db) => _db = db;

    public async Task InitializeAsync()
    {
        _factory = new ApiWebApplicationFactory(_db.ConnectionString);
        _client = _factory.CreateClient();
        await ResetAsync();

        await _client.PostAsJsonAsync("/api/auth/enable",
            new EnableAuthRequest(AdminEmail, AdminPassword, "Keys Admin"));

        var token = await AuthTestHelpers.GetTokenAsync(_client, AdminEmail, AdminPassword);
        _client.UseBearer(token.AccessToken);
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
        await ctx.Database.ExecuteSqlRawAsync("""TRUNCATE auth."PersonalAccessTokens" """);
        await AuthTestHelpers.ClearUsersAsync(_factory);
        _factory.Services.GetRequiredService<IAuthGateState>().Invalidate();
    }

    private async Task<CreatedApiKeyResponse> CreateKeyAsync(string name = "mcp", int? expiresInDays = null)
    {
        var response = await _client.PostAsJsonAsync("/api/apikeys", new CreateApiKeyRequest(name, expiresInDays));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CreatedApiKeyResponse>())!;
    }

    [Fact]
    public async Task CreateKey_ReturnsThePrefixedRawValueOnce()
    {
        var created = await CreateKeyAsync();

        Assert.StartsWith(CorvidaSchemes.PatPrefix, created.Key);

        // The raw value must never be recoverable afterwards — only the hash is stored.
        var listed = await _client.GetFromJsonAsync<List<ApiKeyResponse>>("/api/apikeys");
        var key = Assert.Single(listed!);
        Assert.Equal(created.Id, key.Id);
        Assert.DoesNotContain(created.Key, await (await _client.GetAsync("/api/apikeys")).Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ApiKey_AuthenticatesApiRequests()
    {
        var created = await CreateKeyAsync();

        using var mcpClient = _factory.CreateClient();
        mcpClient.UseBearer(created.Key);

        Assert.Equal(HttpStatusCode.OK, (await mcpClient.GetAsync("/api/boards")).StatusCode);
    }

    [Fact]
    public async Task ApiKey_CarriesTheOwnersRole()
    {
        var created = await CreateKeyAsync();

        using var mcpClient = _factory.CreateClient();
        mcpClient.UseBearer(created.Key);

        var me = await mcpClient.GetFromJsonAsync<MeResponse>("/api/auth/me");
        Assert.Equal(AdminEmail, me!.UserName);
        Assert.Contains(Roles.Admin, me.Roles);
    }

    [Fact]
    public async Task RevokedKey_StopsWorking()
    {
        var created = await CreateKeyAsync();

        using var mcpClient = _factory.CreateClient();
        mcpClient.UseBearer(created.Key);
        Assert.Equal(HttpStatusCode.OK, (await mcpClient.GetAsync("/api/boards")).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await _client.DeleteAsync($"/api/apikeys/{created.Id}")).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await mcpClient.GetAsync("/api/boards")).StatusCode);
    }

    [Fact]
    public async Task ExpiredKey_IsRejected()
    {
        var created = await CreateKeyAsync(expiresInDays: 1);

        using (var scope = _factory.Services.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
            await ctx.PersonalAccessTokens
                .Where(k => k.Id == created.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(k => k.ExpiresAt, DateTime.UtcNow.AddDays(-1)));
        }

        using var mcpClient = _factory.CreateClient();
        mcpClient.UseBearer(created.Key);

        Assert.Equal(HttpStatusCode.Unauthorized, (await mcpClient.GetAsync("/api/boards")).StatusCode);
    }

    [Fact]
    public async Task UnknownKey_IsRejected()
    {
        using var mcpClient = _factory.CreateClient();
        mcpClient.UseBearer(PersonalAccessTokens.Generate());

        Assert.Equal(HttpStatusCode.Unauthorized, (await mcpClient.GetAsync("/api/boards")).StatusCode);
    }

    [Fact]
    public async Task ApiKey_RecordsLastUse()
    {
        var created = await CreateKeyAsync();

        using var mcpClient = _factory.CreateClient();
        mcpClient.UseBearer(created.Key);
        await mcpClient.GetAsync("/api/boards");

        var listed = await _client.GetFromJsonAsync<List<ApiKeyResponse>>("/api/apikeys");
        Assert.NotNull(Assert.Single(listed!).LastUsedAt);
    }

    [Fact]
    public async Task ApiKeyEndpoints_RequireAdmin()
    {
        await AuthTestHelpers.SeedUserAsync(
            _factory, "keys-plain@example.com", AuthTestHelpers.UserPassword, Roles.User);

        using var plainClient = _factory.CreateClient();
        var token = await AuthTestHelpers.GetTokenAsync(
            plainClient, "keys-plain@example.com", AuthTestHelpers.UserPassword);
        plainClient.UseBearer(token.AccessToken);

        Assert.Equal(HttpStatusCode.Forbidden, (await plainClient.GetAsync("/api/apikeys")).StatusCode);
    }
}
