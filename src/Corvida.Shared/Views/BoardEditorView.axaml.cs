using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.VisualTree;
using Corvida.Models;
using Corvida.ViewModels;

namespace Corvida.Views;

public partial class BoardEditorView : UserControl
{
    private static readonly DataFormat<KanbanTask> DragTaskFormat =
        DataFormat.CreateInProcessFormat<KanbanTask>("corvida-task");
    private KanbanTask? _draggedTask;
    private SwimlaneCellViewModel? _dragSourceCell;

    public BoardEditorView() => InitializeComponent();

    private void Cell_Loaded(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control ctrl) return;
        ctrl.AddHandler(DragDrop.DragOverEvent, Cell_DragOver);
        ctrl.AddHandler(DragDrop.DropEvent, Cell_Drop);
    }

    private void Cell_DragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.DataTransfer.Contains(DragTaskFormat)
            ? DragDropEffects.Move
            : DragDropEffects.None;
        e.Handled = true;
    }

    private async void Cell_Drop(object? sender, DragEventArgs e)
    {
        if (!e.DataTransfer.Contains(DragTaskFormat) || _draggedTask is null || _dragSourceCell is null) return;
        if (sender is not Control ctrl || ctrl.DataContext is not SwimlaneCellViewModel targetCell) return;
        if (DataContext is not BoardEditorViewModel { IsReadOnly: false } vm) return;

        var insertIndex = FindInsertIndex(e.GetPosition(ctrl), ctrl, targetCell.Tasks);
        await vm.MoveTaskAsync(_draggedTask, _dragSourceCell, targetCell, insertIndex);
        e.Handled = true;
    }

    private async void TaskCard_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is BoardEditorViewModel { IsReadOnly: true }) return;
        if (!e.GetCurrentPoint(null).Properties.IsLeftButtonPressed) return;
        if (e.Source is Button || (e.Source as Control)?.FindAncestorOfType<Button>() is not null) return;
        if (sender is not Control ctrl || ctrl.DataContext is not KanbanTask task) return;

        var sourceCell = FindAncestorCellViewModel(ctrl);
        if (sourceCell is null) return;

        _draggedTask = task;
        _dragSourceCell = sourceCell;

        var item = DataTransferItem.Create(DragTaskFormat, task);
        var data = new DataTransfer();
        data.Add(item);
        await DragDrop.DoDragDropAsync(e, data, DragDropEffects.Move);

        _draggedTask = null;
        _dragSourceCell = null;
    }

    private void TaskCard_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if ((e.Source as Control)?.FindAncestorOfType<Button>() is not null) return;
        if (sender is not Control ctrl || ctrl.DataContext is not KanbanTask task) return;

        var cell = FindAncestorCellViewModel(ctrl);
        cell?.EditTaskCommand.Execute(task);
    }

    private static int FindInsertIndex(Point dropPositionInCard, Control card, ObservableCollection<KanbanTask> targetTasks)
    {
        if (targetTasks.Count == 0) return 0;

        var taskList = card.GetVisualDescendants()
            .OfType<ItemsControl>()
            .FirstOrDefault(ic => ReferenceEquals(ic.ItemsSource, targetTasks));

        if (taskList is null) return targetTasks.Count;

        var posInList = card.TranslatePoint(dropPositionInCard, taskList);
        if (posInList is null) return targetTasks.Count;

        var itemsPresenter = taskList.GetVisualDescendants().OfType<ItemsPresenter>().FirstOrDefault();
        var panel = itemsPresenter?.GetVisualChildren().OfType<Panel>().FirstOrDefault()
                    ?? taskList.GetVisualDescendants().OfType<Panel>().FirstOrDefault();

        if (panel is null) return targetTasks.Count;

        var posInPanel = taskList.TranslatePoint(posInList.Value, panel) ?? posInList.Value;

        for (var i = 0; i < panel.Children.Count; i++)
        {
            var child = panel.Children[i];
            if (posInPanel.Y <= child.Bounds.Top + child.Bounds.Height / 2)
                return i;
        }

        return targetTasks.Count;
    }

    private static SwimlaneCellViewModel? FindAncestorCellViewModel(Control? ctrl)
    {
        var parent = ctrl?.GetVisualParent();
        while (parent is not null)
        {
            if (parent is Control c && c.DataContext is SwimlaneCellViewModel cvm)
                return cvm;
            parent = parent.GetVisualParent();
        }
        return null;
    }
}
