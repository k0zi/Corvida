using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Corvida.Models;

namespace Corvida.ViewModels;

// One row of the swimlane grid: a board member (Agent), or the implicit Unassigned pseudo-row
// when AgentId is null. Cells are kept in the same order/count as BoardEditorViewModel.GroupCards.
public partial class SwimlaneRowViewModel : ViewModelBase
{
    // Bound to the lane's chevron. The board rebuilds its lanes whenever the view is recreated
    // (back from the task editor) and LoadAsync rebuilds the rows, so the state has to live here.
    [ObservableProperty]
    private bool _isExpanded = true;

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
