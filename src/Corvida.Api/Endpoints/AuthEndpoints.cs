using System.Data;
using System.Diagnostics;
using Corvida.Api.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OpenIddict.Server;
using OpenIddict.Validation;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Corvida.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/api/auth");

        // Anonymous by necessity: every client calls this before it has credentials, to decide
        // whether to show a login screen. It is also install.sh's readiness probe.
        g.MapGet("/status", GetStatus).AllowAnonymous();

        g.MapPost("/enable", Enable)
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicies.Enable);

        g.MapPost("/disable", Disable).RequireAuthorization(Policies.Admin);

        g.MapGet("/me", Me).RequireAuthorization(Policies.User);
    }

    private static async Task<IResult> GetStatus(IAuthGateState gate, HttpContext http)
    {
        http.Response.Headers.CacheControl = "no-store";

        var authRequired = await gate.IsAuthRequiredAsync(http.RequestAborted);

        return Results.Ok(new AuthStatusResponse(authRequired, "/connect/token"));
    }

    private static IResult Me(HttpContext http)
    {
        var user = http.User;

        return Results.Ok(new MeResponse(
            UserId: user.FindFirst(Claims.Subject)?.Value ?? string.Empty,
            UserName: user.FindFirst(Claims.Name)?.Value ?? string.Empty,
            DisplayName: user.FindFirst(CorvidaClaims.DisplayName)?.Value ?? string.Empty,
            Roles: [.. user.FindAll(Claims.Role).Select(c => c.Value)]));
    }

    /// <summary>
    /// Turns on required authentication and creates the first Admin, in one step.
    /// </summary>
    /// <remarks>
    /// Anonymous on purpose: while the gate is open the API is fully readable anyway, so there is
    /// no account to authenticate as. The exposure is that whoever calls this first owns the
    /// instance — mitigated by the explicit CORS origin list, the rate limiter, and the
    /// single-flight guard below, and documented as "enable immediately after installing".
    /// </remarks>
    private static async Task<IResult> Enable(
        EnableAuthRequest request,
        AuthDbContext db,
        UserManager<CorvidaUser> users,
        IRoleSeeder roleSeeder,
        IAuthGateState gate,
        ILoggerFactory loggerFactory,
        HttpContext http)
    {
        var logger = loggerFactory.CreateLogger("Corvida.Auth");

        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return Results.BadRequest(new { Error = "Email and Password are required." });

        var strategy = db.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);

            // Claim the flag first. Two concurrent callers cannot both see a row affected, so
            // exactly one of them goes on to create the first Admin.
            var claimed = await db.Database.ExecuteSqlAsync(
                $"""
                 UPDATE auth."SystemSettings"
                 SET "AuthRequired" = true, "UpdatedAt" = now() AT TIME ZONE 'utc'
                 WHERE "Id" = 1 AND "AuthRequired" = false
                 """);

            if (claimed == 0)
                return Results.Conflict(new { Error = "Authentication is already enabled." });

            await roleSeeder.EnsureRolesAsync(http.RequestAborted);

            var admin = new CorvidaUser
            {
                UserName = request.Email.Trim(),
                Email = request.Email.Trim(),
                DisplayName = string.IsNullOrWhiteSpace(request.DisplayName)
                    ? request.Email.Trim()
                    : request.DisplayName.Trim(),
            };

            var created = await users.CreateAsync(admin, request.Password);
            if (!created.Succeeded)
            {
                await tx.RollbackAsync();
                return Results.BadRequest(new
                {
                    Error = "The password or email was rejected.",
                    Details = created.Errors.Select(e => e.Description).ToArray(),
                });
            }

            var inRole = await users.AddToRoleAsync(admin, Roles.Admin);
            if (!inRole.Succeeded)
            {
                await tx.RollbackAsync();
                return Results.Problem("Could not assign the Admin role.");
            }

            await tx.CommitAsync();
            gate.Invalidate();

            logger.LogWarning("AUDIT: authentication enabled; first admin {User} created from {Ip}",
                admin.UserName, http.Connection.RemoteIpAddress);

            // No token is issued here: OpenIddict can only sign in from within its own token
            // endpoint transaction. The client signs in normally at /connect/token.
            return Results.Ok(new EnableAuthResponse(admin.Id, admin.UserName!, "/connect/token"));
        });
    }

    /// <summary>
    /// Turns authentication back off and deletes the entire user store.
    /// </summary>
    private static async Task<IResult> Disable(
        DisableAuthRequest request,
        AuthDbContext db,
        UserManager<CorvidaUser> users,
        SignInManager<CorvidaUser> signIn,
        IAuthGateState gate,
        ISigningKeyProvider keys,
        IOptionsMonitorCache<OpenIddictServerOptions> serverOptions,
        IOptionsMonitorCache<OpenIddictValidationOptions> validationOptions,
        IHubConnectionRegistry connections,
        ILoggerFactory loggerFactory,
        HttpContext http)
    {
        var logger = loggerFactory.CreateLogger("Corvida.Auth");

        if (request.Confirm != DisableAuthRequest.RequiredConfirmation)
            return Results.BadRequest(new
            {
                Error = $"Set Confirm to \"{DisableAuthRequest.RequiredConfirmation}\" to proceed.",
            });

        // Not UserManager.GetUserAsync: that reads ClaimTypes.NameIdentifier, while OpenIddict
        // issues the OIDC "sub" claim and does not remap inbound claim types.
        var callerId = http.User.FindFirst(Claims.Subject)?.Value;
        var caller = callerId is null ? null : await users.FindByIdAsync(callerId);
        if (caller is null) return Results.Unauthorized();

        // This admin's own password, not just any admin's.
        var check = await signIn.CheckPasswordSignInAsync(caller, request.Password, lockoutOnFailure: true);
        if (!check.Succeeded) return Results.Unauthorized();

        // Reopen the gate before wiping, so no new authenticated work starts mid-truncate.
        await db.Database.ExecuteSqlAsync(
            $"""
             UPDATE auth."SystemSettings"
             SET "AuthRequired" = false, "UpdatedAt" = now() AT TIME ZONE 'utc'
             WHERE "Id" = 1
             """);
        gate.Invalidate();

        await WipeUserStoreAsync(db, http.RequestAborted);

        // Access tokens are self-contained JWTs: truncating the token table does nothing to the
        // ones already issued. Without rotating the keys, a stolen Admin token would outlive the
        // very operation meant to remove all accounts.
        await keys.RotateAsync(http.RequestAborted);
        serverOptions.Clear();
        validationOptions.Clear();

        await connections.AbortAllAsync(authRequired: false);

        logger.LogWarning("AUDIT: authentication disabled and user store wiped by {User} from {Ip}",
            caller.UserName, http.Connection.RemoteIpAddress);

        return Results.NoContent();
    }

    private static async Task WipeUserStoreAsync(AuthDbContext db, CancellationToken ct)
    {
        // The table list is derived from the model rather than hand-maintained, so an entity added
        // later cannot be silently left behind (or, worse, silently truncated when it shouldn't be).
        string[] preserved = [nameof(AuthDbContext.SystemSettings), nameof(AuthDbContext.SigningKeys)];

        var tables = db.Model.GetEntityTypes()
            .Select(t => (Schema: t.GetSchema() ?? "public", Name: t.GetTableName()))
            .Where(t => t.Name is not null && !preserved.Contains(t.Name))
            .Distinct()
            .Select(t => $"\"{t.Schema}\".\"{t.Name}\"")
            .ToArray();

        // Names come from the EF model, never from user input; assert the invariant anyway so a
        // future entity with an exotic name cannot turn this into an injection point.
        Debug.Assert(tables.All(t => !t.Contains(';')), "unexpected character in a model table name");

        // Fail fast rather than blocking behind an in-flight token request.
        await db.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout = '5s'", ct);
        // EF1002: table identifiers cannot be parameterised. The names come from the EF model,
        // never from request data, and the assertion above pins that invariant.
#pragma warning disable EF1002
        await db.Database.ExecuteSqlRawAsync(
            $"TRUNCATE TABLE {string.Join(", ", tables)} RESTART IDENTITY CASCADE", ct);
#pragma warning restore EF1002
    }
}

public record AuthStatusResponse(bool AuthRequired, string TokenEndpoint);

public record MeResponse(string UserId, string UserName, string DisplayName, string[] Roles);

public record EnableAuthRequest(string Email, string Password, string? DisplayName);

public record EnableAuthResponse(string AdminUserId, string AdminUserName, string TokenEndpoint);

public record DisableAuthRequest(string Password, string Confirm)
{
    public const string RequiredConfirmation = "DELETE ALL USERS";
}
