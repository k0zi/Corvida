using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Corvida.Api.Auth;

/// <summary>
/// Identity + auth state, in its own <c>auth</c> schema but the same database as
/// <see cref="Corvida.Api.Data.AppDbContext"/>.
/// </summary>
/// <remarks>
/// Kept separate from AppDbContext on purpose:
/// <list type="bullet">
/// <item>the "disable auth wipes the user store" operation can derive its table list from this
/// context's model instead of a hand-maintained denylist that would rot;</item>
/// <item>the API test fixture swaps <c>DbContextOptions&lt;AppDbContext&gt;</c>, which would
/// silently drop <c>UseOpenIddict()</c> if the OpenIddict stores hung off that context;</item>
/// <item>board/task migrations keep a readable snapshot, free of Identity and OpenIddict tables.</item>
/// </list>
/// </remarks>
public class AuthDbContext(DbContextOptions<AuthDbContext> options)
    : IdentityDbContext<CorvidaUser, IdentityRole, string>(options)
{
    public DbSet<SystemSettingsEntity> SystemSettings => Set<SystemSettingsEntity>();
    public DbSet<PersonalAccessTokenEntity> PersonalAccessTokens => Set<PersonalAccessTokenEntity>();
    public DbSet<SigningKeyEntity> SigningKeys => Set<SigningKeyEntity>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.HasDefaultSchema(AuthSchema.Name);

        base.OnModelCreating(model);

        // Registered on the ModelBuilder rather than via DbContextOptionsBuilder.UseOpenIddict():
        // the options-builder overload works by ReplaceService<IModelCustomizer, ...>, which the
        // API test fixture silently drops when it repoints this context at the test container —
        // every integration test would then get a model with no OpenIddict entity types.
        // HasDefaultSchema still reaches these four tables: OpenIddict calls the single-argument
        // ToTable(), and GetSchema() resolves lazily through Model.GetDefaultSchema().
        model.UseOpenIddict();

        model.Entity<SystemSettingsEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.AuthRequired).IsRequired();
            e.Property(x => x.UpdatedAt).HasColumnType("timestamptz");
            e.ToTable(t => t.HasCheckConstraint("CK_system_settings_singleton", "\"Id\" = 1"));

            // The API starts open, exactly as it behaved before authentication existed.
            e.HasData(new SystemSettingsEntity
            {
                Id = 1,
                AuthRequired = false,
                UpdatedAt = DateTime.UnixEpoch,
            });
        });

        model.Entity<PersonalAccessTokenEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnType("text");
            e.Property(x => x.Name).HasColumnType("text").IsRequired();
            e.Property(x => x.KeyHash).HasColumnType("text").IsRequired();
            e.Property(x => x.OwnerUserId).HasColumnType("text").IsRequired();
            e.Property(x => x.CreatedAt).HasColumnType("timestamptz").IsRequired();
            e.Property(x => x.LastUsedAt).HasColumnType("timestamptz");
            e.Property(x => x.ExpiresAt).HasColumnType("timestamptz");
            e.HasIndex(x => x.KeyHash).IsUnique();
        });

        model.Entity<SigningKeyEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnType("text");
            e.Property(x => x.Pkcs12).IsRequired();
            e.Property(x => x.CreatedAt).HasColumnType("timestamptz").IsRequired();
        });
    }
}
