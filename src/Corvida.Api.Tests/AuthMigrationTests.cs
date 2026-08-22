using Corvida.Api.Auth;
using Corvida.Api.Tests.Fixtures;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Corvida.Api.Tests;

[Collection("postgres")]
public class AuthMigrationTests(PostgresContainerFixture db)
{
    private AuthDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AuthDbContext>()
            .UseNpgsql(db.ConnectionString, npgsql => npgsql
                .MigrationsHistoryTable(AuthSchema.MigrationsHistoryTable, AuthSchema.Name))
            .Options);

    [Fact]
    public async Task MigrateAsync_CompletesWithoutError()
    {
        await using var ctx = CreateContext();
        await ctx.Database.MigrateAsync();
    }

    [Fact]
    public async Task AfterMigration_NoPendingMigrations()
    {
        await using var ctx = CreateContext();
        await ctx.Database.MigrateAsync();

        Assert.Empty(await ctx.Database.GetPendingMigrationsAsync());
    }

    [Fact]
    public async Task MigrateAsync_IsIdempotent()
    {
        await using var ctx = CreateContext();
        await ctx.Database.MigrateAsync();
        await ctx.Database.MigrateAsync();
    }

    [Fact]
    public async Task AfterMigration_SystemSettingsIsSeededOpen()
    {
        await using var ctx = CreateContext();
        await ctx.Database.MigrateAsync();

        var settings = await ctx.SystemSettings.AsNoTracking().SingleAsync();

        Assert.Equal(1, settings.Id);
        // A fresh install must behave exactly as the API did before auth existed.
        Assert.False(settings.AuthRequired);
    }

    [Fact]
    public async Task AfterMigration_CanPersistIdentityUser()
    {
        await using var ctx = CreateContext();
        await ctx.Database.MigrateAsync();

        var user = new CorvidaUser
        {
            Id = "mig-user-1",
            UserName = "mig-user-1@example.com",
            NormalizedUserName = "MIG-USER-1@EXAMPLE.COM",
            Email = "mig-user-1@example.com",
            NormalizedEmail = "MIG-USER-1@EXAMPLE.COM",
            DisplayName = "Migration User",
        };
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        await using var readCtx = CreateContext();
        var saved = await readCtx.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == "mig-user-1");

        Assert.NotNull(saved);
        Assert.Equal("Migration User", saved.DisplayName);

        ctx.Users.Remove(user);
        await ctx.SaveChangesAsync();
    }

    [Fact]
    public async Task AfterMigration_CanPersistRoleAndPersonalAccessToken()
    {
        await using var ctx = CreateContext();
        await ctx.Database.MigrateAsync();

        ctx.Roles.Add(new IdentityRole { Id = "mig-role-1", Name = "Tester", NormalizedName = "TESTER" });
        ctx.PersonalAccessTokens.Add(new PersonalAccessTokenEntity
        {
            Id = "mig-pat-1",
            Name = "mcp",
            KeyHash = "hash-1",
            OwnerUserId = "mig-user-1",
            CreatedAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();

        await using var readCtx = CreateContext();
        Assert.NotNull(await readCtx.Roles.AsNoTracking().FirstOrDefaultAsync(r => r.Id == "mig-role-1"));
        Assert.NotNull(await readCtx.PersonalAccessTokens.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == "mig-pat-1"));

        ctx.Roles.RemoveRange(ctx.Roles.Where(r => r.Id == "mig-role-1"));
        ctx.PersonalAccessTokens.RemoveRange(ctx.PersonalAccessTokens.Where(t => t.Id == "mig-pat-1"));
        await ctx.SaveChangesAsync();
    }

    [Fact]
    public async Task AllAuthTables_LiveInTheAuthSchema()
    {
        // The wipe on "disable auth" derives its table list from this context's model and targets
        // the auth schema; a table that landed in `public` would silently survive the wipe.
        await using var ctx = CreateContext();
        await ctx.Database.MigrateAsync();

        var strayTables = ctx.Model.GetEntityTypes()
            .Where(t => t.GetTableName() is not null)
            .Where(t => (t.GetSchema() ?? "public") != AuthSchema.Name)
            .Select(t => t.GetTableName())
            .ToList();

        Assert.Empty(strayTables);
    }

    [Fact]
    public void MigrationsAssembly_IsNotEmpty()
    {
        using var ctx = CreateContext();

        Assert.NotEmpty(ctx.Database.GetMigrations());
    }

    [Fact]
    public void Model_MatchesSnapshot()
    {
        using var ctx = CreateContext();
        var differ = ctx.GetService<IMigrationsModelDiffer>();
        var snapshot = ctx.GetService<IMigrationsAssembly>().ModelSnapshot;

        Assert.NotNull(snapshot);

        var snapshotModel = snapshot.Model;
        if (snapshotModel is IMutableModel mutable)
            snapshotModel = mutable.FinalizeModel();
        snapshotModel = ctx.GetService<IModelRuntimeInitializer>().Initialize(snapshotModel, designTime: true);

        var differences = differ.GetDifferences(
            snapshotModel.GetRelationalModel(),
            ctx.GetService<IDesignTimeModel>().Model.GetRelationalModel());

        Assert.Empty(differences);
    }
}
