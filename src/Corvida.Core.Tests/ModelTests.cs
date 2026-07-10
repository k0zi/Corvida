using Corvida.Models;

namespace Corvida.Core.Tests;

public class ModelTests
{
    [Fact]
    public void Board_Groups_IsEmptyListByDefault()
    {
        var board = new Board();
        Assert.NotNull(board.Groups);
        Assert.Empty(board.Groups);
    }

    [Fact]
    public void Board_TwoInstances_HaveIndependentGroupLists()
    {
        var a = new Board();
        var b = new Board();
        a.Groups.Add(new KanbanGroup { Name = "Test" });
        Assert.Empty(b.Groups);
    }

    [Fact]
    public void KanbanGroup_TaskIds_IsEmptyListByDefault()
    {
        var group = new KanbanGroup();
        Assert.NotNull(group.TaskIds);
        Assert.Empty(group.TaskIds);
    }

    [Fact]
    public void KanbanTask_Priority_DefaultsToMedium()
    {
        Assert.Equal("Medium", new KanbanTask().Priority);
    }

    [Fact]
    public void KanbanTask_PlannedStart_DefaultsToNull()
    {
        Assert.Null(new KanbanTask().PlannedStart);
    }

    [Fact]
    public void KanbanTask_PlannedEnd_DefaultsToNull()
    {
        Assert.Null(new KanbanTask().PlannedEnd);
    }

    [Fact]
    public void KanbanTask_Created_IsApproximatelyNow()
    {
        var before = DateTime.UtcNow;
        var task = new KanbanTask();
        var after = DateTime.UtcNow;
        Assert.True(task.Created >= before && task.Created <= after);
    }
}
