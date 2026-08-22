using System.Diagnostics;
using Microsoft.EntityFrameworkCore;

namespace Corvida.Api.Auth;

/// <inheritdoc />
internal sealed class AuthGateState(
    IServiceScopeFactory scopes,
    ILogger<AuthGateState> logger) : IAuthGateState
{
    // Asymmetric on purpose: a stale "open" is a security hole, a stale "closed" is only a 401.
    private static readonly TimeSpan OpenTtl = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan ClosedTtl = TimeSpan.FromSeconds(30);

    private sealed record Snapshot(bool AuthRequired, long ExpiresAtTicks);

    private readonly SemaphoreSlim _gate = new(1, 1);
    private Snapshot? _snapshot;

    public async ValueTask<bool> IsAuthRequiredAsync(CancellationToken ct = default)
    {
        if (Volatile.Read(ref _snapshot) is { } hit && !IsExpired(hit))
            return hit.AuthRequired;

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // Another caller may have refreshed while we waited.
            if (Volatile.Read(ref _snapshot) is { } fresh && !IsExpired(fresh))
                return fresh.AuthRequired;

            bool required;
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
                required = await db.SystemSettings
                    .AsNoTracking()
                    .Where(x => x.Id == 1)
                    .Select(x => (bool?)x.AuthRequired)
                    .FirstOrDefaultAsync(ct)
                    .ConfigureAwait(false) ?? false;
            }
            catch (Exception ex)
            {
                // A database outage must never silently open the API.
                if (Volatile.Read(ref _snapshot) is { } stale)
                {
                    logger.LogError(ex, "Auth gate read failed; serving stale value {AuthRequired}", stale.AuthRequired);
                    return stale.AuthRequired;
                }

                logger.LogError(ex, "Auth gate read failed with no cached value; failing closed");
                throw;
            }

            var ttl = required ? ClosedTtl : OpenTtl;
            Volatile.Write(ref _snapshot, new Snapshot(
                required, Stopwatch.GetTimestamp() + (long)(ttl.TotalSeconds * Stopwatch.Frequency)));

            return required;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Invalidate() => Volatile.Write(ref _snapshot, null);

    private static bool IsExpired(Snapshot s) => s.ExpiresAtTicks <= Stopwatch.GetTimestamp();
}
