namespace Corvida.Api.Auth;

/// <summary>
/// Caches the single "is authentication required" flag, so the authorization handler can consult it
/// on every request without a database round trip each time.
/// </summary>
public interface IAuthGateState
{
    ValueTask<bool> IsAuthRequiredAsync(CancellationToken ct = default);

    /// <summary>
    /// Drops the cached value so the next read hits the database. Called by the enable/disable
    /// endpoints; other API instances converge within the cache TTL.
    /// </summary>
    void Invalidate();
}
