using System.Collections.Generic;

namespace Corvida.Models;

public class SwimlaneCellOrder
{
    public string GroupId { get; set; } = string.Empty;
    public string? AgentId { get; set; }
    public List<string> TaskIds { get; set; } = new();
}
