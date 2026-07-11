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
using Material.Icons;

namespace Corvida.ViewModels;

public partial class ArchivedBoardsViewModel : PageBase,
    IRecipient<BoardChangedMessage>, IRecipient<BoardDeletedMessage>
{
    private readonly IBoardService _boardService;
    private readonly IDialogService _dialogService;
    private Action<Board>? _onViewBoard;

    [ObservableProperty]
    private ObservableCollection<Board> _boards = new();

    public override string MenuTitle => "Archived Boards";
    public override MaterialIconKind Icon => MaterialIconKind.Archive;
    public override int DisplayOrder => 50;

    public ArchivedBoardsViewModel(IBoardService boardService, IDialogService dialogService)
    {
        _boardService = boardService;
        _dialogService = dialogService;

        WeakReferenceMessenger.Default.RegisterAll(this);

        _ = LoadAsync();
    }

    public void SetOnViewBoard(Action<Board> onViewBoard) => _onViewBoard = onViewBoard;

    public void Receive(BoardChangedMessage message)
    {
        var idx = Boards.ToList().FindIndex(b => b.Id == message.Board.Id);

        if (!message.Board.IsArchived)
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
        var boards = await _boardService.GetArchivedBoardsAsync();
        Boards = new ObservableCollection<Board>(boards);
    }

    [RelayCommand]
    private void ViewBoard(Board board) => _onViewBoard?.Invoke(board);

    [RelayCommand]
    private async Task RestoreBoard(Board board)
    {
        var confirmed = await _dialogService.ShowConfirmDialogAsync(
            "Restore Board", $"Restore board '{board.Name}' back to your live boards?");
        if (!confirmed) return;

        await _boardService.RestoreBoardAsync(board.Id);
        Boards.Remove(board);
    }

    [RelayCommand]
    private async Task DeleteBoard(Board board)
    {
        var confirmed = await _dialogService.ShowConfirmDialogAsync(
            "Delete Board", $"Permanently delete board '{board.Name}'? This cannot be undone.");
        if (!confirmed) return;

        await _boardService.DeleteBoardAsync(board.Id);
        Boards.Remove(board);
    }
}
