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

public partial class BoardEditorViewModel : ViewModelBase
{
    private readonly IBoardService _boardService;
    private readonly ITaskService _taskService;
    private readonly IDialogService _dialogService;
    private readonly Action _onBack;
    private readonly Action<KanbanTask> _onEditTask;

    public Board Board { get; }

    [ObservableProperty]
    private ObservableCollection<GroupCardViewModel> _groupCards = new();

    public BoardEditorViewModel(
        Board board,
        IBoardService boardService,
        ITaskService taskService,
        IDialogService dialogService,
        Action onBack,
        Action<KanbanTask> onEditTask)
    {
        Board = board;
        _boardService = boardService;
        _taskService = taskService;
        _dialogService = dialogService;
        _onBack = onBack;
        _onEditTask = onEditTask;
    }

    private GroupCardViewModel CreateCard(KanbanGroup group) =>
        new(group, Board, _boardService, _taskService, _dialogService,
            _onEditTask, DeleteGroupAsync, TransferTaskAsync);

    public async Task LoadAsync()
    {
        GroupCards.Clear();
        foreach (var group in Board.Groups)
        {
            var card = CreateCard(group);
            await card.LoadTasksAsync();
            GroupCards.Add(card);
        }
    }

    public void RefreshTask(KanbanTask updated)
    {
        foreach (var card in GroupCards)
            card.RefreshTask(updated);
    }

    public async Task MoveTaskAsync(KanbanTask task, GroupCardViewModel source, GroupCardViewModel target, int insertIndex)
    {
        if (source == target)
        {
            var currentIndex = source.Tasks.IndexOf(task);
            if (currentIndex < 0) return;

            var effectiveIndex = Math.Clamp(
                insertIndex > currentIndex ? insertIndex - 1 : insertIndex,
                0, source.Tasks.Count - 1);
            if (effectiveIndex == currentIndex) return;

            source.Tasks.Move(currentIndex, effectiveIndex);

            source.Group.TaskIds.Clear();
            foreach (var t in source.Tasks)
                source.Group.TaskIds.Add(t.Id);
        }
        else
        {
            source.Group.TaskIds.Remove(task.Id);
            task.GroupId = target.Group.Id;

            var clampedIndex = Math.Clamp(insertIndex, 0, target.Group.TaskIds.Count);
            target.Group.TaskIds.Insert(clampedIndex, task.Id);

            await _taskService.SaveTaskAsync(task);
            source.RemoveTask(task);
            target.InsertTask(task, clampedIndex);
        }

        await _boardService.SaveBoardAsync(Board);
    }

    private async Task TransferTaskAsync(KanbanTask task, GroupCardViewModel source)
    {
        var others = GroupCards.Where(c => c != source).ToList();
        if (others.Count == 0) return;

        var chosen = await _dialogService.ShowPickerDialogAsync(
            "Move to Group", others.Select(c => c.GroupName).ToList());
        if (chosen is null) return;

        var target = others.First(c => c.GroupName == chosen);

        source.Group.TaskIds.Remove(task.Id);
        target.Group.TaskIds.Add(task.Id);
        task.GroupId = target.Group.Id;

        await _taskService.SaveTaskAsync(task);
        await _boardService.SaveBoardAsync(Board);

        source.RemoveTask(task);
        target.AddTask(task);
    }

    [RelayCommand]
    private async Task AddGroup()
    {
        var name = await _dialogService.ShowInputDialogAsync("Add Group", "Group name:", "e.g. To-Do");
        if (name is null) return;

        var group = new KanbanGroup
        {
            Id = "grp-" + Guid.NewGuid().ToString("N")[..8],
            Name = name
        };

        Board.Groups.Add(group);
        await _boardService.SaveBoardAsync(Board);
        GroupCards.Add(CreateCard(group));
    }

    private async Task DeleteGroupAsync(GroupCardViewModel card)
    {
        var confirmed = await _dialogService.ShowConfirmDialogAsync(
            "Delete Group", $"Delete group '{card.GroupName}' and all its tasks?");
        if (!confirmed) return;

        foreach (var taskId in card.Group.TaskIds)
            await _taskService.DeleteTaskAsync(Board.Id, taskId);

        Board.Groups.Remove(card.Group);
        await _boardService.SaveBoardAsync(Board);
        GroupCards.Remove(card);
    }

    [RelayCommand]
    private void GoBack() => _onBack();
}
