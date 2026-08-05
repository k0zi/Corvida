using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Corvida.Messages;
using Corvida.Models;
using Corvida.Services;

namespace Corvida.ViewModels;

public partial class BoardEditorViewModel : ViewModelBase,
    IRecipient<BoardChangedMessage>, IRecipient<BoardDeletedMessage>,
    IRecipient<TaskChangedMessage>, IRecipient<TaskDeletedMessage>,
    IRecipient<AgentChangedMessage>, IRecipient<AgentDeletedMessage>
{
    private readonly IBoardService _boardService;
    private readonly ITaskService _taskService;
    private readonly IAgentService _agentService;
    private readonly IDialogService _dialogService;
    private readonly Action _onBack;
    private readonly Action<KanbanTask> _onEditTask;

    public Board Board { get; }

    // Column chrome (name, add-task, delete-group). Rows below carry the actual tasks —
    // GroupCards[i] and every Rows[r].Cells[i] refer to the same column.
    [ObservableProperty]
    private ObservableCollection<GroupCardViewModel> _groupCards = new();

    [ObservableProperty]
    private ObservableCollection<SwimlaneRowViewModel> _rows = new();

    [ObservableProperty]
    private bool _isReadOnly;

    public BoardEditorViewModel(
        Board board,
        IBoardService boardService,
        ITaskService taskService,
        IAgentService agentService,
        IDialogService dialogService,
        Action onBack,
        Action<KanbanTask> onEditTask)
    {
        Board = board;
        _boardService = boardService;
        _taskService = taskService;
        _agentService = agentService;
        _dialogService = dialogService;
        _onBack = onBack;
        _onEditTask = onEditTask;
        _isReadOnly = board.IsArchived;

        WeakReferenceMessenger.Default.RegisterAll(this);
    }

    public void Receive(BoardChangedMessage message)
    {
        if (message.Board.Id != Board.Id) return;

        Board.Name = message.Board.Name;
        Board.Groups.Clear();
        Board.Groups.AddRange(message.Board.Groups);
        Board.AgentIds.Clear();
        Board.AgentIds.AddRange(message.Board.AgentIds);
        Board.CellOrders.Clear();
        Board.CellOrders.AddRange(message.Board.CellOrders);
        Board.IsArchived = message.Board.IsArchived;
        IsReadOnly = message.Board.IsArchived;
        _ = LoadAsync();
    }

    public void Receive(BoardDeletedMessage message)
    {
        if (message.BoardId != Board.Id) return;
        _onBack();
    }

    public void Receive(TaskChangedMessage message)
    {
        if (message.BoardId != Board.Id) return;

        foreach (var row in Rows)
        foreach (var cell in row.Cells)
        {
            if (cell.Group.Id == message.Task.GroupId && cell.AgentId == message.Task.AssignedAgentId)
                cell.UpsertTask(message.Task);
            else
                cell.RemoveTaskById(message.Task.Id);
        }
    }

    public void Receive(TaskDeletedMessage message)
    {
        if (message.BoardId != Board.Id) return;

        foreach (var row in Rows)
        foreach (var cell in row.Cells)
            cell.RemoveTaskById(message.TaskId);
    }

    public void Receive(AgentChangedMessage message)
    {
        if (!Board.AgentIds.Contains(message.Agent.Id)) return;
        _ = LoadAsync();
    }

    public void Receive(AgentDeletedMessage message)
    {
        if (!Board.AgentIds.Remove(message.AgentId)) return;
        _ = LoadAsync();
    }

    private GroupCardViewModel CreateGroupCard(KanbanGroup group) =>
        new(group, DeleteGroupAsync, AddTaskToGroupAsync, IsReadOnly);

    private SwimlaneCellViewModel CreateCell(KanbanGroup group, string? agentId) =>
        new(group, agentId, _onEditTask, TransferTaskAsync, AssignAgentAsync, DeleteTaskAsync, IsReadOnly);

    private SwimlaneRowViewModel CreateRow(Agent? agent, string? agentId)
    {
        var row = new SwimlaneRowViewModel(agent, agentId);
        foreach (var group in Board.Groups)
            row.Cells.Add(CreateCell(group, agentId));
        return row;
    }

    public async Task LoadAsync()
    {
        GroupCards.Clear();
        foreach (var group in Board.Groups)
            GroupCards.Add(CreateGroupCard(group));

        Rows.Clear();
        Rows.Add(CreateRow(null, null));
        foreach (var agentId in Board.AgentIds)
        {
            var agent = await _agentService.GetAgentAsync(agentId);
            Rows.Add(CreateRow(agent, agentId));
        }

        // There is no "list all tasks" API — enumerate the same way the app always has,
        // via each group's TaskIds — then place each task into its cell.
        var tasksById = new Dictionary<string, KanbanTask>();
        foreach (var group in Board.Groups)
        foreach (var taskId in group.TaskIds)
        {
            var task = await _taskService.GetTaskAsync(Board.Id, taskId);
            if (task is not null) tasksById[taskId] = task;
        }

        var placed = new HashSet<string>();
        var boardChanged = false;

        // Placement pass 1: honor persisted per-cell order so drag-reorder survives reload.
        foreach (var cellOrder in Board.CellOrders.ToList())
        {
            var colIndex = GroupCards.ToList().FindIndex(c => c.Group.Id == cellOrder.GroupId);
            var row = Rows.FirstOrDefault(r => r.AgentId == cellOrder.AgentId);
            if (colIndex < 0 || row is null)
            {
                Board.CellOrders.Remove(cellOrder);
                boardChanged = true;
                continue;
            }

            foreach (var taskId in cellOrder.TaskIds.ToList())
            {
                if (!tasksById.TryGetValue(taskId, out var task))
                {
                    cellOrder.TaskIds.Remove(taskId);
                    boardChanged = true;
                    continue;
                }

                // If the task's real GroupId/AssignedAgentId drifted from this entry (e.g. edited
                // via the Task Editor, or reassigned through a path that bypasses MoveTaskAsync),
                // drop it here — pass 2 below re-files it into the correct cell.
                if (task.GroupId != cellOrder.GroupId || task.AssignedAgentId != cellOrder.AgentId)
                {
                    cellOrder.TaskIds.Remove(taskId);
                    boardChanged = true;
                    continue;
                }

                row.Cells[colIndex].AddTask(task);
                placed.Add(taskId);
            }

            if (cellOrder.TaskIds.Count == 0)
            {
                Board.CellOrders.Remove(cellOrder);
                boardChanged = true;
            }
        }

        // Placement pass 2 (self-heal): anything not covered above — new tasks, legacy boards,
        // or drift — falls back to its group's row-matching-AssignedAgentId (or Unassigned).
        var unassignedRow = Rows.First(r => r.AgentId is null);
        foreach (var group in Board.Groups)
        {
            var colIndex = GroupCards.ToList().FindIndex(c => c.Group.Id == group.Id);
            foreach (var taskId in group.TaskIds)
            {
                if (placed.Contains(taskId)) continue;
                if (!tasksById.TryGetValue(taskId, out var task)) continue;

                var row = Rows.FirstOrDefault(r => r.AgentId == task.AssignedAgentId) ?? unassignedRow;
                row.Cells[colIndex].AddTask(task);
                RelocateInCellOrders(Board, taskId, group.Id, row.AgentId);
                boardChanged = true;
                placed.Add(taskId);
            }
        }

        if (boardChanged && !IsReadOnly)
            await _boardService.SaveBoardAsync(Board);
    }

    public void RefreshTask(KanbanTask updated)
    {
        // The Task Editor can change GroupId/AssignedAgentId directly (not just via drag/drop),
        // so a refresh must relocate the task to its correct cell, not just patch it in place.
        SwimlaneCellViewModel? currentCell = null;
        foreach (var row in Rows)
        foreach (var cell in row.Cells)
            if (cell.Tasks.Any(t => t.Id == updated.Id)) { currentCell = cell; break; }

        var targetColIndex = GroupCards.ToList().FindIndex(c => c.Group.Id == updated.GroupId);
        if (targetColIndex < 0)
        {
            currentCell?.RemoveTaskById(updated.Id);
            return;
        }

        var targetRow = Rows.FirstOrDefault(r => r.AgentId == updated.AssignedAgentId)
            ?? Rows.First(r => r.AgentId is null);
        var targetCell = targetRow.Cells[targetColIndex];

        if (currentCell == targetCell)
        {
            currentCell.RefreshTask(updated);
            return;
        }

        currentCell?.RemoveTaskById(updated.Id);
        targetCell.AddTask(updated);

        RelocateInCellOrders(Board, updated.Id, updated.GroupId, updated.AssignedAgentId);
        _ = _boardService.SaveBoardAsync(Board);
    }

    public async Task MoveTaskAsync(KanbanTask task, SwimlaneCellViewModel source, SwimlaneCellViewModel target, int insertIndex)
    {
        if (IsReadOnly) return;

        if (source == target)
        {
            var currentIndex = source.Tasks.IndexOf(task);
            if (currentIndex < 0) return;

            var effectiveIndex = Math.Clamp(
                insertIndex > currentIndex ? insertIndex - 1 : insertIndex,
                0, source.Tasks.Count - 1);
            if (effectiveIndex == currentIndex) return;

            source.Tasks.Move(currentIndex, effectiveIndex);
            SetCellOrder(Board, source.Group.Id, source.AgentId, source.Tasks.Select(t => t.Id).ToList());
            await _boardService.SaveBoardAsync(Board);
            return;
        }

        var groupChanged = source.Group.Id != target.Group.Id;
        var agentChanged = source.AgentId != target.AgentId;

        if (groupChanged)
        {
            source.Group.TaskIds.Remove(task.Id);
            task.GroupId = target.Group.Id;
            if (!target.Group.TaskIds.Contains(task.Id))
                target.Group.TaskIds.Add(task.Id);
        }
        if (agentChanged)
        {
            task.AssignedAgentId = target.AgentId;
        }

        RelocateInCellOrders(Board, task.Id, target.Group.Id, target.AgentId, insertIndex);

        await _taskService.SaveTaskAsync(task);
        source.RemoveTask(task);
        target.InsertTask(task, insertIndex);

        await _boardService.SaveBoardAsync(Board);
    }

    private async Task TransferTaskAsync(KanbanTask task, SwimlaneCellViewModel source)
    {
        if (IsReadOnly) return;

        var (row, _) = LocateCell(source);
        var otherGroups = GroupCards.Where(c => c.Group.Id != source.Group.Id).ToList();
        if (otherGroups.Count == 0) return;

        var chosen = await _dialogService.ShowPickerDialogAsync(
            "Move to Group", otherGroups.Select(c => c.GroupName).ToList());
        if (chosen is null) return;

        var targetGroupCard = otherGroups.First(c => c.GroupName == chosen);
        var targetColIndex = GroupCards.IndexOf(targetGroupCard);
        var target = row.Cells[targetColIndex];

        await MoveTaskAsync(task, source, target, target.Tasks.Count);
    }

    private async Task AssignAgentAsync(KanbanTask task, SwimlaneCellViewModel source)
    {
        if (IsReadOnly) return;

        var (sourceRow, colIndex) = LocateCell(source);
        var otherRows = Rows.Where(r => r != sourceRow).ToList();
        if (otherRows.Count == 0) return;

        var chosen = await _dialogService.ShowPickerDialogAsync(
            "Assign to Agent", otherRows.Select(r => r.DisplayName).ToList());
        if (chosen is null) return;

        var targetRow = otherRows.First(r => r.DisplayName == chosen);
        var target = targetRow.Cells[colIndex];

        await MoveTaskAsync(task, source, target, target.Tasks.Count);
    }

    private async Task DeleteTaskAsync(KanbanTask task, SwimlaneCellViewModel source)
    {
        if (IsReadOnly) return;

        var confirmed = await _dialogService.ShowConfirmDialogAsync(
            "Delete Task", $"Delete task '{task.Title}'?");
        if (!confirmed) return;

        await _taskService.DeleteTaskAsync(Board.Id, task.Id);
        source.Group.TaskIds.Remove(task.Id);
        RemoveFromCellOrders(Board, task.Id);
        await _boardService.SaveBoardAsync(Board);
        source.RemoveTask(task);
    }

    private async Task AddTaskToGroupAsync(KanbanGroup group)
    {
        if (IsReadOnly) return;

        var title = await _dialogService.ShowInputDialogAsync("Add Task", "Task title:", "Enter task title");
        if (title is null) return;

        var task = new KanbanTask
        {
            Id = title + "-task-" + Guid.NewGuid().ToString("N")[..8],
            Title = title,
            GroupId = group.Id,
            BoardId = Board.Id,
            Created = DateTime.UtcNow,
        };

        await _taskService.SaveTaskAsync(task);
        group.TaskIds.Add(task.Id);
        RelocateInCellOrders(Board, task.Id, group.Id, null);
        await _boardService.SaveBoardAsync(Board);

        var colIndex = GroupCards.ToList().FindIndex(c => c.Group.Id == group.Id);
        var unassignedRow = Rows.First(r => r.AgentId is null);
        unassignedRow.Cells[colIndex].AddTask(task);
    }

    [RelayCommand]
    private async Task AddGroup()
    {
        if (IsReadOnly) return;

        var name = await _dialogService.ShowInputDialogAsync("Add Group", "Group name:", "e.g. To-Do");
        if (name is null) return;

        var group = new KanbanGroup
        {
            Id = "grp-" + Guid.NewGuid().ToString("N")[..8],
            Name = name
        };

        Board.Groups.Add(group);
        await _boardService.SaveBoardAsync(Board);

        GroupCards.Add(CreateGroupCard(group));
        foreach (var row in Rows)
            row.Cells.Add(CreateCell(group, row.AgentId));
    }

    private async Task DeleteGroupAsync(GroupCardViewModel card)
    {
        if (IsReadOnly) return;

        var confirmed = await _dialogService.ShowConfirmDialogAsync(
            "Delete Group", $"Delete group '{card.GroupName}' and all its tasks?");
        if (!confirmed) return;

        foreach (var taskId in card.Group.TaskIds)
            await _taskService.DeleteTaskAsync(Board.Id, taskId);

        Board.Groups.Remove(card.Group);
        Board.CellOrders.RemoveAll(c => c.GroupId == card.Group.Id);
        await _boardService.SaveBoardAsync(Board);

        var colIndex = GroupCards.IndexOf(card);
        GroupCards.Remove(card);
        foreach (var row in Rows)
            if (colIndex >= 0 && colIndex < row.Cells.Count)
                row.Cells.RemoveAt(colIndex);
    }

    [RelayCommand]
    private void GoBack() => _onBack();

    [RelayCommand]
    private async Task ManageMembers()
    {
        if (IsReadOnly) return;

        var allAgents = await _agentService.GetAgentsAsync();
        var result = await _dialogService.ShowBoardMembersDialogAsync(allAgents, Board.AgentIds);
        if (result is null) return;

        Board.AgentIds = result;
        await _boardService.SaveBoardAsync(Board);
        await LoadAsync();
    }

    // XAML previewer data: `d:DataContext="{x:Static vm:BoardEditorViewModel.DesignInstance}"` in
    // BoardEditorView.axaml. Services/callbacks are stubbed since the previewer never invokes commands.
    public static BoardEditorViewModel DesignInstance { get; } = CreateDesignInstance();

    private static BoardEditorViewModel CreateDesignInstance()
    {
        var board = new Board { Id = "design-board", Name = "Sample Board" };
        var todo = new KanbanGroup { Id = "grp-todo", Name = "To Do" };
        var inProgress = new KanbanGroup { Id = "grp-in-progress", Name = "In Progress" };
        var done = new KanbanGroup { Id = "grp-done", Name = "Done" };
        board.Groups.AddRange([todo, inProgress, done]);
        board.AgentIds.Add("agent-1");

        var agent = new Agent { Id = "agent-1", Name = "Ada", Color = "#4C6EF5" };

        var vm = new BoardEditorViewModel(
            board,
            boardService: null!,
            taskService: null!,
            agentService: null!,
            dialogService: null!,
            onBack: () => { },
            onEditTask: _ => { });

        vm.GroupCards.Add(new GroupCardViewModel(todo, _ => Task.CompletedTask, _ => Task.CompletedTask, false));
        vm.GroupCards.Add(new GroupCardViewModel(inProgress, _ => Task.CompletedTask, _ => Task.CompletedTask, false));
        vm.GroupCards.Add(new GroupCardViewModel(done, _ => Task.CompletedTask, _ => Task.CompletedTask, false));

        static SwimlaneCellViewModel MakeCell(KanbanGroup group, string? agentId) => new(
            group, agentId,
            onEditTask: _ => { },
            onTransferGroup: (_, _) => Task.CompletedTask,
            onAssignAgent: (_, _) => Task.CompletedTask,
            onDeleteTask: (_, _) => Task.CompletedTask,
            isReadOnly: false);

        var unassignedRow = new SwimlaneRowViewModel(null, null);
        unassignedRow.Cells.Add(MakeCell(todo, null));
        unassignedRow.Cells.Add(MakeCell(inProgress, null));
        unassignedRow.Cells.Add(MakeCell(done, null));
        unassignedRow.Cells[0].AddTask(new KanbanTask
        {
            Id = "t1", Title = "Write onboarding doc", Priority = "Low", GroupId = todo.Id, BoardId = board.Id,
        });

        var adaRow = new SwimlaneRowViewModel(agent, agent.Id);
        adaRow.Cells.Add(MakeCell(todo, agent.Id));
        adaRow.Cells.Add(MakeCell(inProgress, agent.Id));
        adaRow.Cells.Add(MakeCell(done, agent.Id));
        adaRow.Cells[0].AddTask(new KanbanTask
        {
            Id = "t2", Title = "Fix login bug", Priority = "High", GroupId = todo.Id, BoardId = board.Id, AssignedAgentId = agent.Id,
        });
        adaRow.Cells[1].AddTask(new KanbanTask
        {
            Id = "t3", Title = "Design skill editor", Priority = "Medium", GroupId = inProgress.Id, BoardId = board.Id, AssignedAgentId = agent.Id,
        });
        adaRow.Cells[2].AddTask(new KanbanTask
        {
            Id = "t4", Title = "Ship v1.2", Priority = "High", GroupId = done.Id, BoardId = board.Id, AssignedAgentId = agent.Id,
        });

        vm.Rows.Add(unassignedRow);
        vm.Rows.Add(adaRow);

        return vm;
    }

    private (SwimlaneRowViewModel Row, int ColumnIndex) LocateCell(SwimlaneCellViewModel cell)
    {
        var row = Rows.First(r => r.Cells.Contains(cell));
        return (row, row.Cells.IndexOf(cell));
    }

    // The single source of truth for "which cell (group, agent) a task visually sits in" is
    // Board.CellOrders. Every mutation that moves a task between cells goes through this (or
    // RemoveFromCellOrders/SetCellOrder below) instead of duplicating the removal/insertion logic.
    private static void RelocateInCellOrders(Board board, string taskId, string groupId, string? agentId, int insertIndex = int.MaxValue)
    {
        foreach (var cell in board.CellOrders.ToList())
            if (cell.TaskIds.Remove(taskId) && cell.TaskIds.Count == 0)
                board.CellOrders.Remove(cell);

        var target = board.CellOrders.FirstOrDefault(c => c.GroupId == groupId && c.AgentId == agentId);
        if (target is null)
        {
            target = new SwimlaneCellOrder { GroupId = groupId, AgentId = agentId };
            board.CellOrders.Add(target);
        }

        var clamped = Math.Clamp(insertIndex, 0, target.TaskIds.Count);
        if (!target.TaskIds.Contains(taskId))
            target.TaskIds.Insert(clamped, taskId);
    }

    private static void RemoveFromCellOrders(Board board, string taskId)
    {
        foreach (var cell in board.CellOrders.ToList())
            if (cell.TaskIds.Remove(taskId) && cell.TaskIds.Count == 0)
                board.CellOrders.Remove(cell);
    }

    private static void SetCellOrder(Board board, string groupId, string? agentId, List<string> taskIds)
    {
        var entry = board.CellOrders.FirstOrDefault(c => c.GroupId == groupId && c.AgentId == agentId);
        if (taskIds.Count == 0)
        {
            if (entry is not null) board.CellOrders.Remove(entry);
            return;
        }

        if (entry is null)
        {
            entry = new SwimlaneCellOrder { GroupId = groupId, AgentId = agentId };
            board.CellOrders.Add(entry);
        }

        entry.TaskIds = taskIds;
    }
}
