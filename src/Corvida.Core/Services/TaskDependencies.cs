using System;
using System.Collections.Generic;
using System.Linq;
using Corvida.Models;

namespace Corvida.Services;

/// <summary>
/// Rules for <see cref="KanbanTask.DependsOn"/>, shared by every client and the API so they agree on
/// what a valid dependency is. A dependency only ever points at a task on the same board.
/// </summary>
public static class TaskDependencies
{
    /// <summary>The column that counts as finished: the default boards and the plan skill both name it so.</summary>
    public const string DoneGroupName = "Done";

    public static KanbanGroup? FindDoneGroup(Board board) =>
        board.Groups.FirstOrDefault(g => string.Equals(g.Name.Trim(), DoneGroupName, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// True when making <paramref name="taskId"/> depend on <paramref name="predecessorId"/> would close a
    /// loop, i.e. the predecessor already depends on the task, directly or through others.
    /// </summary>
    public static bool WouldCreateCycle(IReadOnlyDictionary<string, KanbanTask> tasks, string taskId, string predecessorId)
    {
        var visited = new HashSet<string>();
        var stack = new Stack<string>();
        stack.Push(predecessorId);

        while (stack.Count > 0)
        {
            var id = stack.Pop();
            if (id == taskId) return true;
            if (!visited.Add(id) || !tasks.TryGetValue(id, out var task)) continue;
            foreach (var dep in task.DependsOn) stack.Push(dep);
        }

        return false;
    }

    /// <summary>True when one of the task's own dependencies leads back to it.</summary>
    public static bool HasCycleThrough(IReadOnlyDictionary<string, KanbanTask> tasks, KanbanTask task)
    {
        var withTask = new Dictionary<string, KanbanTask>(tasks) { [task.Id] = task };
        return task.DependsOn.Any(dep => WouldCreateCycle(withTask, task.Id, dep));
    }

    /// <summary>The task's dependencies without itself, duplicates and IDs that are not in <paramref name="existingIds"/>.</summary>
    public static List<string> Normalize(KanbanTask task, ICollection<string> existingIds) =>
        task.DependsOn
            .Where(id => id != task.Id && existingIds.Contains(id))
            .Distinct()
            .ToList();

    public static bool IsDone(KanbanTask task, string? doneGroupId) =>
        doneGroupId is not null && task.GroupId == doneGroupId;

    /// <summary>True when the task waits for a predecessor that is not in the Done column yet.</summary>
    public static bool IsBlocked(KanbanTask task, IReadOnlyDictionary<string, KanbanTask> tasks, string? doneGroupId) =>
        task.DependsOn.Any(id => tasks.TryGetValue(id, out var dep) && !IsDone(dep, doneGroupId));

    /// <summary>Removes <paramref name="deletedId"/> from every task's dependencies and returns the tasks that changed.</summary>
    public static List<KanbanTask> RemoveReferences(IEnumerable<KanbanTask> tasks, string deletedId) =>
        tasks.Where(t => t.DependsOn.RemoveAll(id => id == deletedId) > 0).ToList();
}
