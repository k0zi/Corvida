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
using KD.Avalonia.Rice.Controls.Charts;
using KD.Avalonia.Rice.Controls.Markdowns;
using Material.Icons;

namespace Corvida.ViewModels;

/// <summary>One task of the schedule, formatted for the table and the phase columns.</summary>
public sealed class PertRowViewModel(PertNode node, bool isDone)
{
    public PertNode Node { get; } = node;
    public KanbanTask Task => Node.Task;
    public string Title => Task.Title;
    public int PhaseNumber => Node.Phase + 1;
    public bool IsCritical => Node.IsCritical;
    public bool IsDone { get; } = isDone;

    public string Estimates => $"{Format(Task.EstimateOptimistic)} / {Format(Task.EstimateMostLikely)} / {Format(Task.EstimatePessimistic)}";
    public string Expected => Node.HasEstimate ? PertMermaid.Days(Node.Expected) : $"{PertMermaid.Days(Node.Expected)}?";
    public string EarliestStart => PertMermaid.Days(Node.EarliestStart);
    public string EarliestFinish => PertMermaid.Days(Node.EarliestFinish);
    public string LatestStart => PertMermaid.Days(Node.LatestStart);
    public string LatestFinish => PertMermaid.Days(Node.LatestFinish);
    public string Slack => PertMermaid.Days(Node.Slack);

    public string Prerequisites => Node.Predecessors.Count == 0
        ? "No prerequisites"
        : "After " + string.Join(", ", Node.Predecessors.Select(p => p.Task.Title));

    private static string Format(double? value) => value is { } d ? PertMermaid.Days(d) : "–";
}

/// <summary>Tasks that do not depend on each other and can be worked on in parallel.</summary>
public sealed record PertPhaseViewModel(string Header, IReadOnlyList<PertRowViewModel> Tasks);

public partial class PertPageViewModel : PageBase,
    IRecipient<BoardChangedMessage>, IRecipient<BoardDeletedMessage>,
    IRecipient<TaskChangedMessage>, IRecipient<TaskDeletedMessage>
{
    private readonly IBoardService _boardService;
    private readonly ITaskService _taskService;
    private readonly IDialogService _dialogService;
    private readonly INotificationService _notifications;

    private Dictionary<string, KanbanTask> _tasks = new();
    private PertSchedule _schedule = new();
    private int _loadVersion;

    [ObservableProperty] private ObservableCollection<Board> _boards = new();
    [ObservableProperty] private Board? _selectedBoard;
    [ObservableProperty] private bool _isLoading;

    [ObservableProperty] private IReadOnlyList<PertRowViewModel> _rows = [];
    [ObservableProperty] private IReadOnlyList<PertPhaseViewModel> _phases = [];
    [ObservableProperty] private IList<GanttTask> _ganttTasks = new List<GanttTask>();
    [ObservableProperty] private string _mermaidSource = string.Empty;
    [ObservableProperty] private string _summary = string.Empty;
    [ObservableProperty] private string? _errorText;

    [ObservableProperty] private PertRowViewModel? _selectedRow;
    [ObservableProperty] private object? _selectedDiagramElement;
    [ObservableProperty] private TaskPlanningViewModel? _planning;

    public override string MenuTitle => "PERT";
    public override MaterialIconKind Icon => MaterialIconKind.Sitemap;
    public override int DisplayOrder => 5;

    public bool HasBoard => SelectedBoard is not null;
    public bool HasTasks => _tasks.Count > 0;
    public bool HasSchedule => HasTasks && ErrorText is null;
    public bool ShowNoBoards => !IsLoading && Boards.Count == 0;
    public bool ShowNoTasks => !IsLoading && HasBoard && !HasTasks;

    public PertPageViewModel(IBoardService boardService, ITaskService taskService, IDialogService dialogService,
        INotificationService notifications)
    {
        _boardService = boardService;
        _taskService = taskService;
        _dialogService = dialogService;
        _notifications = notifications;

        WeakReferenceMessenger.Default.RegisterAll(this);
    }

    /// <summary>Reloads the boards and the selected board's tasks. Called once signed in, and by the Refresh button.</summary>
    [RelayCommand]
    public async Task RefreshAsync()
    {
        var previous = SelectedBoard?.Id;
        try
        {
            Boards = new ObservableCollection<Board>(await _boardService.GetBoardsAsync());
        }
        catch (Exception ex)
        {
            _notifications.Error(ex.Message, "Could Not Load the Boards");
            return;
        }

        var board = Boards.FirstOrDefault(b => b.Id == previous) ?? Boards.FirstOrDefault();
        if (ReferenceEquals(board, SelectedBoard)) await LoadTasksAsync();
        else SelectedBoard = board;
        NotifyStateChanged();
    }

    partial void OnSelectedBoardChanged(Board? oldValue, Board? newValue)
    {
        if (oldValue?.Id != newValue?.Id)
        {
            SelectedRow = null;
            Planning = null;
        }
        _ = LoadTasksAsync();
    }

    partial void OnSelectedRowChanged(PertRowViewModel? value)
    {
        // A recalculation briefly leaves nothing selected; the task being edited stays open.
        if (value is null || Planning?.TaskId == value.Task.Id) return;
        Planning = CreatePlanning(value.Task);
    }

    partial void OnSelectedDiagramElementChanged(object? value)
    {
        if (value is not MermaidNode node) return;
        var taskId = PertMermaid.TaskIdOf(_schedule, node.Id);
        var row = Rows.FirstOrDefault(r => r.Task.Id == taskId);
        if (row is not null) SelectedRow = row;
    }

    [RelayCommand]
    private void SelectTask(PertRowViewModel row) => SelectedRow = row;

    public void Receive(BoardChangedMessage message)
    {
        var idx = Boards.ToList().FindIndex(b => b.Id == message.Board.Id);
        if (message.Board.IsArchived)
        {
            if (idx >= 0) RemoveBoardAt(idx);
            return;
        }

        if (idx >= 0) Boards[idx] = message.Board; else Boards.Add(message.Board);
        if (SelectedBoard?.Id == message.Board.Id || SelectedBoard is null) SelectedBoard = message.Board;
        NotifyStateChanged();
    }

    public void Receive(BoardDeletedMessage message)
    {
        var idx = Boards.ToList().FindIndex(b => b.Id == message.BoardId);
        if (idx >= 0) RemoveBoardAt(idx);
    }

    public void Receive(TaskChangedMessage message)
    {
        if (message.BoardId != SelectedBoard?.Id) return;
        _tasks[message.Task.Id] = message.Task;
        Recalculate();
    }

    public void Receive(TaskDeletedMessage message)
    {
        if (message.BoardId != SelectedBoard?.Id || !_tasks.Remove(message.TaskId)) return;
        TaskDependencies.RemoveReferences(_tasks.Values, message.TaskId);
        if (Planning?.TaskId == message.TaskId) Planning = null;
        Recalculate();
    }

    private void RemoveBoardAt(int idx)
    {
        var wasSelected = SelectedBoard?.Id == Boards[idx].Id;
        Boards.RemoveAt(idx);
        if (wasSelected) SelectedBoard = Boards.FirstOrDefault();
        NotifyStateChanged();
    }

    private async Task LoadTasksAsync()
    {
        var board = SelectedBoard;
        var version = ++_loadVersion;
        var tasks = new Dictionary<string, KanbanTask>();

        if (board is not null)
        {
            IsLoading = true;
            try
            {
                // There is no "list all tasks" API; the board's groups name every task.
                foreach (var group in board.Groups)
                foreach (var taskId in group.TaskIds)
                {
                    var task = await _taskService.GetTaskAsync(board.Id, taskId);
                    if (task is not null) tasks[taskId] = task;
                }
            }
            catch (Exception ex)
            {
                _notifications.Error(ex.Message, "Could Not Load the Tasks");
            }
            finally
            {
                if (version == _loadVersion) IsLoading = false;
            }
        }

        // The user picked another board while this one was loading.
        if (version != _loadVersion) return;

        _tasks = tasks;
        Recalculate();
    }

    private void Recalculate()
    {
        var doneGroupId = SelectedBoard is { } board ? TaskDependencies.FindDoneGroup(board)?.Id : null;
        _schedule = PertCalculator.Calculate(_tasks.Values.ToList());

        if (_schedule.HasCycle)
        {
            ErrorText = "These tasks wait for each other in a loop, so they cannot be put in order: "
                        + string.Join(", ", _schedule.Cycle.Select(t => t.Title))
                        + ". Remove one of their prerequisites.";
            Rows = [];
            Phases = [];
            GanttTasks = new List<GanttTask>();
            MermaidSource = string.Empty;
            Summary = string.Empty;
        }
        else
        {
            ErrorText = null;
            var rows = _schedule.Nodes.Select(n => new PertRowViewModel(n, TaskDependencies.IsDone(n.Task, doneGroupId))).ToList();
            Rows = rows;
            Phases = rows
                .GroupBy(r => r.PhaseNumber)
                .Select(g => new PertPhaseViewModel(
                    g.Count() == 1 ? $"Phase {g.Key}" : $"Phase {g.Key} · {g.Count()} in parallel", g.ToList()))
                .ToList();
            MermaidSource = PertMermaid.Build(_schedule, doneGroupId);
            GanttTasks = BuildGantt(_schedule, doneGroupId);
            Summary = rows.Count == 0 ? string.Empty : BuildSummary(_schedule);
        }

        if (Planning is not null)
        {
            if (_tasks.ContainsKey(Planning.TaskId)) Planning.SetBoardTasks(_tasks);
            else Planning = null;
        }
        SelectedRow = Rows.FirstOrDefault(r => r.Task.Id == Planning?.TaskId);
        NotifyStateChanged();
    }

    private TaskPlanningViewModel CreatePlanning(KanbanTask task)
    {
        var planning = new TaskPlanningViewModel(task, _tasks, _dialogService, SelectedBoard?.IsArchived == true);
        planning.Changed += () => _ = SavePlanningAsync(planning);
        return planning;
    }

    private async Task SavePlanningAsync(TaskPlanningViewModel planning)
    {
        if (SelectedBoard is not { } board || !_tasks.TryGetValue(planning.TaskId, out var task)) return;

        planning.ApplyTo(task);
        Recalculate();

        try
        {
            await _taskService.SaveTaskAsync(task);
        }
        catch (Exception ex)
        {
            _notifications.Error(ex.Message, "Could Not Save the Task");
            await LoadTasksAsync();
            return;
        }

        // Local storage has no server to broadcast the change, so tell the open board directly.
        WeakReferenceMessenger.Default.Send(new TaskChangedMessage(board.Id, task));
    }

    private void NotifyStateChanged()
    {
        OnPropertyChanged(nameof(HasBoard));
        OnPropertyChanged(nameof(HasTasks));
        OnPropertyChanged(nameof(HasSchedule));
        OnPropertyChanged(nameof(ShowNoBoards));
        OnPropertyChanged(nameof(ShowNoTasks));
    }

    partial void OnIsLoadingChanged(bool value) => NotifyStateChanged();

    private static string BuildSummary(PertSchedule schedule)
    {
        var parallel = schedule.Phases.Max(p => p.Count);
        var spread = schedule.ProjectStdDev > 0 ? $" ± {PertMermaid.Days(schedule.ProjectStdDev)}" : "";
        return $"{schedule.Nodes.Count} tasks in {schedule.Phases.Count} phases, up to {parallel} in parallel · "
               + $"expected duration {PertMermaid.Days(schedule.ProjectDuration)}{spread} days · "
               + $"critical path: {schedule.CriticalPath.Count} tasks";
    }

    // The timeline starts at the earliest planned start on the board, or today.
    private static List<GanttTask> BuildGantt(PertSchedule schedule, string? doneGroupId)
    {
        var start = schedule.Nodes
            .Where(n => n.Task.PlannedStart.HasValue)
            .Select(n => n.Task.PlannedStart!.Value.ToLocalTime().Date)
            .DefaultIfEmpty(DateTime.Today)
            .Min();

        // Gantt tasks refer to each other by name, so the names have to be unique.
        var names = new Dictionary<PertNode, string>();
        foreach (var group in schedule.Nodes.GroupBy(n => n.Task.Title))
        {
            var i = 0;
            foreach (var node in group)
                names[node] = ++i == 1 ? group.Key : $"{group.Key} ({i})";
        }

        return schedule.Nodes.Select(n => new GanttTask
        {
            Name = names[n],
            Group = $"Phase {n.Phase + 1}",
            Start = start.AddDays(n.EarliestStart),
            End = start.AddDays(n.EarliestFinish),
            Progress = TaskDependencies.IsDone(n.Task, doneGroupId) ? 1 : 0,
            // Only one arrow per bar: the predecessor that decides when this task can start.
            DependsOn = n.Predecessors.OrderByDescending(p => p.EarliestFinish).Select(p => names[p]).FirstOrDefault()!,
        }).ToList();
    }
}
