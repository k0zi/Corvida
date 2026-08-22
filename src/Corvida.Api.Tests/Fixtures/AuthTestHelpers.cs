using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Corvida.Api.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Corvida.Api.Tests.Fixtures;

internal sealed record TokenResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("refresh_token")] string? RefreshToken,
    [property: JsonPropertyName("token_type")] string TokenType,
    [property: JsonPropertyName("expires_in")] int ExpiresIn);

internal static class AuthTestHelpers
{
    public const string AdminPassword = "Passw0rd!admin";
    public const string UserPassword = "Passw0rd!user";

    public static async Task<CorvidaUser> SeedUserAsync(
        ApiWebApplicationFactory factory, string userName, string password, string? role = null)
    {
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<CorvidaUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        if (role is not null && !await roles.RoleExistsAsync(role))
            await roles.CreateAsync(new IdentityRole(role));

        var user = new CorvidaUser
        {
            UserName = userName,
            Email = userName,
            DisplayName = userName,
        };

        var created = await users.CreateAsync(user, password);
        Assert.True(created.Succeeded,
            string.Join("; ", created.Errors.Select(e => $"{e.Code}: {e.Description}")));

        if (role is not null)
            Assert.True((await users.AddToRoleAsync(user, role)).Succeeded);

        return user;
    }

    /// <summary>Runs the password grant and asserts it succeeded.</summary>
    public static async Task<TokenResponse> GetTokenAsync(
        HttpClient client, string userName, string password)
    {
        var response = await PostTokenAsync(client,
            ("grant_type", "password"),
            ("username", userName),
            ("password", password),
            ("scope", "offline_access"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<TokenResponse>())!;
    }

    public static Task<HttpResponseMessage> PostTokenAsync(
        HttpClient client, params (string Key, string Value)[] form) =>
        client.PostAsync("/connect/token", new FormUrlEncodedContent(
            form.Select(f => new KeyValuePair<string, string>(f.Key, f.Value))));

    /// <summary>Removes every user, role assignment and token, leaving the schema intact.</summary>
    public static async Task ClearUsersAsync(ApiWebApplicationFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AuthDbContext>();

        await ctx.Database.ExecuteSqlRawAsync(
            """
            TRUNCATE TABLE
                auth."AspNetUserRoles", auth."AspNetUserClaims", auth."AspNetUserLogins",
                auth."AspNetUserTokens", auth."AspNetUsers",
                auth."OpenIddictTokens", auth."OpenIddictAuthorizations"
            RESTART IDENTITY CASCADE
            """);
    }

    public static void UseBearer(this HttpClient client, string accessToken) =>
        client.DefaultRequestHeaders.Authorization = new("Bearer", accessToken);
}
