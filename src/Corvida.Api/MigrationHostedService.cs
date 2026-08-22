using Corvida.Api.Auth;
using Corvida.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Corvida.Api;

/// <summary>
/// Applies migrations for both DbContexts at startup.
/// </summary>
/// <remarks>
/// Runs as a hosted service rather than inline in Program.cs so the web host is already listening
/// (and answering health probes) while the database catches up.
/// </remarks>
public sealed class MigrationHostedService(
    IServiceScopeFactory scopes,
    ILogger<MigrationHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();

        await MigrateAsync<AppDbContext>(scope.ServiceProvider, cancellationToken);
        await MigrateAsync<AuthDbContext>(scope.ServiceProvider, cancellationToken);

        await scope.ServiceProvider.GetRequiredService<IRoleSeeder>()
            .EnsureRolesAsync(cancellationToken);

        // Materialise the signing certificates here, off the request path. Kestrel starts
        // listening before this service runs, so without the warm-up the first request to touch
        // OpenIddict would block while the keys are generated.
        await scope.ServiceProvider.GetRequiredService<ISigningKeyProvider>()
            .GetAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task MigrateAsync<TContext>(IServiceProvider sp, CancellationToken ct)
        where TContext : DbContext
    {
        var db = sp.GetRequiredService<TContext>();
        logger.LogInformation("Applying migrations for {Context}", typeof(TContext).Name);

        await db.Database
            .CreateExecutionStrategy()
            .ExecuteAsync(ct, (token) => db.Database.MigrateAsync(token));
    }
}
