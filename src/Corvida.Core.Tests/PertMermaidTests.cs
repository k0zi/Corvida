using Corvida.Models;
using Corvida.Services;

namespace Corvida.Core.Tests;

public class PertMermaidTests
{
    [Fact]
    public void NodeId_ReplacesCharactersMermaidCannotTake()
    {
        Assert.Equal("t_Fix_login_task_ab12", PertMermaid.NodeId("Fix login-task ab12"));
    }

    [Fact]
    public void Build_MarksCriticalEdgesThick_AndOthersThin()
    {
        var schedule = PertCalculator.Calculate([
            new KanbanTask { Id = "a", Title = "A", EstimateMostLikely = 1 },
            new KanbanTask { Id = "b", Title = "B", EstimateMostLikely = 1, DependsOn = ["a"] },
            new KanbanTask { Id = "c", Title = "C", EstimateMostLikely = 5, DependsOn = ["a"] },
        ]);

        var text = PertMermaid.Build(schedule);

        Assert.StartsWith("flowchart LR", text);
        Assert.Contains("t_a ==> t_c", text);
        Assert.Contains("t_a --> t_b", text);
        Assert.Contains("★ C", text);
    }

    [Fact]
    public void Build_KeepsQuotesOutOfTheLabel_AndDrawsDoneTasksRound()
    {
        var schedule = PertCalculator.Calculate([new KanbanTask { Id = "a", Title = "Say \"hi\"", GroupId = "done" }]);

        var text = PertMermaid.Build(schedule, doneGroupId: "done");

        Assert.Contains("t_a([\"★ Say 'hi'<br>", text);
    }

    [Theory]
    [InlineData(-1e-9, "0")]
    [InlineData(2.1666, "2.2")]
    [InlineData(3, "3")]
    public void Days_RoundsToOneDecimal_WithoutANegativeZero(double value, string expected)
    {
        Assert.Equal(expected, PertMermaid.Days(value));
    }

    [Fact]
    public void TaskIdOf_FindsTheTaskBehindANode()
    {
        var schedule = PertCalculator.Calculate([new KanbanTask { Id = "x-1", Title = "X" }]);

        Assert.Equal("x-1", PertMermaid.TaskIdOf(schedule, PertMermaid.NodeId("x-1")));
        Assert.Null(PertMermaid.TaskIdOf(schedule, "t_nope"));
    }
}
