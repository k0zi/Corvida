using System.Text.Json;
using Corvida.Models;
using Corvida.Services;

namespace Corvida.Core.Tests;

public class BoardServiceTests : IDisposable
{
    private readonly TempDir _dir = new();
    private readonly FakeSettingsService _settings;
    private readonly BoardService _sut;

    public BoardServiceTests()
    {
        _settings = new FakeSettingsService();
        _settings.Settings.DataPath = _dir.Path;
        _sut = new BoardService(_settings);
    }

    public void Dispose() => _dir.Dispose();

    [Fact]
    public async Task GetBoardsAsync_ReturnsEmpty_WhenBoardsDirDoesNotExist()
    {
        var result = await _sut.GetBoardsAsync();
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetBoardsAsync_ReturnsEmpty_WhenBoardsDirExistsButIsEmpty()
    {
        Directory.CreateDirectory(Path.Combine(_dir.Path, "boards"));
        var result = await _sut.GetBoardsAsync();
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetBoardsAsync_SkipsSubdirectoriesWithoutBoardJson()
    {
        Directory.CreateDirectory(Path.Combine(_dir.Path, "boards", "orphan-dir"));
        var result = await _sut.GetBoardsAsync();
        Assert.Empty(result);
    }

    [Fact]
    public async Task CreateBoardAsync_ReturnsBoardWithCorrectName()
    {
        var board = await _sut.CreateBoardAsync("ProjectAlpha");
        Assert.Equal("ProjectAlpha", board.Name);
    }

    [Fact]
    public async Task CreateBoardAsync_BoardIdStartsWithNameAndSuffix()
    {
        var board = await _sut.CreateBoardAsync("MyBoard");
        Assert.StartsWith("MyBoard-brd-", board.Id);
    }

    [Fact]
    public async Task CreateBoardAsync_BoardIdSuffixIs8Chars()
    {
        var board = await _sut.CreateBoardAsync("MyBoard");
        var suffix = board.Id.Split("-brd-")[1];
        Assert.Equal(8, suffix.Length);
    }

    [Fact]
    public async Task CreateBoardAsync_HasThreeGroups()
    {
        var board = await _sut.CreateBoardAsync("Test");
        Assert.Equal(3, board.Groups.Count);
    }

    [Fact]
    public async Task CreateBoardAsync_GroupNamesAre_ToDo_InProgress_Done()
    {
        var board = await _sut.CreateBoardAsync("Test");
        var names = board.Groups.Select(g => g.Name).ToList();
        Assert.Contains("To-Do", names);
        Assert.Contains("In-Progress", names);
        Assert.Contains("Done", names);
    }

    [Fact]
    public async Task CreateBoardAsync_EachGroupHasUniqueId()
    {
        var board = await _sut.CreateBoardAsync("Test");
        var ids = board.Groups.Select(g => g.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public async Task CreateBoardAsync_WritesBoardJsonToDisk()
    {
        var board = await _sut.CreateBoardAsync("Test");
        var path = Path.Combine(_dir.Path, "boards", board.Id, "board.json");
        Assert.True(File.Exists(path));
    }

    [Fact]
    public async Task CreateBoardAsync_CreatesTasksSubdirectory()
    {
        var board = await _sut.CreateBoardAsync("Test");
        var tasksDir = Path.Combine(_dir.Path, "boards", board.Id, "tasks");
        Assert.True(Directory.Exists(tasksDir));
    }

    [Fact]
    public async Task GetBoardsAsync_ReturnsSavedBoard_AfterCreate()
    {
        var created = await _sut.CreateBoardAsync("ReturnMe");
        var boards = await _sut.GetBoardsAsync();
        Assert.Single(boards);
        Assert.Equal(created.Name, boards[0].Name);
    }

    [Fact]
    public async Task GetBoardsAsync_ReturnsAllBoards_WhenMultipleCreated()
    {
        await _sut.CreateBoardAsync("Alpha");
        await _sut.CreateBoardAsync("Beta");
        var boards = await _sut.GetBoardsAsync();
        Assert.Equal(2, boards.Count);
    }

    [Fact]
    public async Task DeleteBoardAsync_RemovesBoardDirectory()
    {
        var board = await _sut.CreateBoardAsync("ToDelete");
        var boardDir = Path.Combine(_dir.Path, "boards", board.Id);
        Assert.True(Directory.Exists(boardDir));

        await _sut.DeleteBoardAsync(board.Id);

        Assert.False(Directory.Exists(boardDir));
    }

    [Fact]
    public async Task DeleteBoardAsync_IsNoOp_WhenBoardDoesNotExist()
    {
        await _sut.DeleteBoardAsync("nonexistent-brd-00000000");
    }

    [Fact]
    public async Task SaveBoardAsync_WritesValidJson()
    {
        var board = new Board { Id = "test-brd-12345678", Name = "Persisted" };
        await _sut.SaveBoardAsync(board);

        var path = Path.Combine(_dir.Path, "boards", board.Id, "board.json");
        var json = await File.ReadAllTextAsync(path);
        var deserialized = JsonSerializer.Deserialize<Board>(json);

        Assert.NotNull(deserialized);
        Assert.Equal(board.Id, deserialized!.Id);
        Assert.Equal(board.Name, deserialized.Name);
    }

    [Fact]
    public async Task CreateTwoBoards_HaveDifferentIds()
    {
        var a = await _sut.CreateBoardAsync("Board");
        var b = await _sut.CreateBoardAsync("Board");
        Assert.NotEqual(a.Id, b.Id);
    }
}
