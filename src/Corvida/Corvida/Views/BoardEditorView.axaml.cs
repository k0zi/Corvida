using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Corvida.Models;
using Corvida.ViewModels;

namespace Corvida.Views;

public partial class BoardEditorView : UserControl
{
    public BoardEditorView() => InitializeComponent();

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
}
