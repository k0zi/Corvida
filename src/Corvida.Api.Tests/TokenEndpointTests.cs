using System.Net;
using System.Net.Http.Json;
using Corvida.Api.Auth;
using Corvida.Api.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Corvida.Api.Tests;

[Collection("postgres")]
public class TokenEndpointTests : IAsyncLifetime
{
    private const string AdminUser = "token-admin@example.com";
    private const string PlainUser = "token-user@example.com";

    private readonly PostgresContainerFixture _db;
    private ApiWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    public TokenEndpointTests(PostgresContainerFixture db) => _db = db;

    public async Task InitializeAsync()
    {
        _factory = new ApiWebApplicationFactory(_db.ConnectionString);
        _client = _factory.CreateClient();

        await AuthTestHelpers.ClearUsersAsync(_factory);
        await AuthTestHelpers.SeedUserAsync(_factory, AdminUser, AuthTestHelpers.AdminPassword, Roles.Admin);
        await AuthTestHelpers.SeedUserAsync(_factory, PlainUser, AuthTestHelpers.UserPassword, Roles.User);
    }

    public async Task DisposeAsync()
    {
        await AuthTestHelpers.ClearUsersAsync(_factory);
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task PasswordGrant_ReturnsAccessAndRefreshToken()
    {
        var token = await AuthTestHelpers.GetTokenAsync(_client, AdminUser, AuthTestHelpers.AdminPassword);

        Assert.False(string.IsNullOrWhiteSpace(token.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(token.RefreshToken));
        Assert.Equal("Bearer", token.TokenType);
        Assert.True(token.ExpiresIn > 0);
    }

    [Fact]
    public async Task PasswordGrant_WrongPassword_IsRejected()
    {
        var response = await AuthTestHelpers.PostTokenAsync(_client,
            ("grant_type", "password"), ("username", AdminUser), ("password", "wrong-password"),
            ("scope", "offline_access"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("invalid_grant", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task PasswordGrant_UnknownUser_IsRejected()
    {
        var response = await AuthTestHelpers.PostTokenAsync(_client,
            ("grant_type", "password"), ("username", "nobody@example.com"), ("password", "whatever12"),
            ("scope", "offline_access"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("invalid_grant", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task PasswordGrant_LocksOutAfterRepeatedFailures()
    {
        // IdentityOptions.Lockout.MaxFailedAccessAttempts is 5; this proves lockoutOnFailure:true
        // is actually being passed to CheckPasswordSignInAsync.
        for (var i = 0; i < 5; i++)
        {
            await AuthTestHelpers.PostTokenAsync(_client,
                ("grant_type", "password"), ("username", PlainUser), ("password", "wrong-password"),
                ("scope", "offline_access"));
        }

        var response = await AuthTestHelpers.PostTokenAsync(_client,
            ("grant_type", "password"), ("username", PlainUser), ("password", AuthTestHelpers.UserPassword),
            ("scope", "offline_access"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("locked", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PasswordGrant_UnregisteredScope_IsRejected()
    {
        // Documents the constraint: only offline_access is registered, so clients must not ask
        // for openid/profile/roles unless RegisterScopes() is extended.
        var response = await AuthTestHelpers.PostTokenAsync(_client,
            ("grant_type", "password"), ("username", AdminUser), ("password", AuthTestHelpers.AdminPassword),
            ("scope", "profile"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("invalid_scope", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task RefreshGrant_IssuesANewTokenPair()
    {
        var first = await AuthTestHelpers.GetTokenAsync(_client, AdminUser, AuthTestHelpers.AdminPassword);

        var response = await AuthTestHelpers.PostTokenAsync(_client,
            ("grant_type", "refresh_token"), ("refresh_token", first.RefreshToken!));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var refreshed = await response.Content.ReadFromJsonAsync<TokenResponse>();
        Assert.NotNull(refreshed);
        Assert.False(string.IsNullOrWhiteSpace(refreshed.AccessToken));
    }

    [Fact]
    public async Task RefreshGrant_ReflectsRoleRevokedAfterIssue()
    {
        var first = await AuthTestHelpers.GetTokenAsync(_client, AdminUser, AuthTestHelpers.AdminPassword);
        Assert.Contains(Roles.Admin, RolesIn(first.AccessToken));

        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider
                .GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<CorvidaUser>>();
            var user = await users.FindByNameAsync(AdminUser);
            await users.RemoveFromRoleAsync(user!, Roles.Admin);
        }

        var response = await AuthTestHelpers.PostTokenAsync(_client,
            ("grant_type", "refresh_token"), ("refresh_token", first.RefreshToken!));
        var refreshed = await response.Content.ReadFromJsonAsync<TokenResponse>();

        // Claims are re-read from the store on refresh, so a revoked role must not survive.
        Assert.DoesNotContain(Roles.Admin, RolesIn(refreshed!.AccessToken));
    }

    [Fact]
    public async Task AccessToken_CarriesSubjectNameAndRole_ButNotTheSecurityStamp()
    {
        var token = await AuthTestHelpers.GetTokenAsync(_client, AdminUser, AuthTestHelpers.AdminPassword);

        var jwt = new JsonWebTokenHandler().ReadJsonWebToken(token.AccessToken);

        Assert.False(string.IsNullOrWhiteSpace(jwt.Subject));
        Assert.Contains(jwt.Claims, c => c.Type == "name" && c.Value == AdminUser);
        Assert.Contains(Roles.Admin, RolesIn(token.AccessToken));
        Assert.DoesNotContain(jwt.Claims, c => c.Type == CorvidaClaims.SecurityStamp);
    }

    private static IReadOnlyList<string> RolesIn(string accessToken) =>
        new JsonWebTokenHandler().ReadJsonWebToken(accessToken).Claims
            .Where(c => c.Type == "role")
            .Select(c => c.Value)
            .ToList();
}
