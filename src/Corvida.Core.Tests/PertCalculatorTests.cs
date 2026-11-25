using Corvida.Models;
using Corvida.Services;

namespace Corvida.Core.Tests;

public class PertCalculatorTests
{
    private static KanbanTask Task(string id, double? mostLikely = null, params string[] dependsOn) => new()
    {
        Id = id,
        Title = id.ToUpperInvariant(),
        EstimateMostLikely = mostLikely,
        DependsOn = dependsOn.ToList(),
    };

    private static PertNode Node(PertSchedule schedule, string id) => schedule.Nodes.Single(n => n.Task.Id == id);

    [Fact]
    public void Estimate_UsesTheThreePointFormula()
    {
        var task = new KanbanTask { EstimateOptimistic = 1, EstimateMostLikely = 2, EstimatePessimistic = 9 };

        var (expected, variance, hasEstimate) = PertCalculator.Estimate(task);

        Assert.Equal(3, expected, 9);           // (1 + 8 + 9) / 6
        Assert.Equal(64.0 / 36, variance, 9);   // ((9 - 1) / 6)²
        Assert.True(hasEstimate);
    }

    [Fact]
    public void Estimate_WithoutAnyValue_FallsBackToTheDefaultDuration()
    {
        var (expected, variance, hasEstimate) = PertCalculator.Estimate(new KanbanTask());

        Assert.Equal(PertCalculator.DefaultDuration, expected);
        Assert.Equal(0, variance);
        Assert.False(hasEstimate);
    }

    [Fact]
    public void Estimate_WithOnlyMostLikely_UsesIt()
    {
        Assert.Equal(4, PertCalculator.Estimate(new KanbanTask { EstimateMostLikely = 4 }).Expected);
    }

    [Fact]
    public void LinearChain_IsOnePhasePerTask_AndAllCritical()
    {
        var schedule = PertCalculator.Calculate([Task("c", 3, "b"), Task("a", 1), Task("b", 2, "a")]);

        Assert.Equal(["a", "b", "c"], schedule.Nodes.Select(n => n.Task.Id));
        Assert.Equal([0, 1, 2], schedule.Nodes.Select(n => n.Phase));
        Assert.All(schedule.Nodes, n => Assert.True(n.IsCritical));
        Assert.Equal(6, schedule.ProjectDuration);
        Assert.Equal(["a", "b", "c"], schedule.CriticalPath.Select(n => n.Task.Id));
    }

    [Fact]
    public void Diamond_PutsTheMiddleTasksInOneParallelPhase_AndFindsTheCriticalPath()
    {
        // a → b (2 days) → d, a → c (5 days) → d
        var schedule = PertCalculator.Calculate([
            Task("a", 1), Task("b", 2, "a"), Task("c", 5, "a"), Task("d", 1, "b", "c"),
        ]);

        Assert.Equal(3, schedule.Phases.Count);
        Assert.Equal(["b", "c"], schedule.Phases[1].Select(n => n.Task.Id));
        Assert.Equal(7, schedule.ProjectDuration);
        Assert.Equal(["a", "c", "d"], schedule.CriticalPath.Select(n => n.Task.Id));

        var b = Node(schedule, "b");
        Assert.False(b.IsCritical);
        Assert.Equal(1, b.EarliestStart);
        Assert.Equal(3, b.EarliestFinish);
        Assert.Equal(4, b.LatestStart);
        Assert.Equal(6, b.LatestFinish);
        Assert.Equal(3, b.Slack);
    }

    [Fact]
    public void ProjectStdDev_SumsTheVariancesOnTheCriticalPath()
    {
        var a = new KanbanTask { Id = "a", EstimateOptimistic = 1, EstimateMostLikely = 2, EstimatePessimistic = 7 };   // σ = 1
        var b = new KanbanTask { Id = "b", DependsOn = ["a"], EstimateOptimistic = 0, EstimateMostLikely = 4, EstimatePessimistic = 8 }; // σ = 4/3

        var schedule = PertCalculator.Calculate([a, b]);

        Assert.Equal(Math.Sqrt(1 + 16.0 / 9), schedule.ProjectStdDev, 9);
    }

    [Fact]
    public void Cycle_IsReported_WithoutTheTasksMerelyAfterIt()
    {
        var schedule = PertCalculator.Calculate([
            Task("start"), Task("x", null, "start", "y"), Task("y", null, "x"), Task("after", null, "y"),
        ]);

        Assert.True(schedule.HasCycle);
        Assert.Equal(["x", "y"], schedule.Cycle.Select(t => t.Id).Order());
        Assert.Empty(schedule.Nodes);
    }

    [Fact]
    public void UnknownAndSelfDependencies_AreIgnored()
    {
        var schedule = PertCalculator.Calculate([Task("a", 2, "deleted-task", "a")]);

        Assert.False(schedule.HasCycle);
        Assert.Empty(Node(schedule, "a").Predecessors);
        Assert.Equal(2, schedule.ProjectDuration);
    }

    [Fact]
    public void EmptyBoard_HasAnEmptySchedule()
    {
        var schedule = PertCalculator.Calculate([]);

        Assert.Empty(schedule.Nodes);
        Assert.Equal(0, schedule.ProjectDuration);
    }
}
