using System;
using System.Threading;
using System.Threading.Tasks;

namespace Corvida.Services;

/// <summary>
/// Supplies a fixed personal access token from settings, for clients that cannot prompt anyone.
/// </summary>
/// <remarks>
/// The MCP server is a stdio process with no UI, so there is nowhere to show a login form and
/// nothing to refresh: the token is provisioned out of band and either works or does not.
/// </remarks>
public sealed class ApiKeyTokenProvider(ISettingsService settings) : ITokenProvider
{
    public event Action? SignedOut;

    public ValueTask<string?> GetAccessTokenAsync(CancellationToken ct = default)
    {
        var key = settings.Settings.ApiKey;
        return new(string.IsNullOrWhiteSpace(key) ? null : key);
    }

    /// <summary>Always false: a static token has nothing to refresh, so a 401 is final.</summary>
    public ValueTask<bool> TryRefreshAsync(CancellationToken ct = default)
    {
        SignedOut?.Invoke();
        return new(false);
    }

    public ValueTask SignInAsync(StoredTokens tokens) => ValueTask.CompletedTask;

    public ValueTask SignOutAsync() => ValueTask.CompletedTask;
}
