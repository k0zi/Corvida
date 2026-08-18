using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Corvida.Models;
using Corvida.Services;

namespace Corvida.ViewModels;

public partial class TaskEditorViewModel : ViewModelBase
{
    private static readonly Agent UnassignedSentinel = new() { Id = "", Name = "Unassigned" };

    private readonly ITaskService _taskService;
    private readonly IAgentService _agentService;
    private readonly Action<KanbanTask> _onSaved;
    private readonly Action _onBack;

    private readonly KanbanTask _task;
    private readonly Board _board;

    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private string _selectedPriority = "Medium";
    [ObservableProperty] private DateTimeOffset? _plannedStart;
    [ObservableProperty] private DateTimeOffset? _plannedEnd;
    [ObservableProperty] private Agent? _selectedAssignedAgent;

    public IReadOnlyList<string> Priorities { get; } = new[] { "Low", "Medium", "High" };

    public ObservableCollection<Agent> BoardMembers { get; } = new() { UnassignedSentinel };

    public string BoardName => _board.Name;

    public TaskEditorViewModel(KanbanTask task, Board board, ITaskService taskService, IAgentService agentService,
        Action<KanbanTask> onSaved, Action onBack)
    {
        _task = task;
        _board = board;
        _taskService = taskService;
        _agentService = agentService;
        _onSaved = onSaved;
        _onBack = onBack;

        Title = task.Title;
        Description = task.Description;
        SelectedPriority = task.Priority;
        PlannedStart = task.PlannedStart.HasValue ? new DateTimeOffset(task.PlannedStart.Value) : null;
        PlannedEnd = task.PlannedEnd.HasValue ? new DateTimeOffset(task.PlannedEnd.Value) : null;
        SelectedAssignedAgent = UnassignedSentinel;

        _ = LoadBoardMembersAsync();
    }

    private async Task LoadBoardMembersAsync()
    {
        foreach (var agentId in _board.AgentIds)
        {
            var agent = await _agentService.GetAgentAsync(agentId);
            if (agent is not null) BoardMembers.Add(agent);
        }

        SelectedAssignedAgent = BoardMembers.FirstOrDefault(a => a.Id == _task.AssignedAgentId) ?? UnassignedSentinel;
    }

    [RelayCommand]
    private async Task Save()
    {
        _task.Title = Title.Trim();
        _task.Description = Description;
        _task.Priority = SelectedPriority;
        _task.PlannedStart = PlannedStart?.UtcDateTime;
        _task.PlannedEnd = PlannedEnd?.UtcDateTime;
        _task.AssignedAgentId = string.IsNullOrEmpty(SelectedAssignedAgent?.Id) ? null : SelectedAssignedAgent.Id;
        await _taskService.SaveTaskAsync(_task);
        _onSaved(_task);
    }

    [RelayCommand]
    private void GoBack() => _onBack();
}
