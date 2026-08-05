using System.Collections.ObjectModel;
using Corvida.Models;

namespace Corvida.ViewModels;

// One row of the swimlane grid: a board member (Agent), or the implicit Unassigned pseudo-row
// when AgentId is null. Cells are kept in the same order/count as BoardEditorViewModel.GroupCards.
public class SwimlaneRowViewModel : ViewModelBase
{
    public Agent? Agent { get; }
    public string? AgentId { get; }

    public string DisplayName => AgentId is null ? "Unassigned" : (Agent?.Name ?? "Unknown Agent");
    public string ColorHex => Agent?.Color ?? "#808080";
    public string? AvatarDataUri => Agent?.AvatarDataUri;

    public ObservableCollection<SwimlaneCellViewModel> Cells { get; } = new();

    public SwimlaneRowViewModel(Agent? agent, string? agentId)
    {
        Agent = agent;
        AgentId = agentId;
    }
}
