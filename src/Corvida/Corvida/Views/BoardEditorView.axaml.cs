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
    private const string DragTaskFormat = "corvida/task";
    private KanbanTask? _draggedTask;
    private GroupCardViewModel? _dragSourceGroup;

    public BoardEditorView() => InitializeComponent();

    private void GroupCard_Loaded(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control ctrl) return;
        ctrl.AddHandler(DragDrop.DragOverEvent, GroupCard_DragOver);
        ctrl.AddHandler(DragDrop.DropEvent, GroupCard_Drop);
    }

    private void GroupCard_DragOver(object? sender, DragEventArgs e)
    {
#pragma warning disable CS0618
        e.DragEffects = e.Data.Contains(DragTaskFormat)
            ? DragDropEffects.Move
            : DragDropEffects.None;
#pragma warning restore CS0618
        e.Handled = true;
    }

    private async void GroupCard_Drop(object? sender, DragEventArgs e)
    {
#pragma warning disable CS0618
        if (!e.Data.Contains(DragTaskFormat) || _draggedTask is null || _dragSourceGroup is null) return;
#pragma warning restore CS0618
        if (sender is not Control ctrl || ctrl.DataContext is not GroupCardViewModel targetGroup) return;
        if (DataContext is not BoardEditorViewModel vm) return;

        var insertIndex = FindInsertIndex(e.GetPosition(ctrl), ctrl, targetGroup);
        await vm.MoveTaskAsync(_draggedTask, _dragSourceGroup, targetGroup, insertIndex);
        e.Handled = true;
    }

    private async void TaskCard_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(null).Properties.IsLeftButtonPressed) return;
        if (e.Source is Button || (e.Source as Control)?.FindAncestorOfType<Button>() is not null) return;
        if (sender is not Control ctrl || ctrl.DataContext is not KanbanTask task) return;

        var sourceGroup = FindAncestorGroupViewModel(ctrl);
        if (sourceGroup is null) return;

        _draggedTask = task;
        _dragSourceGroup = sourceGroup;

#pragma warning disable CS0618
        var data = new DataObject();
        data.Set(DragTaskFormat, task);
        await DragDrop.DoDragDrop(e, data, DragDropEffects.Move);
#pragma warning restore CS0618

        _draggedTask = null;
        _dragSourceGroup = null;
    }

    private void TaskCard_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if ((e.Source as Control)?.FindAncestorOfType<Button>() is not null) return;
        if (sender is not Control ctrl || ctrl.DataContext is not KanbanTask task) return;

        var parent = ctrl.GetVisualParent();
        while (parent is not null)
        {
            if (parent is Control c && c.DataContext is GroupCardViewModel gvm)
            {
                gvm.EditTaskCommand.Execute(task);
                return;
            }
            parent = parent.GetVisualParent();
        }
    }

    private static int FindInsertIndex(Point dropPositionInCard, Control card, GroupCardViewModel targetGroup)
    {
        if (targetGroup.Tasks.Count == 0) return 0;

        var taskList = card.GetVisualDescendants()
            .OfType<ItemsControl>()
            .FirstOrDefault(ic => ReferenceEquals(ic.ItemsSource, targetGroup.Tasks));

        if (taskList is null) return targetGroup.Tasks.Count;

        var posInList = card.TranslatePoint(dropPositionInCard, taskList);
        if (posInList is null) return targetGroup.Tasks.Count;

        var itemsPresenter = taskList.GetVisualDescendants().OfType<ItemsPresenter>().FirstOrDefault();
        var panel = itemsPresenter?.GetVisualChildren().OfType<Panel>().FirstOrDefault()
                    ?? taskList.GetVisualDescendants().OfType<Panel>().FirstOrDefault();

        if (panel is null) return targetGroup.Tasks.Count;

        var posInPanel = taskList.TranslatePoint(posInList.Value, panel) ?? posInList.Value;

        for (var i = 0; i < panel.Children.Count; i++)
        {
            var child = panel.Children[i];
            if (posInPanel.Y <= child.Bounds.Top + child.Bounds.Height / 2)
                return i;
        }

        return targetGroup.Tasks.Count;
    }

    private static GroupCardViewModel? FindAncestorGroupViewModel(Control? ctrl)
    {
        var parent = ctrl?.GetVisualParent();
        while (parent is not null)
        {
            if (parent is Control c && c.DataContext is GroupCardViewModel gvm)
                return gvm;
            parent = parent.GetVisualParent();
        }
        return null;
    }
}