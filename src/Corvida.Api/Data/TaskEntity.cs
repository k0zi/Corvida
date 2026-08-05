namespace Corvida.Api.Data;

public class TaskEntity
{
    public string Id { get; set; } = string.Empty;
    public string BoardId { get; set; } = string.Empty;
    public string GroupId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Priority { get; set; } = "Medium";
    public DateTime Created { get; set; } = DateTime.UtcNow;
    public DateTime? PlannedStart { get; set; }
    public DateTime? PlannedEnd { get; set; }
    public string? AssignedAgentId { get; set; }

    public BoardEntity Board { get; set; } = null!;
    public AgentEntity? AssignedAgent { get; set; }
}
