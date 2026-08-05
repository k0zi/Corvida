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

    [Fact]
    public void KanbanTask_AssignedAgentId_DefaultsToNull()
    {
        Assert.Null(new KanbanTask().AssignedAgentId);
    }

    [Fact]
    public void Board_AgentIds_IsEmptyListByDefault()
    {
        var board = new Board();
        Assert.NotNull(board.AgentIds);
        Assert.Empty(board.AgentIds);
    }

    [Fact]
    public void Board_CellOrders_IsEmptyListByDefault()
    {
        var board = new Board();
        Assert.NotNull(board.CellOrders);
        Assert.Empty(board.CellOrders);
    }

    [Fact]
    public void Board_TwoInstances_HaveIndependentAgentIdLists()
    {
        var a = new Board();
        var b = new Board();
        a.AgentIds.Add("agent-1");
        Assert.Empty(b.AgentIds);
    }

    [Fact]
    public void SwimlaneCellOrder_AgentId_DefaultsToNull()
    {
        Assert.Null(new SwimlaneCellOrder().AgentId);
    }

    [Fact]
    public void SwimlaneCellOrder_TaskIds_IsEmptyListByDefault()
    {
        var cell = new SwimlaneCellOrder();
        Assert.NotNull(cell.TaskIds);
        Assert.Empty(cell.TaskIds);
    }

    [Fact]
    public void Agent_Color_HasDefaultValue()
    {
        Assert.False(string.IsNullOrEmpty(new Agent().Color));
    }

    [Fact]
    public void Agent_AvatarDataUri_DefaultsToNull()
    {
        Assert.Null(new Agent().AvatarDataUri);
    }
}
