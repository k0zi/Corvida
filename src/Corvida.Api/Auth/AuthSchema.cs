namespace Corvida.Api.Auth;

public static class AuthSchema
{
    /// <summary>Postgres schema holding every auth table, so the wipe can target it wholesale.</summary>
    public const string Name = "auth";

    /// <summary>Migration history table for <see cref="AuthDbContext"/>; must not collide with AppDbContext's.</summary>
    public const string MigrationsHistoryTable = "__EFMigrationsHistory";
}
