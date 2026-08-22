using System.Threading.Tasks;

namespace Corvida.Services;

/// <param name="AuthRequired">Whether the server currently demands authentication.</param>
public sealed record AuthStatus(bool AuthRequired, string TokenEndpoint);

public sealed record SignedInUser(string UserId, string UserName, string DisplayName, string[] Roles)
{
    public bool IsAdmin => Array.IndexOf(Roles, "Admin") >= 0;
}

/// <summary>
/// Talks to the Corvida API's authentication endpoints.
/// </summary>
/// <remarks>
/// An interface rather than a concrete class from the start: the password grant cannot federate, so
/// signing in with an external provider will arrive as a second implementation rather than a
/// rewrite of everything that depends on this.
/// </remarks>
public interface IAuthClient
{
    /// <summary>Reads whether login is required. Safe to call unauthenticated.</summary>
    Task<AuthStatus> GetStatusAsync();

    /// <summary>Exchanges a username and password for tokens.</summary>
    Task<StoredTokens> SignInAsync(string userName, string password);

    /// <summary>Exchanges a refresh token for a new pair, or null if it is no longer valid.</summary>
    Task<StoredTokens?> RefreshAsync(string refreshToken);

    /// <summary>Reads the signed-in user's identity and roles.</summary>
    Task<SignedInUser> GetCurrentUserAsync();

    /// <summary>Turns on required authentication and creates the first administrator.</summary>
    Task EnableAuthAsync(string email, string password, string displayName);

    /// <summary>Turns authentication off, deleting every account.</summary>
    Task DisableAuthAsync(string password);
}
