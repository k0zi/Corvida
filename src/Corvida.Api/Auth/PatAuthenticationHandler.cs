using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Corvida.Api.Auth;

/// <summary>
/// Authenticates non-interactive clients (the MCP server) with a long-lived personal access token.
/// </summary>
/// <remarks>
/// A dedicated opaque token rather than OpenIddict reference tokens: enabling those is a global
/// server setting that would make <em>every</em> access token opaque and database-backed, adding a
/// store read to every request from every client.
/// </remarks>
internal sealed class PatAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory loggerFactory,
    UrlEncoder encoder,
    AuthDbContext db,
    UserManager<CorvidaUser> users,
    TimeProvider time)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, loggerFactory, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var presented = PersonalAccessTokens.Extract(Context);
        if (presented is null) return AuthenticateResult.NoResult();

        var hash = PersonalAccessTokens.Hash(presented);

        var token = await db.PersonalAccessTokens
            .FirstOrDefaultAsync(t => t.KeyHash == hash, Context.RequestAborted);

        if (token is null)
            return AuthenticateResult.Fail("Unknown access token.");

        var now = time.GetUtcNow().UtcDateTime;
        if (token.ExpiresAt is { } expiry && expiry <= now)
            return AuthenticateResult.Fail("The access token has expired.");

        var user = await users.FindByIdAsync(token.OwnerUserId);
        if (user is null)
            return AuthenticateResult.Fail("The account behind this token no longer exists.");

        // Best-effort usage stamp: a failure here must not reject an otherwise valid request.
        try
        {
            token.LastUsedAt = now;
            await db.SaveChangesAsync(Context.RequestAborted);
        }
        catch (DbUpdateException ex)
        {
            Logger.LogDebug(ex, "Could not record last use of token {TokenId}", token.Id);
        }

        // Same name/role claim types as the token endpoint issues, so User.IsInRole works
        // identically whichever way the caller authenticated.
        var identity = new ClaimsIdentity(
            authenticationType: TokenValidationParameters.DefaultAuthenticationType,
            nameType: Claims.Name,
            roleType: Claims.Role);

        identity.AddClaim(new Claim(Claims.Subject, user.Id));
        identity.AddClaim(new Claim(Claims.Name, user.UserName ?? string.Empty));
        identity.AddClaim(new Claim(CorvidaClaims.DisplayName, user.DisplayName));

        foreach (var role in await users.GetRolesAsync(user))
            identity.AddClaim(new Claim(Claims.Role, role));

        var principal = new ClaimsPrincipal(identity);
        return AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.Headers.WWWAuthenticate = "Bearer";
        return Task.CompletedTask;
    }
}

public static class PersonalAccessTokens
{
    /// <summary>Reads the presented token from the header or the query string, or null.</summary>
    /// <remarks>
    /// The query string matters because SignalR's WebSocket transport cannot set headers.
    /// </remarks>
    public static string? Extract(HttpContext context)
    {
        var header = context.Request.Headers.Authorization.ToString();
        if (header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var value = header["Bearer ".Length..].Trim();
            if (value.StartsWith(CorvidaSchemes.PatPrefix, StringComparison.Ordinal)) return value;
        }

        var queryToken = context.Request.Query["access_token"].ToString();
        return queryToken.StartsWith(CorvidaSchemes.PatPrefix, StringComparison.Ordinal) ? queryToken : null;
    }

    public static bool IsPresented(HttpContext context) => Extract(context) is not null;

    /// <summary>Generates a token. The raw value is shown once and never stored.</summary>
    public static string Generate() =>
        CorvidaSchemes.PatPrefix + Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));

    public static string Hash(string rawToken) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
}
