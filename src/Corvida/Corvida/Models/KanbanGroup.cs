using System.Collections.Generic;

namespace Corvida.Models;

public class KanbanGroup
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<string> TaskIds { get; set; } = new();
}
