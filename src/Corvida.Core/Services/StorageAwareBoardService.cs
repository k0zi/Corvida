using System.Collections.Generic;
using System.Threading.Tasks;
using Corvida.Models;

namespace Corvida.Services;

public class StorageAwareBoardService(
    BoardService local,
    HttpBoardService http,
    ISettingsService settings) : IBoardService
{
    private IBoardService Active =>
        settings.Settings.StorageMode == StorageMode.ServerHosted ? http : local;

    public Task<List<Board>> GetBoardsAsync()         => Active.GetBoardsAsync();
    public Task<List<Board>> GetArchivedBoardsAsync() => Active.GetArchivedBoardsAsync();
    public Task<Board> CreateBoardAsync(string name)  => Active.CreateBoardAsync(name);
    public Task SaveBoardAsync(Board board)           => Active.SaveBoardAsync(board);
    public Task ArchiveBoardAsync(string boardId)     => Active.ArchiveBoardAsync(boardId);
    public Task RestoreBoardAsync(string boardId)     => Active.RestoreBoardAsync(boardId);
    public Task DeleteBoardAsync(string boardId)      => Active.DeleteBoardAsync(boardId);
}
