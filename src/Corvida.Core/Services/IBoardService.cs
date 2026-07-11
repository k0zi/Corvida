using System.Collections.Generic;
using System.Threading.Tasks;
using Corvida.Models;

namespace Corvida.Services;

public interface IBoardService
{
    Task<List<Board>> GetBoardsAsync();
    Task<List<Board>> GetArchivedBoardsAsync();
    Task<Board> CreateBoardAsync(string name);
    Task SaveBoardAsync(Board board);
    Task ArchiveBoardAsync(string boardId);
    Task RestoreBoardAsync(string boardId);
    Task DeleteBoardAsync(string boardId);
}
