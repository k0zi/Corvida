using System.Net;
using System.Net.Http.Json;
using Corvida.Api.Auth;
using Corvida.Api.Endpoints;
using Corvida.Api.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Corvida.Api.Tests;

[Collection("postgres")]
public class EnableDisableTests : IAsyncLifetime
{
    private const string AdminEmail = "enable-admin@example.com";
    private const string AdminPassword = "Passw0rd!enable";

    private readonly PostgresContainerFixture _db;
    private ApiWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    public EnableDisableTests(PostgresContainerFixture db) => _db = db;

    public async Task InitializeAsync()
    {
        _factory = new ApiWebApplicationFactory(_db.ConnectionString);
        _client = _factory.CreateClient();
        await ResetAsync();
    }

    public async Task DisposeAsync()
    {
        // The gate and the user store are process-global on a shared container; anything left
        // behind here would break whichever test class runs next.
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

    private Task<HttpResponseMessage> EnableAsync(string email = AdminEmail, string password = AdminPassword) =>
        _client.PostAsJsonAsync("/api/auth/enable",
            new EnableAuthRequest(email, password, "Enable Admin"));

    private async Task<bool> AuthRequiredAsync()
    {
        var status = await _client.GetFromJsonAsync<AuthStatusResponse>("/api/auth/status");
        return status!.AuthRequired;
    }

    [Fact]
    public async Task Enable_CreatesFirstAdmin_AndClosesTheGate()
    {
        Assert.False(await AuthRequiredAsync());
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/api/boards")).StatusCode);

        var response = await EnableAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<EnableAuthResponse>();
        Assert.Equal(AdminEmail, body!.AdminUserName);

        Assert.True(await AuthRequiredAsync());
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/api/boards")).StatusCode);
    }

    [Fact]
    public async Task Enable_FirstAdminCanSignInAndReachTheApi()
    {
        await EnableAsync();

        var token = await AuthTestHelpers.GetTokenAsync(_client, AdminEmail, AdminPassword);
        _client.UseBearer(token.AccessToken);

        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/api/boards")).StatusCode);

        var me = await _client.GetFromJsonAsync<MeResponse>("/api/auth/me");
        Assert.Equal(AdminEmail, me!.UserName);
        Assert.Contains(Roles.Admin, me.Roles);
    }

    [Fact]
    public async Task Enable_IsRejected_WhenAlreadyEnabled()
    {
        Assert.Equal(HttpStatusCode.OK, (await EnableAsync()).StatusCode);

        var second = await EnableAsync("someone-else@example.com", "Passw0rd!other");

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Enable_RejectsAWeakPassword_AndLeavesTheGateOpen()
    {
        var response = await EnableAsync(password: "short");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(await AuthRequiredAsync());
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/api/boards")).StatusCode);
    }

    [Fact]
    public async Task Disable_RequiresTheCallersOwnPassword()
    {
        await EnableAsync();
        var token = await AuthTestHelpers.GetTokenAsync(_client, AdminEmail, AdminPassword);
        _client.UseBearer(token.AccessToken);

        var response = await _client.PostAsJsonAsync("/api/auth/disable",
            new DisableAuthRequest("not-the-password", DisableAuthRequest.RequiredConfirmation));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.True(await AuthRequiredAsync());
    }

    [Fact]
    public async Task Disable_RequiresTheConfirmationPhrase()
    {
        await EnableAsync();
        var token = await AuthTestHelpers.GetTokenAsync(_client, AdminEmail, AdminPassword);
        _client.UseBearer(token.AccessToken);

        var response = await _client.PostAsJsonAsync("/api/auth/disable",
            new DisableAuthRequest(AdminPassword, "yes please"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(await AuthRequiredAsync());
    }

    [Fact]
    public async Task Disable_WipesTheUserStore_AndReopensTheGate()
    {
        await EnableAsync();
        var token = await AuthTestHelpers.GetTokenAsync(_client, AdminEmail, AdminPassword);
        _client.UseBearer(token.AccessToken);

        var response = await _client.PostAsJsonAsync("/api/auth/disable",
            new DisableAuthRequest(AdminPassword, DisableAuthRequest.RequiredConfirmation));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.False(await AuthRequiredAsync());

        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        Assert.Empty(await ctx.Users.AsNoTracking().ToListAsync());
        Assert.Empty(await ctx.PersonalAccessTokens.AsNoTracking().ToListAsync());

        // The singleton row must survive — it is what the gate reads.
        Assert.Single(await ctx.SystemSettings.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task Disable_InvalidatesTokensIssuedBeforeTheWipe()
    {
        await EnableAsync();
        var token = await AuthTestHelpers.GetTokenAsync(_client, AdminEmail, AdminPassword);
        _client.UseBearer(token.AccessToken);

        await _client.PostAsJsonAsync("/api/auth/disable",
            new DisableAuthRequest(AdminPassword, DisableAuthRequest.RequiredConfirmation));

        // Re-close the gate without re-creating that admin, so the old token is the only
        // credential in play. Access tokens are self-contained JWTs: without key rotation this
        // one would still authenticate, outliving the wipe that was meant to remove all accounts.
        await EnableAsync("second-admin@example.com", "Passw0rd!second");

        var response = await _client.GetAsync("/api/boards");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Disable_RequiresAdmin_NotJustAnyAuthenticatedUser()
    {
        await EnableAsync();
        await AuthTestHelpers.SeedUserAsync(
            _factory, "plain@example.com", AuthTestHelpers.UserPassword, Roles.User);

        var token = await AuthTestHelpers.GetTokenAsync(_client, "plain@example.com", AuthTestHelpers.UserPassword);
        _client.UseBearer(token.AccessToken);

        var response = await _client.PostAsJsonAsync("/api/auth/disable",
            new DisableAuthRequest(AuthTestHelpers.UserPassword, DisableAuthRequest.RequiredConfirmation));

        // 403, not 401: this is the end-to-end proof that role claims survive into the access
        // token and that RoleClaimType lines up with what the policy checks.
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.True(await AuthRequiredAsync());
    }
}
