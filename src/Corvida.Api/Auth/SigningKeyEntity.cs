namespace Corvida.Api.Auth;

/// <summary>
/// The OpenIddict signing/encryption certificate, as a PKCS#12 blob.
/// </summary>
/// <remarks>
/// OpenIddict's development certificates live in the machine X.509 store, which is ephemeral in a
/// container — every restart would invalidate every issued token. Persisting the key here keeps
/// tokens valid across <c>docker compose restart</c>.
/// </remarks>
public class SigningKeyEntity
{
    public string Id { get; set; } = string.Empty;
    public byte[] Pkcs12 { get; set; } = [];
    public DateTime CreatedAt { get; set; }
}
