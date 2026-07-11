namespace Corvida.Api.Data;

public class BoardEntity
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string GroupsJson { get; set; } = "[]";
    public bool IsArchived { get; set; }

    public ICollection<TaskEntity> Tasks { get; set; } = [];
}
