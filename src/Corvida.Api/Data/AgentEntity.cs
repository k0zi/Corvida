namespace Corvida.Api.Data;

public class AgentEntity
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Personality { get; set; } = string.Empty;
    public string Color { get; set; } = "#4C6EF5";
    public string? AvatarDataUri { get; set; }

    public ICollection<TaskEntity> AssignedTasks { get; set; } = [];
}
