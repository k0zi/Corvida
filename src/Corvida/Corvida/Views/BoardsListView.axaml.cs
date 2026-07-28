using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Corvida.Models;
using Corvida.ViewModels;

namespace Corvida.Views;

public partial class BoardsListView : UserControl
{
    public BoardsListView() => InitializeComponent();

    private void BoardCard_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if ((e.Source as Control)?.FindAncestorOfType<Button>() is not null) return;

        if (sender is Control { DataContext: Board board } &&
            DataContext is BoardsListViewModel vm)
            vm.EditBoardCommand.Execute(board);
    }
}
