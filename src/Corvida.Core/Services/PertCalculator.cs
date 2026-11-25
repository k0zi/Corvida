using System;
using System.Collections.Generic;
using System.Linq;
using Corvida.Models;

namespace Corvida.Services;

public sealed class PertNode
{
    internal PertNode(KanbanTask task) => Task = task;

    public KanbanTask Task { get; }

    /// <summary>Expected duration in days: (O + 4M + P) / 6.</summary>
    public double Expected { get; internal set; }
    public double Variance { get; internal set; }

    /// <summary>False when the task has no estimate and <see cref="Expected"/> is the default.</summary>
    public bool HasEstimate { get; internal set; }

    public double EarliestStart { get; internal set; }
    public double EarliestFinish { get; internal set; }
    public double LatestStart { get; internal set; }
    public double LatestFinish { get; internal set; }
    public double Slack => LatestStart - EarliestStart;
    public bool IsCritical { get; internal set; }

    /// <summary>
    /// 0 for a task with no predecessors, otherwise one more than its latest predecessor. Tasks of the
    /// same phase do not depend on each other, so they can be worked on in parallel.
    /// </summary>
    public int Phase { get; internal set; }

    public List<PertNode> Predecessors { get; } = new();
    public List<PertNode> Successors { get; } = new();
}

public sealed class PertSchedule
{
    /// <summary>Every task in an order that can be executed: predecessors first.</summary>
    public IReadOnlyList<PertNode> Nodes { get; init; } = [];
    public IReadOnlyList<IReadOnlyList<PertNode>> Phases { get; init; } = [];
    public IReadOnlyList<PertNode> CriticalPath { get; init; } = [];
    public double ProjectDuration { get; init; }

    /// <summary>Standard deviation of the project duration: the root of the summed variances on the critical path.</summary>
    public double ProjectStdDev { get; init; }

    /// <summary>The tasks that depend on each other in a loop; when not empty there is no schedule.</summary>
    public IReadOnlyList<KanbanTask> Cycle { get; init; } = [];
    public bool HasCycle => Cycle.Count > 0;

    /// <summary>True when the dependency between the two lies on a critical path.</summary>
    public static bool IsCriticalEdge(PertNode from, PertNode to) =>
        from.IsCritical && to.IsCritical && Math.Abs(from.EarliestFinish - to.EarliestStart) < PertCalculator.Epsilon;
}

/// <summary>
/// Program Evaluation and Review Technique over the tasks of one board, using
/// <see cref="KanbanTask.DependsOn"/> as the network and the three-point estimates as durations.
/// </summary>
public static class PertCalculator
{
    /// <summary>Duration of a task without any estimate, so ordering and phases still work.</summary>
    public const double DefaultDuration = 1;

    internal const double Epsilon = 1e-9;

    public static PertSchedule Calculate(IReadOnlyList<KanbanTask> tasks)
    {
        var nodes = new Dictionary<string, PertNode>();
        foreach (var task in tasks) nodes.TryAdd(task.Id, new PertNode(task));

        // Unknown IDs (deleted tasks, tasks of another board) are not part of the network.
        foreach (var node in nodes.Values)
            foreach (var depId in node.Task.DependsOn.Distinct())
            {
                if (depId == node.Task.Id || !nodes.TryGetValue(depId, out var dep)) continue;
                node.Predecessors.Add(dep);
                dep.Successors.Add(node);
            }

        var ordered = TopologicalOrder(nodes.Values.ToList());
        if (ordered.Count < nodes.Count)
            return new PertSchedule { Cycle = FindCycle(nodes.Values.Except(ordered)) };

        foreach (var node in ordered)
        {
            (node.Expected, node.Variance, node.HasEstimate) = Estimate(node.Task);
            node.EarliestStart = node.Predecessors.Count == 0 ? 0 : node.Predecessors.Max(p => p.EarliestFinish);
            node.EarliestFinish = node.EarliestStart + node.Expected;
            node.Phase = node.Predecessors.Count == 0 ? 0 : node.Predecessors.Max(p => p.Phase) + 1;
        }

        var duration = ordered.Count == 0 ? 0 : ordered.Max(n => n.EarliestFinish);

        for (var i = ordered.Count - 1; i >= 0; i--)
        {
            var node = ordered[i];
            node.LatestFinish = node.Successors.Count == 0 ? duration : node.Successors.Min(s => s.LatestStart);
            node.LatestStart = node.LatestFinish - node.Expected;
            node.IsCritical = Math.Abs(node.Slack) < Epsilon;
        }

        var criticalPath = CriticalPath(ordered);

        return new PertSchedule
        {
            Nodes = ordered,
            Phases = ordered.GroupBy(n => n.Phase).OrderBy(g => g.Key).Select(g => (IReadOnlyList<PertNode>)g.ToList()).ToList(),
            CriticalPath = criticalPath,
            ProjectDuration = duration,
            ProjectStdDev = Math.Sqrt(criticalPath.Sum(n => n.Variance)),
        };
    }

    /// <summary>Expected duration and variance; missing estimates fall back as gracefully as they can.</summary>
    public static (double Expected, double Variance, bool HasEstimate) Estimate(KanbanTask task)
    {
        var o = Clamp(task.EstimateOptimistic);
        var m = Clamp(task.EstimateMostLikely);
        var p = Clamp(task.EstimatePessimistic);

        if (o is { } lo && m is { } likely && p is { } hi)
            return ((lo + 4 * likely + hi) / 6, Square((hi - lo) / 6), true);
        if (m is { } onlyLikely)
            return (onlyLikely, 0, true);
        if (o is { } low && p is { } high)
            return ((low + high) / 2, Square((high - low) / 6), true);
        if ((o ?? p) is { } single)
            return (single, 0, true);
        return (DefaultDuration, 0, false);

        static double? Clamp(double? v) => v is { } d && double.IsFinite(d) ? Math.Max(0, d) : null;
        static double Square(double v) => v * v;
    }

    // Kahn's algorithm; ties keep the order the tasks came in, so the result is stable.
    private static List<PertNode> TopologicalOrder(List<PertNode> nodes)
    {
        var inDegree = nodes.ToDictionary(n => n, n => n.Predecessors.Count);
        var queue = new Queue<PertNode>(nodes.Where(n => inDegree[n] == 0));
        var result = new List<PertNode>(nodes.Count);

        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            result.Add(node);
            foreach (var succ in node.Successors)
                if (--inDegree[succ] == 0) queue.Enqueue(succ);
        }

        return result;
    }

    // What Kahn's algorithm leaves over is the loops plus everything after them; peeling off the
    // tasks nothing left depends on narrows that down to the loops themselves.
    private static List<KanbanTask> FindCycle(IEnumerable<PertNode> leftOver)
    {
        var remaining = leftOver.ToHashSet();
        bool removed;
        do
        {
            var tails = remaining.Where(n => !n.Successors.Any(remaining.Contains)).ToList();
            removed = tails.Count > 0;
            remaining.ExceptWith(tails);
        } while (removed);

        return remaining.Select(n => n.Task).ToList();
    }

    // One chain of critical tasks from the start of the project to its end.
    private static List<PertNode> CriticalPath(List<PertNode> ordered)
    {
        var path = new List<PertNode>();
        var current = ordered.FirstOrDefault(n => n.IsCritical && n.Predecessors.Count == 0);
        while (current is not null)
        {
            path.Add(current);
            var from = current;
            current = from.Successors
                .Where(s => PertSchedule.IsCriticalEdge(from, s))
                .OrderBy(ordered.IndexOf)
                .FirstOrDefault();
        }
        return path;
    }
}
