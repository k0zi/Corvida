using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Text.Json;

namespace Corvida.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<BoardEntity> Boards => Set<BoardEntity>();
    public DbSet<TaskEntity> Tasks => Set<TaskEntity>();
    public DbSet<AgentEntity> Agents => Set<AgentEntity>();

    // Npgsql refuses to write DateTime.Kind=Unspecified into "timestamp with time zone" columns.
    // Clients (desktop app, MCP server) are expected to send UTC, but this normalizes any
    // Unspecified-kind value as UTC instead of throwing, so a client-side bug can't 500 the API.
    private static readonly ValueConverter<DateTime, DateTime> UtcDateTimeConverter = new(
        v => v.Kind == DateTimeKind.Utc ? v : DateTime.SpecifyKind(v, DateTimeKind.Utc),
        v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

    private static readonly ValueConverter<DateTime?, DateTime?> UtcNullableDateTimeConverter = new(
        v => v.HasValue ? (v.Value.Kind == DateTimeKind.Utc ? v.Value : DateTime.SpecifyKind(v.Value, DateTimeKind.Utc)) : v,
        v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<BoardEntity>(b =>
        {
            b.HasKey(e => e.Id);
            b.Property(e => e.Id).HasColumnType("text");
            b.Property(e => e.Name).HasColumnType("text").IsRequired();
            b.Property(e => e.GroupsJson)
                .HasColumnType("jsonb")
                .IsRequired()
                .HasDefaultValue("[]");
            b.Property(e => e.AgentIdsJson)
                .HasColumnType("jsonb")
                .IsRequired()
                .HasDefaultValue("[]");
            b.Property(e => e.CellOrdersJson)
                .HasColumnType("jsonb")
                .IsRequired()
                .HasDefaultValue("[]");
        });

        model.Entity<AgentEntity>(a =>
        {
            a.HasKey(e => e.Id);
            a.Property(e => e.Id).HasColumnType("text");
            a.Property(e => e.Name).HasColumnType("text").IsRequired();
            a.Property(e => e.Personality).HasColumnType("text").IsRequired().HasDefaultValue("");
            a.Property(e => e.Color).HasColumnType("text").IsRequired().HasDefaultValue("#4C6EF5");
            a.Property(e => e.AvatarDataUri).HasColumnType("text");
        });

        model.Entity<TaskEntity>(t =>
        {
            t.HasKey(e => e.Id);
            t.Property(e => e.Id).HasColumnType("text");
            t.Property(e => e.BoardId).HasColumnType("text").IsRequired();
            t.Property(e => e.GroupId).HasColumnType("text").IsRequired();
            t.Property(e => e.Title).HasColumnType("text").IsRequired();
            t.Property(e => e.Description).HasColumnType("text").IsRequired().HasDefaultValue("");
            t.Property(e => e.Priority).HasColumnType("text").IsRequired().HasDefaultValue("Medium");
            t.Property(e => e.Created).HasColumnType("timestamptz").IsRequired().HasConversion(UtcDateTimeConverter);
            t.Property(e => e.PlannedStart).HasColumnType("timestamptz").HasConversion(UtcNullableDateTimeConverter);
            t.Property(e => e.PlannedEnd).HasColumnType("timestamptz").HasConversion(UtcNullableDateTimeConverter);
            t.Property(e => e.AssignedAgentId).HasColumnType("text");

            t.HasOne(e => e.Board)
                .WithMany(b => b.Tasks)
                .HasForeignKey(e => e.BoardId)
                .OnDelete(DeleteBehavior.Cascade);

            t.HasOne(e => e.AssignedAgent)
                .WithMany(a => a.AssignedTasks)
                .HasForeignKey(e => e.AssignedAgentId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
