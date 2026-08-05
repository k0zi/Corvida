using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Corvida.Models;

namespace Corvida.ViewModels;

// The atomic drop target / task collection at one (Group, Agent) intersection of the swimlane
// grid. AgentId is null for the Unassigned row. Dialogs and persistence for the actions below
// live on BoardEditorViewModel (via the injected delegates) since they need board-wide context
// (other groups, other rows) that a single cell doesn't have.
public partial class SwimlaneCellViewModel : ViewModelBase
{
    private readonly Action<KanbanTask> _onEditTask;
    private readonly Func<KanbanTask, SwimlaneCellViewModel, Task> _onTransferGroup;
    private readonly Func<KanbanTask, SwimlaneCellViewModel, Task> _onAssignAgent;
    private readonly Func<KanbanTask, SwimlaneCellViewModel, Task> _onDeleteTask;

    public KanbanGroup Group { get; }
    public string? AgentId { get; }
    public bool IsReadOnly { get; }

    [ObservableProperty]
    private ObservableCollection<KanbanTask> _tasks = new();

    public SwimlaneCellViewModel(
        KanbanGroup group,
        string? agentId,
        Action<KanbanTask> onEditTask,
        Func<KanbanTask, SwimlaneCellViewModel, Task> onTransferGroup,
        Func<KanbanTask, SwimlaneCellViewModel, Task> onAssignAgent,
        Func<KanbanTask, SwimlaneCellViewModel, Task> onDeleteTask,
        bool isReadOnly)
    {
        Group = group;
        AgentId = agentId;
        _onEditTask = onEditTask;
        _onTransferGroup = onTransferGroup;
        _onAssignAgent = onAssignAgent;
        _onDeleteTask = onDeleteTask;
        IsReadOnly = isReadOnly;
    }

    public void AddTask(KanbanTask task) => Tasks.Add(task);

    public void InsertTask(KanbanTask task, int index) => Tasks.Insert(Math.Clamp(index, 0, Tasks.Count), task);

    public void RemoveTask(KanbanTask task) => Tasks.Remove(task);

    public void RemoveTaskById(string taskId)
    {
        var existing = Tasks.FirstOrDefault(t => t.Id == taskId);
        if (existing is not null) Tasks.Remove(existing);
    }

    public void RefreshTask(KanbanTask updated)
    {
        var idx = Tasks.ToList().FindIndex(t => t.Id == updated.Id);
        if (idx >= 0) Tasks[idx] = updated;
    }

    public void UpsertTask(KanbanTask task)
    {
        var idx = Tasks.ToList().FindIndex(t => t.Id == task.Id);
        if (idx >= 0) Tasks[idx] = task; else Tasks.Add(task);
    }

    [RelayCommand]
    private void EditTask(KanbanTask task)
    {
        if (IsReadOnly) return;
        _onEditTask(task);
    }

    [RelayCommand]
    private async Task TransferTask(KanbanTask task)
    {
        if (IsReadOnly) return;
        await _onTransferGroup(task, this);
    }

    [RelayCommand]
    private async Task AssignAgent(KanbanTask task)
    {
        if (IsReadOnly) return;
        await _onAssignAgent(task, this);
    }

    [RelayCommand]
    private async Task DeleteTask(KanbanTask task)
    {
        if (IsReadOnly) return;
        await _onDeleteTask(task, this);
    }
}
