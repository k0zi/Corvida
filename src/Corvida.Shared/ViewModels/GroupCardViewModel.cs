using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using Corvida.Models;

namespace Corvida.ViewModels;

// Column header chrome only: name and delete-group. Tasks live on SwimlaneCellViewModel (one per
// lane for this column), and each of those cells has its own add-task button.
public partial class GroupCardViewModel : ViewModelBase
{
    private readonly Func<GroupCardViewModel, Task> _onDelete;

    public KanbanGroup Group { get; }
    public string GroupName => Group.Name;
    public bool IsReadOnly { get; }

    public GroupCardViewModel(
        KanbanGroup group,
        Func<GroupCardViewModel, Task> onDelete,
        bool isReadOnly)
    {
        Group = group;
        _onDelete = onDelete;
        IsReadOnly = isReadOnly;
    }

    [RelayCommand]
    private async Task DeleteGroup()
    {
        if (IsReadOnly) return;
        await _onDelete(this);
    }
}
