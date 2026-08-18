using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using Corvida.Models;

namespace Corvida.ViewModels;

// Column chrome only: name, add-task, delete-group. Task collections/actions now live on
// SwimlaneCellViewModel (one per row for this column) since a column can hold multiple rows.
public partial class GroupCardViewModel : ViewModelBase
{
    private readonly Func<GroupCardViewModel, Task> _onDelete;
    private readonly Func<KanbanGroup, Task> _onAddTask;

    public KanbanGroup Group { get; }
    public string GroupName => Group.Name;
    public bool IsReadOnly { get; }

    public GroupCardViewModel(
        KanbanGroup group,
        Func<GroupCardViewModel, Task> onDelete,
        Func<KanbanGroup, Task> onAddTask,
        bool isReadOnly)
    {
        Group = group;
        _onDelete = onDelete;
        _onAddTask = onAddTask;
        IsReadOnly = isReadOnly;
    }

    [RelayCommand]
    private async Task AddTask()
    {
        if (IsReadOnly) return;
        await _onAddTask(Group);
    }

    [RelayCommand]
    private async Task DeleteGroup()
    {
        if (IsReadOnly) return;
        await _onDelete(this);
    }
}
