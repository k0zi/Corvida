using Avalonia.Controls;
using Corvida.Models;
using Corvida.ViewModels;
using KD.Avalonia.Rice.Controls;

namespace Corvida.Views;

public partial class BoardEditorView : UserControl
{
    public BoardEditorView() => InitializeComponent();

    // Every column of the board is a SwimlaneCellViewModel and every card a KanbanTask (see the
    // templates), so the board's containers carry them as their DataContext.
    private void Board_CardMoving(object? sender, RiceKanbanCardMovingEventArgs e)
    {
        if (DataContext is not BoardEditorViewModel { IsReadOnly: false } vm
            || e.Item is not KanbanTask task
            || e.SourceColumn.DataContext is not SwimlaneCellViewModel source
            || e.TargetColumn.DataContext is not SwimlaneCellViewModel target)
        {
            e.Cancel = true;
            return;
        }

        // The view model moves the task between the cells itself (synchronously, before it saves),
        // since a move also changes the task's group / assignee and the board's stored order.
        e.Handled = true;
        _ = vm.MoveTaskAsync(task, source, target, e.TargetIndex);
    }

    private void Board_CardActivated(object? sender, RiceKanbanCardEventArgs e)
    {
        if (e.Item is KanbanTask task && e.Column.DataContext is SwimlaneCellViewModel cell)
            cell.EditTaskCommand.Execute(task);
    }

    private void Board_AddCardRequested(object? sender, RiceKanbanColumnEventArgs e)
    {
        if (DataContext is BoardEditorViewModel vm && e.Column.DataContext is SwimlaneCellViewModel cell)
            _ = vm.AddTaskToCellAsync(cell);
    }
}
