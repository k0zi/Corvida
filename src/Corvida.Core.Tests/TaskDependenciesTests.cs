using Corvida.Models;
using Corvida.Services;

namespace Corvida.Core.Tests;

public class TaskDependenciesTests
{
    private static Dictionary<string, KanbanTask> Tasks(params KanbanTask[] tasks) => tasks.ToDictionary(t => t.Id);

    private static KanbanTask Task(string id, string groupId = "todo", params string[] dependsOn) =>
        new() { Id = id, GroupId = groupId, DependsOn = dependsOn.ToList() };

    [Fact]
    public void WouldCreateCycle_SelfDependency_IsACycle()
    {
        Assert.True(TaskDependencies.WouldCreateCycle(Tasks(Task("a")), "a", "a"));
    }

    [Fact]
    public void WouldCreateCycle_IndirectLoop_IsDetected()
    {
        // c waits for b, b waits for a: making a wait for c closes the loop.
        var tasks = Tasks(Task("a"), Task("b", dependsOn: "a"), Task("c", dependsOn: "b"));

        Assert.True(TaskDependencies.WouldCreateCycle(tasks, "a", "c"));
        Assert.False(TaskDependencies.WouldCreateCycle(tasks, "c", "a"));
    }

    [Fact]
    public void HasCycleThrough_ChecksTheTasksOwnProposedDependencies()
    {
        var tasks = Tasks(Task("a"), Task("b", dependsOn: "a"));

        Assert.True(TaskDependencies.HasCycleThrough(tasks, Task("a", dependsOn: "b")));
        Assert.False(TaskDependencies.HasCycleThrough(tasks, Task("c", dependsOn: "b")));
    }

    [Fact]
    public void Normalize_DropsSelfDuplicatesAndUnknownIds()
    {
        var task = Task("a", dependsOn: ["a", "b", "b", "gone"]);

        Assert.Equal(["b"], TaskDependencies.Normalize(task, ["a", "b"]));
    }

    [Fact]
    public void IsBlocked_UntilEveryPrerequisiteIsInTheDoneGroup()
    {
        var a = Task("a", "done");
        var b = Task("b", "todo");
        var c = Task("c", "todo", "a", "b");
        var tasks = Tasks(a, b, c);

        Assert.True(TaskDependencies.IsBlocked(c, tasks, "done"));
        b.GroupId = "done";
        Assert.False(TaskDependencies.IsBlocked(c, tasks, "done"));
    }

    [Fact]
    public void IsBlocked_IgnoresPrerequisitesThatNoLongerExist()
    {
        Assert.False(TaskDependencies.IsBlocked(Task("a", dependsOn: "gone"), Tasks(), "done"));
    }

    [Fact]
    public void FindDoneGroup_MatchesTheNameIgnoringCase()
    {
        var board = new Board { Groups = [new() { Id = "g1", Name = "To-Do" }, new() { Id = "g2", Name = " done " }] };

        Assert.Equal("g2", TaskDependencies.FindDoneGroup(board)?.Id);
        Assert.Null(TaskDependencies.FindDoneGroup(new Board()));
    }

    [Fact]
    public void RemoveReferences_ReturnsOnlyTheTasksThatChanged()
    {
        var b = Task("b", dependsOn: "a");
        var c = Task("c", dependsOn: "x");

        var changed = TaskDependencies.RemoveReferences([b, c], "a");

        Assert.Equal([b], changed);
        Assert.Empty(b.DependsOn);
        Assert.Equal(["x"], c.DependsOn);
    }
}
