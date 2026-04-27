using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using Corvida.Models;
using Corvida.Services;
using Material.Icons;

namespace Corvida.ViewModels;

public partial class BoardsPageViewModel : PageBase
{
    private readonly IBoardService _boardService;
    private readonly ITaskService _taskService;
    private readonly IDialogService _dialogService;

    private readonly Stack<ViewModelBase> _navStack = new();

    [ObservableProperty]
    private ViewModelBase _currentViewModel = null!;

    public override string MenuTitle => "Boards";
    public override MaterialIconKind Icon => MaterialIconKind.ViewDashboard;
    public override int DisplayOrder => 0;

    public BoardsPageViewModel(IBoardService boardService, ITaskService taskService, IDialogService dialogService)
    {
        _boardService = boardService;
        _taskService = taskService;
        _dialogService = dialogService;

        var listVm = new BoardsListViewModel(boardService, dialogService, NavigateToBoardEditor);
        _navStack.Push(listVm);
        CurrentViewModel = listVm;

        _ = listVm.LoadAsync();
    }

    private void NavigateTo(ViewModelBase vm)
    {
        _navStack.Push(vm);
        CurrentViewModel = vm;
    }

    private void GoBack()
    {
        if (_navStack.Count <= 1) return;
        _navStack.Pop();
        CurrentViewModel = _navStack.Peek();
    }

    private void NavigateToBoardEditor(Board board)
    {
        var editorVm = new BoardEditorViewModel(
            board, _boardService, _taskService, _dialogService,
            onBack: GoBack,
            onEditTask: task => NavigateToTaskEditor(task, board));

        _ = editorVm.LoadAsync();
        NavigateTo(editorVm);
    }

    private void NavigateToTaskEditor(KanbanTask task, Board board)
    {
        var editorVm = new TaskEditorViewModel(
            task, board.Name, _taskService,
            onSaved: updated =>
            {
                var stack = _navStack.ToArray();
                foreach (var vm in stack)
                {
                    if (vm is BoardEditorViewModel boardEditor)
                    {
                        boardEditor.RefreshTask(updated);
                        break;
                    }
                }
                GoBack();
            },
            onBack: GoBack);

        NavigateTo(editorVm);
    }
}
