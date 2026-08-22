using Corvida.Api.Auth;
using Microsoft.EntityFrameworkCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Corvida.Api.Endpoints;

public static class ApiKeyEndpoints
{
    public static void MapApiKeyEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/api/apikeys").RequireAuthorization(Policies.Admin);

        g.MapGet("/", GetKeys);
        g.MapPost("/", CreateKey);
        g.MapDelete("/{id}", RevokeKey);
    }

    private static async Task<IResult> GetKeys(AuthDbContext db)
    {
        var keys = await db.PersonalAccessTokens
            .AsNoTracking()
            .OrderByDescending(k => k.CreatedAt)
            .Select(k => new ApiKeyResponse(k.Id, k.Name, k.CreatedAt, k.LastUsedAt, k.ExpiresAt))
            .ToListAsync();

        return Results.Ok(keys);
    }

    private static async Task<IResult> CreateKey(
        CreateApiKeyRequest request, AuthDbContext db, TimeProvider time, HttpContext http)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return Results.BadRequest(new { Error = "Name is required." });

        var ownerId = http.User.FindFirst(Claims.Subject)?.Value;
        if (ownerId is null) return Results.Unauthorized();

        var raw = PersonalAccessTokens.Generate();
        var now = time.GetUtcNow().UtcDateTime;

        var entity = new PersonalAccessTokenEntity
        {
            Id = Guid.NewGuid().ToString("n"),
            Name = request.Name.Trim(),
            // Only the hash is stored, so a database leak does not hand over working credentials.
            KeyHash = PersonalAccessTokens.Hash(raw),
            OwnerUserId = ownerId,
            CreatedAt = now,
            ExpiresAt = request.ExpiresInDays is > 0 ? now.AddDays(request.ExpiresInDays.Value) : null,
        };

        db.PersonalAccessTokens.Add(entity);
        await db.SaveChangesAsync(http.RequestAborted);

        // The raw value is returned exactly once; it cannot be recovered later.
        return Results.Created($"/api/apikeys/{entity.Id}", new CreatedApiKeyResponse(
            entity.Id, entity.Name, raw, entity.CreatedAt, entity.ExpiresAt));
    }

    private static async Task<IResult> RevokeKey(string id, AuthDbContext db, HttpContext http)
    {
        var deleted = await db.PersonalAccessTokens
            .Where(k => k.Id == id)
            .ExecuteDeleteAsync(http.RequestAborted);

        return deleted == 0 ? Results.NotFound() : Results.NoContent();
    }
}

public record ApiKeyResponse(
    string Id, string Name, DateTime CreatedAt, DateTime? LastUsedAt, DateTime? ExpiresAt);

public record CreatedApiKeyResponse(
    string Id, string Name, string Key, DateTime CreatedAt, DateTime? ExpiresAt);

public record CreateApiKeyRequest(string Name, int? ExpiresInDays);
