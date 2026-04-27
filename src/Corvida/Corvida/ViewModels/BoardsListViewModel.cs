using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Corvida.Models;
using Corvida.Services;

namespace Corvida.ViewModels;

public partial class BoardsListViewModel : ViewModelBase
{
    private readonly IBoardService _boardService;
    private readonly IDialogService _dialogService;
    private readonly Action<Board> _onEditBoard;

    [ObservableProperty]
    private ObservableCollection<Board> _boards = new();

    public BoardsListViewModel(IBoardService boardService, IDialogService dialogService, Action<Board> onEditBoard)
    {
        _boardService = boardService;
        _dialogService = dialogService;
        _onEditBoard = onEditBoard;
    }

    public async Task LoadAsync()
    {
        var boards = await _boardService.GetBoardsAsync();
        Boards = new ObservableCollection<Board>(boards);
    }

    [RelayCommand]
    private async Task CreateBoard()
    {
        var name = await _dialogService.ShowInputDialogAsync("Create Board", "Board name:", "Enter board name");
        if (name is null) return;

        var board = await _boardService.CreateBoardAsync(name);
        Boards.Add(board);
    }

    [RelayCommand]
    private void EditBoard(Board board) => _onEditBoard(board);

    [RelayCommand]
    private async Task DeleteBoard(Board board)
    {
        var confirmed = await _dialogService.ShowConfirmDialogAsync(
            "Delete Board", $"Delete board '{board.Name}'? This cannot be undone.");
        if (!confirmed) return;

        await _boardService.DeleteBoardAsync(board.Id);
        Boards.Remove(board);
    }
}
