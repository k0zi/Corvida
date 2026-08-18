using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Corvida.Messages;
using Corvida.Models;
using Corvida.Services;

namespace Corvida.ViewModels;

public partial class BoardsListViewModel : ViewModelBase,
    IRecipient<BoardChangedMessage>, IRecipient<BoardDeletedMessage>
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

        WeakReferenceMessenger.Default.RegisterAll(this);
    }

    public void Receive(BoardChangedMessage message)
    {
        var idx = Boards.ToList().FindIndex(b => b.Id == message.Board.Id);

        if (message.Board.IsArchived)
        {
            if (idx >= 0) Boards.RemoveAt(idx);
            return;
        }

        if (idx >= 0) Boards[idx] = message.Board; else Boards.Add(message.Board);
    }

    public void Receive(BoardDeletedMessage message)
    {
        var existing = Boards.FirstOrDefault(b => b.Id == message.BoardId);
        if (existing is not null) Boards.Remove(existing);
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
    private async Task ArchiveBoard(Board board)
    {
        var confirmed = await _dialogService.ShowConfirmDialogAsync(
            "Archive Board", $"Archive board '{board.Name}'? You can restore it later from Archived Boards.");
        if (!confirmed) return;

        await _boardService.ArchiveBoardAsync(board.Id);
        board.IsArchived = true;
        WeakReferenceMessenger.Default.Send(new BoardChangedMessage(board));
    }
}
