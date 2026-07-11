using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Corvida.Models;
using Corvida.Services;

namespace Corvida.ViewModels;

public partial class GroupCardViewModel : ViewModelBase
{
    private readonly IBoardService _boardService;
    private readonly ITaskService _taskService;
    private readonly IDialogService _dialogService;
    private readonly Board _board;
    private readonly KanbanGroup _group;
    private readonly Action<KanbanTask> _onEditTask;
    private readonly Func<GroupCardViewModel, Task> _onDelete;
    private readonly Func<KanbanTask, GroupCardViewModel, Task> _onTransfer;

    public string GroupName => _group.Name;
    public bool IsReadOnly { get; }

    [ObservableProperty]
    private ObservableCollection<KanbanTask> _tasks = new();

    public GroupCardViewModel(
        KanbanGroup group,
        Board board,
        IBoardService boardService,
        ITaskService taskService,
        IDialogService dialogService,
        Action<KanbanTask> onEditTask,
        Func<GroupCardViewModel, Task> onDelete,
        Func<KanbanTask, GroupCardViewModel, Task> onTransfer,
        bool isReadOnly = false)
    {
        _group = group;
        _board = board;
        _boardService = boardService;
        _taskService = taskService;
        _dialogService = dialogService;
        _onEditTask = onEditTask;
        _onDelete = onDelete;
        _onTransfer = onTransfer;
        IsReadOnly = isReadOnly;
    }

    public async Task LoadTasksAsync()
    {
        Tasks.Clear();
        foreach (var taskId in _group.TaskIds)
        {
            var task = await _taskService.GetTaskAsync(_board.Id, taskId);
            if (task is not null) Tasks.Add(task);
        }
    }

    public void RefreshTask(KanbanTask updated)
    {
        for (var i = 0; i < Tasks.Count; i++)
        {
            if (Tasks[i].Id == updated.Id)
            {
                Tasks[i] = updated;
                break;
            }
        }
    }

    public void AddTask(KanbanTask task) => Tasks.Add(task);

    public void InsertTask(KanbanTask task, int index) => Tasks.Insert(Math.Clamp(index, 0, Tasks.Count), task);

    public void RemoveTask(KanbanTask task) => Tasks.Remove(task);

    public void UpsertTask(KanbanTask task)
    {
        var idx = Tasks.ToList().FindIndex(t => t.Id == task.Id);
        if (idx >= 0) Tasks[idx] = task; else Tasks.Add(task);
        if (!_group.TaskIds.Contains(task.Id)) _group.TaskIds.Add(task.Id);
    }

    public void RemoveTaskById(string taskId)
    {
        var existing = Tasks.FirstOrDefault(t => t.Id == taskId);
        if (existing is not null) Tasks.Remove(existing);
        _group.TaskIds.Remove(taskId);
    }

    [RelayCommand]
    private async Task AddTask()
    {
        if (IsReadOnly) return;

        var title = await _dialogService.ShowInputDialogAsync("Add Task", "Task title:", "Enter task title");
        if (title is null) return;

        var task = new KanbanTask
        {
            Id = title + "-task-" + Guid.NewGuid().ToString("N")[..8],
            Title = title,
            GroupId = _group.Id,
            BoardId = _board.Id,
            Created = DateTime.UtcNow
        };

        await _taskService.SaveTaskAsync(task);
        _group.TaskIds.Add(task.Id);
        await _boardService.SaveBoardAsync(_board);
        Tasks.Add(task);
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
        await _onTransfer(task, this);
    }

    [RelayCommand]
    private async Task DeleteTask(KanbanTask task)
    {
        if (IsReadOnly) return;

        var confirmed = await _dialogService.ShowConfirmDialogAsync(
            "Delete Task", $"Delete task '{task.Title}'?");
        if (!confirmed) return;

        await _taskService.DeleteTaskAsync(_board.Id, task.Id);
        _group.TaskIds.Remove(task.Id);
        await _boardService.SaveBoardAsync(_board);
        Tasks.Remove(task);
    }

    [RelayCommand]
    private async Task DeleteGroup()
    {
        if (IsReadOnly) return;
        await _onDelete(this);
    }

    public KanbanGroup Group => _group;
}
