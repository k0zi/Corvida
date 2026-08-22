using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Corvida.Api.Auth;

/// <inheritdoc />
internal sealed class SigningKeyProvider(
    IServiceScopeFactory scopes,
    TimeProvider time,
    ILogger<SigningKeyProvider> logger) : ISigningKeyProvider
{
    private const string SigningId = "signing";
    private const string EncryptionId = "encryption";

    /// <summary>Postgres <c>undefined_table</c>: migrations have not caught up yet.</summary>
    private const string UndefinedTable = "42P01";

    private static readonly TimeSpan TableWaitTimeout = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan TableWaitDelay = TimeSpan.FromMilliseconds(500);

    private readonly SemaphoreSlim _gate = new(1, 1);
    private SigningKeyPair? _cached;

    // ASP.NET Core installs no SynchronizationContext, so this cannot deadlock. It only ever
    // blocks while OpenIddict's options are built for the first time, and MigrationHostedService
    // normally warms the cache before any request gets that far.
    public SigningKeyPair Get() => GetAsync().GetAwaiter().GetResult();

    public async Task<SigningKeyPair> GetAsync(CancellationToken ct = default)
    {
        if (Volatile.Read(ref _cached) is { } hit) return hit;

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (Volatile.Read(ref _cached) is { } fresh) return fresh;

            var pair = new SigningKeyPair(
                await LoadOrCreateAsync(SigningId, X509KeyUsageFlags.DigitalSignature, ct).ConfigureAwait(false),
                await LoadOrCreateAsync(EncryptionId, X509KeyUsageFlags.KeyEncipherment, ct).ConfigureAwait(false));

            Volatile.Write(ref _cached, pair);
            return pair;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task RotateAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
            await db.SigningKeys.ExecuteDeleteAsync(ct).ConfigureAwait(false);

            Volatile.Write(ref _cached, null);
            logger.LogWarning("Signing keys rotated; all previously issued tokens are now invalid");
        }
        finally
        {
            _gate.Release();
        }

        await GetAsync(ct).ConfigureAwait(false);
    }

    private async Task<X509Certificate2> LoadOrCreateAsync(
        string id, X509KeyUsageFlags usage, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();

        var existing = await ReadWaitingForTableAsync(db, id, ct).ConfigureAwait(false);
        if (existing is not null) return Import(existing);

        var certificate = Create(usage);
        var pkcs12 = certificate.Export(X509ContentType.Pkcs12);
        try
        {
            db.SigningKeys.Add(new SigningKeyEntity
            {
                Id = id,
                Pkcs12 = pkcs12,
                CreatedAt = time.GetUtcNow().UtcDateTime,
            });
            await db.SaveChangesAsync(ct).ConfigureAwait(false);

            logger.LogInformation("Generated a new {KeyId} certificate", id);
            return certificate;
        }
        catch (DbUpdateException)
        {
            // Another instance inserted first; whoever won owns the key.
            certificate.Dispose();

            var winner = await db.SigningKeys.AsNoTracking()
                .Where(k => k.Id == id)
                .Select(k => k.Pkcs12)
                .SingleAsync(ct)
                .ConfigureAwait(false);

            return Import(winner);
        }
        finally
        {
            Array.Clear(pkcs12);
        }
    }

    /// <summary>
    /// Reads the stored key, tolerating the window where the host is already listening but
    /// <see cref="MigrationHostedService"/> has not created the table yet. EnableRetryOnFailure
    /// does not cover "relation does not exist", so it is handled explicitly.
    /// </summary>
    private async Task<byte[]?> ReadWaitingForTableAsync(
        AuthDbContext db, string id, CancellationToken ct)
    {
        var deadline = time.GetUtcNow() + TableWaitTimeout;

        while (true)
        {
            try
            {
                return await db.SigningKeys.AsNoTracking()
                    .Where(k => k.Id == id)
                    .Select(k => k.Pkcs12)
                    .FirstOrDefaultAsync(ct)
                    .ConfigureAwait(false);
            }
            catch (PostgresException ex) when (ex.SqlState == UndefinedTable)
            {
                if (time.GetUtcNow() >= deadline) throw;

                logger.LogDebug("auth.SigningKeys does not exist yet; waiting for migrations");
                await Task.Delay(TableWaitDelay, time, ct).ConfigureAwait(false);
            }
        }
    }

    private X509Certificate2 Create(X509KeyUsageFlags usage)
    {
        using var rsa = RSA.Create(4096);

        var role = usage is X509KeyUsageFlags.DigitalSignature ? "Signing" : "Encryption";
        var request = new CertificateRequest(
            new X500DistinguishedName($"CN=Corvida {role} Certificate"),
            rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        // OpenIddict rejects a certificate whose key usage does not cover its role (ID0060/ID0070).
        request.CertificateExtensions.Add(new X509KeyUsageExtension(usage, critical: true));

        var now = time.GetUtcNow();

        // Long-lived on purpose: expiry would take the API down until an admin intervened, and the
        // key never leaves the database.
        return request.CreateSelfSigned(now, now.AddYears(10));
    }

    private static X509Certificate2 Import(byte[] pkcs12)
    {
        // EphemeralKeySet keeps the private key out of the OS keystore, which is what makes this
        // work in a container with no writable user profile. macOS does not support it.
        var flags = OperatingSystem.IsMacOS()
            ? X509KeyStorageFlags.Exportable
            : X509KeyStorageFlags.EphemeralKeySet;

        return X509CertificateLoader.LoadPkcs12(pkcs12, password: null, flags);
    }
}
