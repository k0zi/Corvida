using System;
using System.Collections.ObjectModel;
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
        Func<KanbanTask, GroupCardViewModel, Task> onTransfer)
    {
        _group = group;
        _board = board;
        _boardService = boardService;
        _taskService = taskService;
        _dialogService = dialogService;
        _onEditTask = onEditTask;
        _onDelete = onDelete;
        _onTransfer = onTransfer;
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

    [RelayCommand]
    private async Task AddTask()
    {
        var title = await _dialogService.ShowInputDialogAsync("Add Task", "Task title:", "Enter task title");
        if (title is null) return;

        var task = new KanbanTask
        {
            Id = "task-" + Guid.NewGuid().ToString("N")[..8],
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
    private void EditTask(KanbanTask task) => _onEditTask(task);

    [RelayCommand]
    private async Task TransferTask(KanbanTask task) => await _onTransfer(task, this);

    [RelayCommand]
    private async Task DeleteTask(KanbanTask task)
    {
        var confirmed = await _dialogService.ShowConfirmDialogAsync(
            "Delete Task", $"Delete task '{task.Title}'?");
        if (!confirmed) return;

        await _taskService.DeleteTaskAsync(_board.Id, task.Id);
        _group.TaskIds.Remove(task.Id);
        await _boardService.SaveBoardAsync(_board);
        Tasks.Remove(task);
    }

    [RelayCommand]
    private async Task DeleteGroup() => await _onDelete(this);

    public KanbanGroup Group => _group;
}
