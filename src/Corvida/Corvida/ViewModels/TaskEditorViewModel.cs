using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Corvida.Models;
using Corvida.Services;

namespace Corvida.ViewModels;

public partial class TaskEditorViewModel : ViewModelBase
{
    private readonly ITaskService _taskService;
    private readonly Action<KanbanTask> _onSaved;
    private readonly Action _onBack;

    private readonly KanbanTask _task;

    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private string _selectedPriority = "Medium";
    [ObservableProperty] private DateTimeOffset? _plannedStart;
    [ObservableProperty] private DateTimeOffset? _plannedEnd;

    public IReadOnlyList<string> Priorities { get; } = new[] { "Low", "Medium", "High" };

    public string BoardName { get; }

    public TaskEditorViewModel(KanbanTask task, string boardName, ITaskService taskService,
        Action<KanbanTask> onSaved, Action onBack)
    {
        _task = task;
        BoardName = boardName;
        _taskService = taskService;
        _onSaved = onSaved;
        _onBack = onBack;

        Title = task.Title;
        Description = task.Description;
        SelectedPriority = task.Priority;
        PlannedStart = task.PlannedStart.HasValue ? new DateTimeOffset(task.PlannedStart.Value) : null;
        PlannedEnd = task.PlannedEnd.HasValue ? new DateTimeOffset(task.PlannedEnd.Value) : null;
    }

    [RelayCommand]
    private async Task Save()
    {
        _task.Title = Title.Trim();
        _task.Description = Description;
        _task.Priority = SelectedPriority;
        _task.PlannedStart = PlannedStart?.DateTime;
        _task.PlannedEnd = PlannedEnd?.DateTime;
        await _taskService.SaveTaskAsync(_task);
        _onSaved(_task);
    }

    [RelayCommand]
    private void GoBack() => _onBack();
}
