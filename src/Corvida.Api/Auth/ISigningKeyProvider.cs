using System.Security.Cryptography.X509Certificates;

namespace Corvida.Api.Auth;

/// <summary>The certificates OpenIddict signs and encrypts tokens with.</summary>
public sealed record SigningKeyPair(X509Certificate2 Signing, X509Certificate2 Encryption);

/// <summary>
/// Supplies OpenIddict's signing/encryption certificates from <c>auth.signing_keys</c>.
/// </summary>
/// <remarks>
/// OpenIddict's development certificates live in the machine X.509 store, which is ephemeral in a
/// container: every <c>docker compose restart</c> would invalidate every issued token. Persisting
/// them in the database keeps tokens valid across restarts — and makes deliberate rotation
/// possible, which is what invalidates outstanding access tokens when the user store is wiped.
/// </remarks>
public interface ISigningKeyProvider
{
    /// <summary>Idempotent and safe to call concurrently; waits for the table to exist.</summary>
    Task<SigningKeyPair> GetAsync(CancellationToken ct = default);

    /// <summary>Blocking bridge for IConfigureOptions, which has no async form.</summary>
    SigningKeyPair Get();

    /// <summary>Discards and regenerates both certificates, invalidating every issued token.</summary>
    Task RotateAsync(CancellationToken ct = default);
}
