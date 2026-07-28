using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Text.Json;

namespace Corvida.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<BoardEntity> Boards => Set<BoardEntity>();
    public DbSet<TaskEntity> Tasks => Set<TaskEntity>();

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
            t.Property(e => e.Created).HasColumnType("timestamptz").IsRequired();
            t.Property(e => e.PlannedStart).HasColumnType("timestamptz");
            t.Property(e => e.PlannedEnd).HasColumnType("timestamptz");

            t.HasOne(e => e.Board)
                .WithMany(b => b.Tasks)
                .HasForeignKey(e => e.BoardId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
