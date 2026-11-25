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

/// <summary>
/// The PERT side of one task: its three-point estimate and its prerequisites. It edits a copy of
/// that state; <see cref="ApplyTo"/> writes it back, so the task editor can apply it on Save while
/// the PERT page applies and saves on every <see cref="Changed"/>.
/// </summary>
public partial class TaskPlanningViewModel : ViewModelBase
{
    private readonly KanbanTask _task;
    private readonly IDialogService _dialogService;
    private IReadOnlyDictionary<string, KanbanTask> _boardTasks;
    private readonly List<string> _dependsOn;
    private bool _loading;

    // NumericUpDown works in decimals.
    [ObservableProperty] private decimal? _estimateOptimistic;
    [ObservableProperty] private decimal? _estimateMostLikely;
    [ObservableProperty] private decimal? _estimatePessimistic;

    public bool IsReadOnly { get; }

    public ObservableCollection<KanbanTask> Prerequisites { get; } = new();

    public string TaskTitle => _task.Title;

    public string ExpectedText
    {
        get
        {
            var probe = new KanbanTask();
            ApplyEstimates(probe);
            var (expected, variance, hasEstimate) = PertCalculator.Estimate(probe);
            if (!hasEstimate) return $"No estimate yet: counts as {PertMermaid.Days(PertCalculator.DefaultDuration)} day.";
            var spread = variance > 0 ? $" ± {PertMermaid.Days(Math.Sqrt(variance))}" : "";
            return $"Expected: {PertMermaid.Days(expected)}{spread} days";
        }
    }

    /// <summary>Raised after the user changed an estimate or a prerequisite.</summary>
    public event Action? Changed;

    public TaskPlanningViewModel(KanbanTask task, IReadOnlyDictionary<string, KanbanTask> boardTasks,
        IDialogService dialogService, bool isReadOnly = false)
    {
        _task = task;
        _boardTasks = boardTasks;
        _dialogService = dialogService;
        _dependsOn = task.DependsOn.ToList();
        IsReadOnly = isReadOnly;

        _loading = true;
        EstimateOptimistic = ToDecimal(task.EstimateOptimistic);
        EstimateMostLikely = ToDecimal(task.EstimateMostLikely);
        EstimatePessimistic = ToDecimal(task.EstimatePessimistic);
        _loading = false;

        RefreshPrerequisites();
    }

    public string TaskId => _task.Id;

    /// <summary>Takes a newer snapshot of the board's tasks, e.g. after another client changed one.</summary>
    public void SetBoardTasks(IReadOnlyDictionary<string, KanbanTask> boardTasks)
    {
        _boardTasks = boardTasks;
        RefreshPrerequisites();
    }

    /// <summary>Writes the estimates and the prerequisites that still exist on the board into <paramref name="task"/>.</summary>
    public void ApplyTo(KanbanTask task)
    {
        ApplyEstimates(task);
        task.DependsOn = _dependsOn.Where(_boardTasks.ContainsKey).ToList();
    }

    partial void OnEstimateOptimisticChanged(decimal? value) => OnEstimateChanged();
    partial void OnEstimateMostLikelyChanged(decimal? value) => OnEstimateChanged();
    partial void OnEstimatePessimisticChanged(decimal? value) => OnEstimateChanged();

    private void OnEstimateChanged()
    {
        OnPropertyChanged(nameof(ExpectedText));
        if (!_loading) Changed?.Invoke();
    }

    [RelayCommand]
    private async Task AddPrerequisite()
    {
        if (IsReadOnly) return;

        // Offering only what can be added keeps a cycle from ever being created here.
        var candidates = _boardTasks.Values
            .Where(t => t.Id != _task.Id && !_dependsOn.Contains(t.Id)
                        && !TaskDependencies.WouldCreateCycle(_boardTasks, _task.Id, t.Id))
            .OrderBy(t => t.Title, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        if (candidates.Count == 0)
        {
            await _dialogService.ShowMessageAsync("Add Prerequisite",
                "There is no other task on this board that this one could wait for.");
            return;
        }

        var labels = UniqueLabels(candidates);
        var chosen = await _dialogService.ShowPickerDialogAsync("Add Prerequisite", labels.Keys.ToList());
        if (chosen is null || !labels.TryGetValue(chosen, out var picked)) return;

        _dependsOn.Add(picked.Id);
        RefreshPrerequisites();
        Changed?.Invoke();
    }

    [RelayCommand]
    private void RemovePrerequisite(KanbanTask prerequisite)
    {
        if (IsReadOnly || !_dependsOn.Remove(prerequisite.Id)) return;
        RefreshPrerequisites();
        Changed?.Invoke();
    }

    private void RefreshPrerequisites()
    {
        Prerequisites.Clear();
        foreach (var id in _dependsOn)
            if (_boardTasks.TryGetValue(id, out var t)) Prerequisites.Add(t);
    }

    private void ApplyEstimates(KanbanTask task)
    {
        task.EstimateOptimistic = ToDouble(EstimateOptimistic);
        task.EstimateMostLikely = ToDouble(EstimateMostLikely);
        task.EstimatePessimistic = ToDouble(EstimatePessimistic);
    }

    // The picker returns the chosen label, so two tasks with the same title need telling apart.
    private static Dictionary<string, KanbanTask> UniqueLabels(IEnumerable<KanbanTask> tasks)
    {
        var result = new Dictionary<string, KanbanTask>();
        foreach (var group in tasks.GroupBy(t => t.Title))
        {
            if (group.Count() == 1)
            {
                result[group.Key] = group.First();
                continue;
            }
            foreach (var t in group)
                result[$"{t.Title} (#{t.Id[^Math.Min(6, t.Id.Length)..]})"] = t;
        }
        return result;
    }

    private static decimal? ToDecimal(double? value) => value is { } d && double.IsFinite(d) ? (decimal)d : null;
    private static double? ToDouble(decimal? value) => value is { } d ? (double)d : null;
}
