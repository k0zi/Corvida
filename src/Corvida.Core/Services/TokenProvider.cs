using System;
using System.Threading;
using System.Threading.Tasks;

namespace Corvida.Services;

/// <inheritdoc />
public sealed class TokenProvider(IAuthClient auth, ITokenStore store) : ITokenProvider
{
    /// <summary>Refresh this long before expiry, so a token cannot lapse mid-request.</summary>
    private static readonly TimeSpan RefreshWindow = TimeSpan.FromSeconds(60);

    // Refreshing must be single-flight. OpenIddict rotates refresh tokens and detects reuse, so
    // several concurrent requests each redeeming the same refresh token would revoke the whole
    // token chain and sign the user out.
    private readonly SemaphoreSlim _gate = new(1, 1);

    public event Action? SignedOut;

    public async ValueTask<string?> GetAccessTokenAsync(CancellationToken ct = default)
    {
        var tokens = await store.GetAsync().ConfigureAwait(false);
        if (tokens is null) return null;

        if (DateTimeOffset.UtcNow < tokens.ExpiresAt - RefreshWindow)
            return tokens.AccessToken;

        return await RefreshAsync(tokens, ct).ConfigureAwait(false);
    }

    public async ValueTask<bool> TryRefreshAsync(CancellationToken ct = default)
    {
        var tokens = await store.GetAsync().ConfigureAwait(false);
        if (tokens is null) return false;

        return await RefreshAsync(tokens, ct).ConfigureAwait(false) is not null;
    }

    public async ValueTask SignInAsync(StoredTokens tokens) =>
        await store.SetAsync(tokens).ConfigureAwait(false);

    public async ValueTask SignOutAsync()
    {
        await store.SetAsync(null).ConfigureAwait(false);
        SignedOut?.Invoke();
    }

    private async ValueTask<string?> RefreshAsync(StoredTokens known, CancellationToken ct)
    {
        if (known.RefreshToken is null)
        {
            await SignOutAsync().ConfigureAwait(false);
            return null;
        }

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // Someone else may have refreshed while we waited; if so, use their result.
            var current = await store.GetAsync().ConfigureAwait(false);
            if (current is null) return null;
            if (!ReferenceEquals(current, known) && DateTimeOffset.UtcNow < current.ExpiresAt - RefreshWindow)
                return current.AccessToken;

            if (current.RefreshToken is null)
            {
                await SignOutAsync().ConfigureAwait(false);
                return null;
            }

            var refreshed = await auth.RefreshAsync(current.RefreshToken).ConfigureAwait(false);
            if (refreshed is null)
            {
                await SignOutAsync().ConfigureAwait(false);
                return null;
            }

            await store.SetAsync(refreshed).ConfigureAwait(false);
            return refreshed.AccessToken;
        }
        finally
        {
            _gate.Release();
        }
    }
}
