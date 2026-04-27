using System;

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
}
