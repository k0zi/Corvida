using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Corvida.Models;

namespace Corvida.Services;

/// <inheritdoc />
public class HttpUserAdminService(IHttpClientFactory factory, ISettingsService settings) : IUserAdminService
{
    private HttpClient Client => factory.CreateClient("CorvidaApi");
    private string Base => (settings.Settings.ServerUrl ?? "http://localhost:5083").TrimEnd('/');

    public async Task<List<UserAccount>> GetUsersAsync()
    {
        try
        {
            return await Client.GetFromJsonAsync<List<UserAccount>>($"{Base}/api/users") ?? [];
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw HttpErrors.Translate(ex, Base);
        }
    }

    public async Task<UserAccount> CreateUserAsync(string email, string password, string displayName, string role)
    {
        try
        {
            var response = await Client.PostAsJsonAsync($"{Base}/api/users",
                new { Email = email, Password = password, DisplayName = displayName, Role = role });

            await ThrowIfRejectedAsync(response);
            return (await response.Content.ReadFromJsonAsync<UserAccount>())!;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw HttpErrors.Translate(ex, Base);
        }
    }

    public async Task<UserAccount> SaveUserAsync(UserAccount user)
    {
        try
        {
            var response = await Client.PutAsJsonAsync($"{Base}/api/users/{user.Id}",
                new { user.DisplayName, user.Role });

            await ThrowIfRejectedAsync(response);
            return (await response.Content.ReadFromJsonAsync<UserAccount>())!;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw HttpErrors.Translate(ex, Base);
        }
    }

    public async Task SetPasswordAsync(string userId, string? currentPassword, string newPassword)
    {
        try
        {
            var response = await Client.PostAsJsonAsync($"{Base}/api/users/{userId}/password",
                new { CurrentPassword = currentPassword, NewPassword = newPassword });

            await ThrowIfRejectedAsync(response);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw HttpErrors.Translate(ex, Base);
        }
    }

    public async Task DeleteUserAsync(string userId)
    {
        try
        {
            var response = await Client.DeleteAsync($"{Base}/api/users/{userId}");
            await ThrowIfRejectedAsync(response);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw HttpErrors.Translate(ex, Base);
        }
    }

    /// <summary>
    /// Surfaces the server's own explanation instead of a bare status code — Identity's password
    /// rules and the "last administrator" guard are only useful if the user can read them.
    /// </summary>
    private static async Task ThrowIfRejectedAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;

        if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict)
        {
            string? message = null;
            try
            {
                var problem = await response.Content.ReadFromJsonAsync<ServerError>();
                message = problem?.Details is { Length: > 0 }
                    ? string.Join(" ", problem.Details)
                    : problem?.Error;
            }
            catch (JsonException)
            {
                // Fall through to EnsureSuccessStatusCode below.
            }

            if (!string.IsNullOrWhiteSpace(message))
                throw new InvalidOperationException(message);
        }

        response.EnsureSuccessStatusCode();
    }

    private sealed record ServerError(string? Error, string[]? Details);
}
