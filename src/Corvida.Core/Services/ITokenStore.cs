using System;
using System.Threading.Tasks;

namespace Corvida.Services;

/// <param name="ExpiresAt">When the access token stops being accepted.</param>
public sealed record StoredTokens(string AccessToken, DateTimeOffset ExpiresAt, string? RefreshToken);

/// <summary>
/// Where a client keeps its tokens. Interface rather than a concrete type because the desktop, the
/// browser build and the MCP server each have different persistence (and different threat models).
/// </summary>
public interface ITokenStore
{
    ValueTask<StoredTokens?> GetAsync();
    ValueTask SetAsync(StoredTokens? tokens);
}

/// <summary>
/// Keeps tokens in memory only, so nothing reaches disk and closing the app signs the user out.
/// </summary>
public sealed class InMemoryTokenStore : ITokenStore
{
    private StoredTokens? _tokens;

    public ValueTask<StoredTokens?> GetAsync() => new(Volatile.Read(ref _tokens));

    public ValueTask SetAsync(StoredTokens? tokens)
    {
        Volatile.Write(ref _tokens, tokens);
        return ValueTask.CompletedTask;
    }
}
