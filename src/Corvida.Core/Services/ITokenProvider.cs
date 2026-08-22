using System;
using System.Threading;
using System.Threading.Tasks;

namespace Corvida.Services;

/// <summary>
/// Supplies the access token for outgoing requests, refreshing it when it is close to expiry.
/// </summary>
public interface ITokenProvider
{
    /// <summary>The current access token, refreshed if needed, or null when signed out.</summary>
    ValueTask<string?> GetAccessTokenAsync(CancellationToken ct = default);

    /// <summary>Attempts a refresh after a 401. False means the caller must sign in again.</summary>
    ValueTask<bool> TryRefreshAsync(CancellationToken ct = default);

    /// <summary>Stores the tokens obtained from a successful sign-in.</summary>
    ValueTask SignInAsync(StoredTokens tokens);

    /// <summary>Discards the tokens and raises <see cref="SignedOut"/>.</summary>
    ValueTask SignOutAsync();

    /// <summary>Raised when the session ends, either by signing out or by a refresh failing.</summary>
    event Action? SignedOut;
}
