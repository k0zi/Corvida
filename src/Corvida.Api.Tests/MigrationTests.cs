using Corvida.Api.Data;
using Corvida.Api.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace Corvida.Api.Tests;

[Collection("postgres")]
public class MigrationTests(PostgresContainerFixture db)
{
    private AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(db.ConnectionString)
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

        var pending = await ctx.Database.GetPendingMigrationsAsync();

        Assert.Empty(pending);
    }

    [Fact]
    public async Task MigrateAsync_IsIdempotent()
    {
        await using var ctx = CreateContext();
        await ctx.Database.MigrateAsync();
        await ctx.Database.MigrateAsync();
    }

    [Fact]
    public async Task AfterMigration_CanPersistAndQueryBoardEntity()
    {
        await using var ctx = CreateContext();
        await ctx.Database.MigrateAsync();

        var board = new BoardEntity { Id = "mig-board-1", Name = "Test Board" };
        ctx.Boards.Add(board);
        await ctx.SaveChangesAsync();

        await using var readCtx = CreateContext();
        var saved = await readCtx.Boards.AsNoTracking().FirstOrDefaultAsync(b => b.Id == "mig-board-1");

        Assert.NotNull(saved);
        Assert.Equal("Test Board", saved.Name);
        Assert.Equal("[]", saved.GroupsJson);
    }

    [Fact]
    public async Task AfterMigration_CanPersistAndQueryTaskEntity()
    {
        await using var ctx = CreateContext();
        await ctx.Database.MigrateAsync();

        var board = new BoardEntity { Id = "mig-board-2", Name = "Board For Tasks" };
        ctx.Boards.Add(board);
        var task = new TaskEntity
        {
            Id = "mig-task-1",
            BoardId = "mig-board-2",
            GroupId = "grp-1",
            Title = "My Task",
            Created = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        };
        ctx.Tasks.Add(task);
        await ctx.SaveChangesAsync();

        await using var readCtx = CreateContext();
        var saved = await readCtx.Tasks.AsNoTracking().FirstOrDefaultAsync(t => t.Id == "mig-task-1");

        Assert.NotNull(saved);
        Assert.Equal("My Task", saved.Title);
        Assert.Equal("mig-board-2", saved.BoardId);
    }

    [Fact]
    public async Task AfterMigration_CanPersistAndQueryAgentEntity()
    {
        await using var ctx = CreateContext();
        await ctx.Database.MigrateAsync();

        var agent = new AgentEntity { Id = "mig-agent-1", Name = "Test Agent" };
        ctx.Agents.Add(agent);
        await ctx.SaveChangesAsync();

        await using var readCtx = CreateContext();
        var saved = await readCtx.Agents.AsNoTracking().FirstOrDefaultAsync(a => a.Id == "mig-agent-1");

        Assert.NotNull(saved);
        Assert.Equal("Test Agent", saved.Name);
        Assert.Equal("#4C6EF5", saved.Color);
    }

    [Fact]
    public async Task AfterMigration_BoardEntity_HasAgentIdsAndCellOrdersJsonDefaults()
    {
        await using var ctx = CreateContext();
        await ctx.Database.MigrateAsync();

        var board = new BoardEntity { Id = "mig-board-4", Name = "Board With Defaults" };
        ctx.Boards.Add(board);
        await ctx.SaveChangesAsync();

        await using var readCtx = CreateContext();
        var saved = await readCtx.Boards.AsNoTracking().FirstOrDefaultAsync(b => b.Id == "mig-board-4");

        Assert.NotNull(saved);
        Assert.Equal("[]", saved.AgentIdsJson);
        Assert.Equal("[]", saved.CellOrdersJson);
    }

    [Fact]
    public async Task AfterMigration_DeleteAgent_SetsNullOnAssignedTasks()
    {
        await using var ctx = CreateContext();
        await ctx.Database.MigrateAsync();

        var board = new BoardEntity { Id = "mig-board-5", Name = "Board For Assignment" };
        var agent = new AgentEntity { Id = "mig-agent-2", Name = "Assignee" };
        ctx.Boards.Add(board);
        ctx.Agents.Add(agent);
        ctx.Tasks.Add(new TaskEntity
        {
            Id = "mig-task-3",
            BoardId = "mig-board-5",
            GroupId = "grp-1",
            Title = "Assigned Task",
            Created = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            AssignedAgentId = "mig-agent-2",
        });
        await ctx.SaveChangesAsync();

        ctx.Agents.Remove(agent);
        await ctx.SaveChangesAsync();

        await using var readCtx = CreateContext();
        var task = await readCtx.Tasks.AsNoTracking().FirstOrDefaultAsync(t => t.Id == "mig-task-3");

        Assert.NotNull(task);
        Assert.Null(task.AssignedAgentId);
    }

    [Fact]
    public async Task AfterMigration_DeleteBoard_CascadesToTasks()
    {
        await using var ctx = CreateContext();
        await ctx.Database.MigrateAsync();

        var board = new BoardEntity { Id = "mig-board-3", Name = "Cascade Board" };
        ctx.Boards.Add(board);
        ctx.Tasks.Add(new TaskEntity
        {
            Id = "mig-task-2",
            BoardId = "mig-board-3",
            GroupId = "grp-1",
            Title = "Orphan Task",
            Created = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        });
        await ctx.SaveChangesAsync();

        ctx.Boards.Remove(board);
        await ctx.SaveChangesAsync();

        await using var readCtx = CreateContext();
        var task = await readCtx.Tasks.AsNoTracking().FirstOrDefaultAsync(t => t.Id == "mig-task-2");

        Assert.Null(task);
    }
}
