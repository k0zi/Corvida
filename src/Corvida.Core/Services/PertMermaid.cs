using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Corvida.Services;

/// <summary>
/// The PERT network as a Mermaid flowchart, for a diagram view to lay out. The view draws in the
/// colours of the theme and ignores styling statements, so the critical path is marked by thick
/// arrows and a star in the label rather than by colour.
/// </summary>
public static partial class PertMermaid
{
    public const string NodeIdPrefix = "t_";

    public static string Build(PertSchedule schedule, string? doneGroupId = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine("flowchart LR");

        foreach (var node in schedule.Nodes)
        {
            var label = Label(node);
            var shape = TaskDependencies.IsDone(node.Task, doneGroupId) ? $"([\"{label}\"])" : $"[\"{label}\"]";
            sb.AppendLine($"    {NodeId(node.Task.Id)}{shape}");
        }

        foreach (var node in schedule.Nodes)
            foreach (var succ in node.Successors)
            {
                var arrow = PertSchedule.IsCriticalEdge(node, succ) ? "==>" : "-->";
                sb.AppendLine($"    {NodeId(node.Task.Id)} {arrow} {NodeId(succ.Task.Id)}");
            }

        return sb.ToString();
    }

    /// <summary>The diagram node of a task; unsafe characters become underscores.</summary>
    public static string NodeId(string taskId) => NodeIdPrefix + UnsafeIdChars().Replace(taskId, "_");

    /// <summary>The task ID behind a diagram node ID, or null when no task matches it.</summary>
    public static string? TaskIdOf(PertSchedule schedule, string nodeId) =>
        schedule.Nodes.Select(n => n.Task.Id).FirstOrDefault(id => NodeId(id) == nodeId);

    private static string Label(PertNode node)
    {
        var title = node.Task.Title.Replace('"', '\'').ReplaceLineEndings(" ").Trim();
        if (node.IsCritical) title = "★ " + title;
        var estimate = node.HasEstimate ? Days(node.Expected) : $"{Days(node.Expected)}?";
        return $"{title}<br>{estimate} d · {Days(node.EarliestStart)}–{Days(node.EarliestFinish)}<br>slack {Days(node.Slack)}";
    }

    public static string Days(double value)
    {
        // Slack comes out of subtractions as -0.0000001 now and then, which would print as "-0".
        var rounded = Math.Round(value, 1);
        if (rounded == 0) rounded = 0;
        return rounded.ToString("0.#", CultureInfo.InvariantCulture);
    }

    [GeneratedRegex("[^A-Za-z0-9_]")]
    private static partial Regex UnsafeIdChars();
}
