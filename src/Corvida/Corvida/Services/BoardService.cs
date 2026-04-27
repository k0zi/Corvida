using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Corvida.Models;

namespace Corvida.Services;

public class BoardService : IBoardService
{
    private readonly ISettingsService _settings;

    public BoardService(ISettingsService settings) => _settings = settings;

    private string BoardsRoot => Path.Combine(_settings.Settings.DataPath, "boards");

    private string BoardDir(string boardId) => Path.Combine(BoardsRoot, boardId);

    private string BoardFile(string boardId) => Path.Combine(BoardDir(boardId), "board.json");

    public async Task<List<Board>> GetBoardsAsync()
    {
        var result = new List<Board>();
        if (!Directory.Exists(BoardsRoot)) return result;

        foreach (var dir in Directory.GetDirectories(BoardsRoot))
        {
            var file = Path.Combine(dir, "board.json");
            if (!File.Exists(file)) continue;

            try
            {
                var json = await File.ReadAllTextAsync(file);
                var board = JsonSerializer.Deserialize<Board>(json);
                if (board is not null) result.Add(board);
            }
            catch { }
        }

        return result;
    }

    public async Task<Board> CreateBoardAsync(string name)
    {
        var board = new Board
        {
            Id = "brd-" + Guid.NewGuid().ToString("N")[..8],
            Name = name,
            Groups = new List<KanbanGroup>
            {
                new() { Id = "grp-" + Guid.NewGuid().ToString("N")[..8], Name = "To-Do" },
                new() { Id = "grp-" + Guid.NewGuid().ToString("N")[..8], Name = "In-Progress" },
                new() { Id = "grp-" + Guid.NewGuid().ToString("N")[..8], Name = "Done" }
            }
        };

        Directory.CreateDirectory(Path.Combine(BoardDir(board.Id), "tasks"));
        await SaveBoardAsync(board);
        return board;
    }

    public async Task SaveBoardAsync(Board board)
    {
        Directory.CreateDirectory(BoardDir(board.Id));
        var json = JsonSerializer.Serialize(board, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(BoardFile(board.Id), json);
    }

    public Task DeleteBoardAsync(string boardId)
    {
        var dir = BoardDir(boardId);
        if (Directory.Exists(dir))
            Directory.Delete(dir, recursive: true);
        return Task.CompletedTask;
    }
}
