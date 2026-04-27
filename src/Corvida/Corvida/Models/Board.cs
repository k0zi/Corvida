using System.Collections.Generic;

namespace Corvida.Models;

public class Board
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<KanbanGroup> Groups { get; set; } = new();
}
