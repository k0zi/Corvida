using System.Security.Claims;
using Corvida.Api.Auth;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Corvida.Api.Endpoints;

public static class TokenEndpoints
{
    public static void MapTokenEndpoint(this WebApplication app)
    {
        // AllowAnonymous is load-bearing: the fallback policy applies CorvidaAccessRequirement to
        // every endpoint without explicit metadata, which with the gate closed would 401 the very
        // endpoint used to obtain credentials.
        app.MapPost("/connect/token", ExchangeAsync)
           .AllowAnonymous()
           .RequireRateLimiting(RateLimitPolicies.Token);
    }

    private static async Task<IResult> ExchangeAsync(
        HttpContext http,
        UserManager<CorvidaUser> users,
        SignInManager<CorvidaUser> signIn)
    {
        var request = http.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

        if (request.IsPasswordGrantType())
            return await PasswordGrantAsync(request, users, signIn);

        if (request.IsRefreshTokenGrantType())
            return await RefreshGrantAsync(http, users, signIn);

        return InvalidGrant("The specified grant type is not supported.");
    }

    private static async Task<IResult> PasswordGrantAsync(
        OpenIddictRequest request, UserManager<CorvidaUser> users, SignInManager<CorvidaUser> signIn)
    {
        var user = await users.FindByNameAsync(request.Username!);
        if (user is null)
            return InvalidGrant("The username/password couple is invalid.");

        // lockoutOnFailure is what makes IdentityOptions.Lockout.MaxFailedAccessAttempts real.
        // CheckPasswordSignInAsync (not PasswordSignInAsync) — the latter routes through the
        // two-factor ticket path, which needs token providers we deliberately do not register.
        var result = await signIn.CheckPasswordSignInAsync(user, request.Password!, lockoutOnFailure: true);
        if (!result.Succeeded)
            return InvalidGrant(result.IsLockedOut
                ? "This account is temporarily locked. Try again later."
                : "The username/password couple is invalid.");

        var identity = await CreateIdentityAsync(users, user);
        identity.SetScopes(request.GetScopes());
        identity.SetDestinations(GetDestinations);

        return Results.SignIn(new ClaimsPrincipal(identity), properties: null,
            authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private static async Task<IResult> RefreshGrantAsync(
        HttpContext http, UserManager<CorvidaUser> users, SignInManager<CorvidaUser> signIn)
    {
        // OpenIddict validated and decrypted the refresh token during UseAuthentication();
        // this retrieves the principal it produced.
        var result = await http.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

        var subject = result.Principal?.GetClaim(Claims.Subject);
        var user = subject is null ? null : await users.FindByIdAsync(subject);
        if (user is null)
            return InvalidGrant("The refresh token is no longer valid.");

        if (!await signIn.CanSignInAsync(user))
            return InvalidGrant("The user is no longer allowed to sign in.");

        // Claims are re-read from the store: a role revoked after the refresh token was issued
        // must not survive a refresh.
        var identity = await CreateIdentityAsync(users, user);
        identity.SetScopes(result.Principal!.GetScopes());
        identity.SetDestinations(GetDestinations);

        return Results.SignIn(new ClaimsPrincipal(identity), properties: null,
            authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private static async Task<ClaimsIdentity> CreateIdentityAsync(
        UserManager<CorvidaUser> users, CorvidaUser user)
    {
        // nameType/roleType MUST match what the validation stack uses when it rehydrates the
        // principal on the resource-server side (OpenIddictValidationOptions hardcodes
        // Claims.Name / Claims.Role). Getting this wrong makes User.IsInRole silently false,
        // which looks like a broken authorization policy.
        var identity = new ClaimsIdentity(
            authenticationType: TokenValidationParameters.DefaultAuthenticationType,
            nameType: Claims.Name,
            roleType: Claims.Role);

        identity.SetClaim(Claims.Subject, await users.GetUserIdAsync(user))
                .SetClaim(Claims.Name, await users.GetUserNameAsync(user))
                .SetClaim(Claims.PreferredUsername, await users.GetUserNameAsync(user))
                .SetClaim(Claims.Email, await users.GetEmailAsync(user))
                .SetClaim(CorvidaClaims.DisplayName, user.DisplayName)
                .SetClaims(Claims.Role, [.. await users.GetRolesAsync(user)]);

        return identity;
    }

    /// <summary>
    /// Claims carry no destination by default and are silently dropped from every issued token.
    /// Corvida issues no identity tokens, so everything the API needs goes to the access token.
    /// </summary>
    private static IEnumerable<string> GetDestinations(Claim claim)
    {
        // Never expose Identity's security stamp in a token the client can read.
        if (claim.Type is CorvidaClaims.SecurityStamp)
            yield break;

        yield return Destinations.AccessToken;
    }

    private static IResult InvalidGrant(string description) =>
        Results.Forbid(
            new AuthenticationProperties(new Dictionary<string, string?>
            {
                [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidGrant,
                [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = description,
            }),
            [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
}
