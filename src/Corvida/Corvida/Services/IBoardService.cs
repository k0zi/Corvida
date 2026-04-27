using System.Collections.Generic;
using System.Threading.Tasks;
using Corvida.Models;

namespace Corvida.Services;

public interface IBoardService
{
    Task<List<Board>> GetBoardsAsync();
    Task<Board> CreateBoardAsync(string name);
    Task SaveBoardAsync(Board board);
    Task DeleteBoardAsync(string boardId);
}
