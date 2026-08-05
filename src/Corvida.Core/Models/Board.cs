using System.Collections.Generic;

namespace Corvida.Models;

public class Board
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<KanbanGroup> Groups { get; set; } = new();
    public bool IsArchived { get; set; }
    public List<string> AgentIds { get; set; } = new();
    public List<SwimlaneCellOrder> CellOrders { get; set; } = new();
}
