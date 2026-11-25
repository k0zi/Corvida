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
    private readonly IDialogService _dialogService;
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

    /// <summary>Estimate and prerequisites; null until the board's other tasks have loaded.</summary>
    [ObservableProperty] private TaskPlanningViewModel? _planning;

    public IReadOnlyList<string> Priorities { get; } = new[] { "Low", "Medium", "High" };

    public ObservableCollection<Agent> BoardMembers { get; } = new() { UnassignedSentinel };

    public string BoardName => _board.Name;

    public TaskEditorViewModel(KanbanTask task, Board board, ITaskService taskService, IAgentService agentService,
        IDialogService dialogService, Action<KanbanTask> onSaved, Action onBack)
    {
        _task = task;
        _board = board;
        _taskService = taskService;
        _agentService = agentService;
        _dialogService = dialogService;
        _onSaved = onSaved;
        _onBack = onBack;

        Title = task.Title;
        Description = task.Description;
        SelectedPriority = task.Priority;
        PlannedStart = task.PlannedStart.HasValue ? new DateTimeOffset(task.PlannedStart.Value) : null;
        PlannedEnd = task.PlannedEnd.HasValue ? new DateTimeOffset(task.PlannedEnd.Value) : null;
        SelectedAssignedAgent = UnassignedSentinel;

        _ = LoadBoardMembersAsync();
        _ = LoadBoardTasksAsync();
    }

    // The prerequisite picker offers the board's other tasks.
    private async Task LoadBoardTasksAsync()
    {
        var tasks = new Dictionary<string, KanbanTask>();
        foreach (var group in _board.Groups)
        foreach (var taskId in group.TaskIds)
        {
            var task = taskId == _task.Id ? _task : await _taskService.GetTaskAsync(_board.Id, taskId);
            if (task is not null) tasks[taskId] = task;
        }

        Planning = new TaskPlanningViewModel(_task, tasks, _dialogService, _board.IsArchived);
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
        Planning?.ApplyTo(_task);
        await _taskService.SaveTaskAsync(_task);
        _onSaved(_task);
    }

    [RelayCommand]
    private void GoBack() => _onBack();
}
