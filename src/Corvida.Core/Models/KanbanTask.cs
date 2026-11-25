using System;
using System.Collections.Generic;

namespace Corvida.Models;

public class KanbanTask
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string GroupId { get; set; } = string.Empty;
    public string BoardId { get; set; } = string.Empty;
    public DateTime Created { get; set; } = DateTime.UtcNow;
    public string Priority { get; set; } = "Medium";
    public DateTime? PlannedStart { get; set; }
    public DateTime? PlannedEnd { get; set; }
    public string? AssignedAgentId { get; set; }

    /// <summary>IDs of the tasks on the same board that have to be finished before this one.</summary>
    public List<string> DependsOn { get; set; } = new();

    // Three-point PERT estimate in days; each one is optional.
    public double? EstimateOptimistic { get; set; }
    public double? EstimateMostLikely { get; set; }
    public double? EstimatePessimistic { get; set; }
}
