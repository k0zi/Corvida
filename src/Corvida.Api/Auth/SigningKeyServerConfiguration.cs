using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Server;

namespace Corvida.Api.Auth;

/// <summary>
/// Feeds the persisted certificates into OpenIddict, replacing the built-in development
/// certificates (which would regenerate on every container restart).
/// </summary>
/// <remarks>
/// Must be <see cref="IConfigureOptions{TOptions}"/> rather than a post-configure step: OpenIddict's
/// own post-configuration validates that signing and encryption credentials exist (ID0085/ID0086),
/// so these have to be in place before it runs.
/// <para>
/// AddValidation().UseLocalServer() imports these credentials from the server options, so the
/// validation stack needs no configurator of its own.
/// </para>
/// </remarks>
internal sealed class SigningKeyServerConfiguration(ISigningKeyProvider keys)
    : IConfigureOptions<OpenIddictServerOptions>
{
    public void Configure(OpenIddictServerOptions options)
    {
        var pair = keys.Get();

        options.SigningCredentials.Add(new SigningCredentials(
            new X509SecurityKey(pair.Signing), SecurityAlgorithms.RsaSha256));

        options.EncryptionCredentials.Add(new EncryptingCredentials(
            new X509SecurityKey(pair.Encryption),
            SecurityAlgorithms.RsaOAEP,
            SecurityAlgorithms.Aes256CbcHmacSha512));
    }
}
