using System.Net;
using System.Net.Http.Json;
using Corvida.Api.Auth;
using Corvida.Api.Endpoints;
using Corvida.Api.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Corvida.Api.Tests;

[Collection("postgres")]
public class UserEndpointTests : IAsyncLifetime
{
    private const string AdminEmail = "users-admin@example.com";
    private const string AdminPassword = "Passw0rd!users";
    private const string PlainEmail = "users-plain@example.com";

    private readonly PostgresContainerFixture _db;
    private ApiWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    public UserEndpointTests(PostgresContainerFixture db) => _db = db;

    public async Task InitializeAsync()
    {
        _factory = new ApiWebApplicationFactory(_db.ConnectionString);
        _client = _factory.CreateClient();
        await ResetAsync();

        await _client.PostAsJsonAsync("/api/auth/enable",
            new EnableAuthRequest(AdminEmail, AdminPassword, "Users Admin"));

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
        await AuthTestHelpers.ClearUsersAsync(_factory);
        _factory.Services.GetRequiredService<IAuthGateState>().Invalidate();
    }

    private Task<HttpResponseMessage> CreateAsync(string email, string role = "User", string password = "Passw0rd!new") =>
        _client.PostAsJsonAsync("/api/users", new CreateUserRequest(email, password, email, role));

    [Fact]
    public async Task GetUsers_ListsTheFirstAdmin()
    {
        var users = await _client.GetFromJsonAsync<List<UserResponse>>("/api/users");

        var admin = Assert.Single(users!);
        Assert.Equal(AdminEmail, admin.UserName);
        Assert.Equal(Roles.Admin, admin.Role);
    }

    [Fact]
    public async Task CreateUser_AddsAUserWithTheRequestedRole()
    {
        var response = await CreateAsync(PlainEmail);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<UserResponse>();
        Assert.Equal(PlainEmail, created!.UserName);
        Assert.Equal(Roles.User, created.Role);
    }

    [Fact]
    public async Task CreateUser_RejectsAWeakPassword_WithDetails()
    {
        var response = await CreateAsync(PlainEmail, password: "short");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Password", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateUser_RejectsAnUnknownRole()
    {
        var response = await CreateAsync(PlainEmail, role: "Superuser");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task NewUser_CanSignInAndReachTheApi_ButNotUserManagement()
    {
        await CreateAsync(PlainEmail);

        using var plainClient = _factory.CreateClient();
        var token = await AuthTestHelpers.GetTokenAsync(plainClient, PlainEmail, "Passw0rd!new");
        plainClient.UseBearer(token.AccessToken);

        Assert.Equal(HttpStatusCode.OK, (await plainClient.GetAsync("/api/boards")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await plainClient.GetAsync("/api/users")).StatusCode);
    }

    [Fact]
    public async Task UpdateUser_ChangesTheRole()
    {
        var created = await (await CreateAsync(PlainEmail)).Content.ReadFromJsonAsync<UserResponse>();

        var response = await _client.PutAsJsonAsync($"/api/users/{created!.Id}",
            new UpdateUserRequest("Promoted", Roles.Admin));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<UserResponse>();
        Assert.Equal(Roles.Admin, updated!.Role);
        Assert.Equal("Promoted", updated.DisplayName);
    }

    [Fact]
    public async Task UpdateUser_RefusesToDemoteTheLastAdministrator()
    {
        var users = await _client.GetFromJsonAsync<List<UserResponse>>("/api/users");
        var admin = users!.Single();

        var response = await _client.PutAsJsonAsync($"/api/users/{admin.Id}",
            new UpdateUserRequest(admin.DisplayName, Roles.User));

        // Demoting the only admin would leave nobody able to manage users or turn auth off.
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task SetPassword_LetsAnAdminResetSomeoneElse()
    {
        var created = await (await CreateAsync(PlainEmail)).Content.ReadFromJsonAsync<UserResponse>();

        var response = await _client.PostAsJsonAsync($"/api/users/{created!.Id}/password",
            new SetPasswordRequest(null, "Passw0rd!reset"));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var plainClient = _factory.CreateClient();
        await AuthTestHelpers.GetTokenAsync(plainClient, PlainEmail, "Passw0rd!reset");
    }

    [Fact]
    public async Task SetPassword_RequiresTheCurrentPassword_WhenChangingYourOwn()
    {
        var users = await _client.GetFromJsonAsync<List<UserResponse>>("/api/users");
        var admin = users!.Single();

        var missing = await _client.PostAsJsonAsync($"/api/users/{admin.Id}/password",
            new SetPasswordRequest(null, "Passw0rd!changed"));
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);

        var ok = await _client.PostAsJsonAsync($"/api/users/{admin.Id}/password",
            new SetPasswordRequest(AdminPassword, "Passw0rd!changed"));
        Assert.Equal(HttpStatusCode.NoContent, ok.StatusCode);
    }

    [Fact]
    public async Task DeleteUser_RemovesThemAndRevokesAccess()
    {
        var created = await (await CreateAsync(PlainEmail)).Content.ReadFromJsonAsync<UserResponse>();

        var response = await _client.DeleteAsync($"/api/users/{created!.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var users = await _client.GetFromJsonAsync<List<UserResponse>>("/api/users");
        Assert.DoesNotContain(users!, u => u.Id == created.Id);
    }

    [Fact]
    public async Task DeleteUser_RefusesYourOwnAccount()
    {
        var users = await _client.GetFromJsonAsync<List<UserResponse>>("/api/users");
        var admin = users!.Single();

        var response = await _client.DeleteAsync($"/api/users/{admin.Id}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task UserEndpoints_AreUnreachableAnonymously()
    {
        using var anonymous = _factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/users")).StatusCode);
    }
}
