using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Corvida.Exceptions;

namespace Corvida.Services;

/// <inheritdoc />
public class AuthClient(IHttpClientFactory factory, ISettingsService settings) : IAuthClient
{
    private HttpClient Client => factory.CreateClient("CorvidaApi");
    private string Base => (settings.Settings.ServerUrl ?? "http://localhost:5083").TrimEnd('/');

    public async Task<AuthStatus> GetStatusAsync()
    {
        try
        {
            return await Client.GetFromJsonAsync<AuthStatus>($"{Base}/api/auth/status")
                   ?? new AuthStatus(false, "/connect/token");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw HttpErrors.Translate(ex, Base);
        }
    }

    public Task<StoredTokens> SignInAsync(string userName, string password) =>
        RequestTokenAsync(
            [
                new("grant_type", "password"),
                new("username", userName),
                new("password", password),
                // offline_access is the only registered scope; asking for more is rejected.
                new("scope", "offline_access"),
            ],
            allowFailure: false)!;

    public async Task<StoredTokens?> RefreshAsync(string refreshToken) =>
        await RequestTokenAsync(
            [
                new("grant_type", "refresh_token"),
                new("refresh_token", refreshToken),
            ],
            allowFailure: true);

    private async Task<StoredTokens?> RequestTokenAsync(
        List<KeyValuePair<string, string>> form, bool allowFailure)
    {
        HttpResponseMessage response;
        try
        {
            response = await Client.PostAsync($"{Base}/connect/token", new FormUrlEncodedContent(form));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw HttpErrors.Translate(ex, Base);
        }

        if (response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadFromJsonAsync<TokenResponse>();
            if (body is null || string.IsNullOrEmpty(body.AccessToken))
                throw new CorvidaAuthenticationException("The server returned an empty token response.");

            return new StoredTokens(
                body.AccessToken,
                DateTimeOffset.UtcNow.AddSeconds(body.ExpiresIn),
                body.RefreshToken);
        }

        // A refresh failing is routine (the token expired or was revoked); a sign-in failing is not.
        if (allowFailure) return null;

        throw new CorvidaAuthenticationException(await DescribeFailureAsync(response));
    }

    private static async Task<string> DescribeFailureAsync(HttpResponseMessage response)
    {
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
            return "Too many attempts. Please wait a moment and try again.";

        try
        {
            // The token endpoint answers with RFC 6749 snake_case, unlike the rest of the API.
            var error = await response.Content.ReadFromJsonAsync<TokenError>();
            if (!string.IsNullOrWhiteSpace(error?.ErrorDescription))
                return error.ErrorDescription!;
        }
        catch (JsonException)
        {
            // Fall through to the generic message below.
        }

        return "Sign-in failed. Check your username and password.";
    }

    public async Task<SignedInUser> GetCurrentUserAsync()
    {
        try
        {
            return await Client.GetFromJsonAsync<SignedInUser>($"{Base}/api/auth/me")
                   ?? throw new CorvidaAuthenticationException("The server did not return a user.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw HttpErrors.Translate(ex, Base);
        }
    }

    public async Task EnableAuthAsync(string email, string password, string displayName)
    {
        try
        {
            var response = await Client.PostAsJsonAsync($"{Base}/api/auth/enable",
                new { Email = email, Password = password, DisplayName = displayName });

            if (response.StatusCode == HttpStatusCode.Conflict)
                throw new InvalidOperationException("Authentication is already enabled on this server.");

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException(await DescribeEnableFailureAsync(response));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw HttpErrors.Translate(ex, Base);
        }
    }

    private static async Task<string> DescribeEnableFailureAsync(HttpResponseMessage response)
    {
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<EnableError>();
            if (problem?.Details is { Length: > 0 })
                return string.Join(" ", problem.Details);
            if (!string.IsNullOrWhiteSpace(problem?.Error))
                return problem.Error!;
        }
        catch (JsonException)
        {
            // Fall through.
        }

        return "The server rejected the request.";
    }

    public async Task DisableAuthAsync(string password)
    {
        try
        {
            var response = await Client.PostAsJsonAsync($"{Base}/api/auth/disable",
                new { Password = password, Confirm = "DELETE ALL USERS" });

            if (response.StatusCode == HttpStatusCode.Unauthorized)
                throw new CorvidaAuthenticationException("That password is not correct.");

            if (response.StatusCode == HttpStatusCode.Forbidden)
                throw new CorvidaAuthorizationException("Only an administrator can turn authentication off.");

            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw HttpErrors.Translate(ex, Base);
        }
    }

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("refresh_token")] string? RefreshToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);

    private sealed record TokenError(
        [property: JsonPropertyName("error")] string? Error,
        [property: JsonPropertyName("error_description")] string? ErrorDescription);

    private sealed record EnableError(string? Error, string[]? Details);
}
