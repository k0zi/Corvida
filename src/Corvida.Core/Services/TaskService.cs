using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Corvida.Models;

namespace Corvida.Services;

public class TaskService : ITaskService
{
    private readonly ISettingsService _settings;

    public TaskService(ISettingsService settings) => _settings = settings;

    private string BoardFile(string boardId) =>
        Path.Combine(_settings.Settings.DataPath, "boards", boardId, "board.json");

    private string TasksDir(string boardId) =>
        Path.Combine(_settings.Settings.DataPath, "boards", boardId, "tasks");

    private string TaskFile(string boardId, string taskId) =>
        Path.Combine(TasksDir(boardId), taskId + ".md");

    public async Task<KanbanTask?> GetTaskAsync(string boardId, string taskId)
    {
        var path = TaskFile(boardId, taskId);
        if (!File.Exists(path)) return null;

        var text = await File.ReadAllTextAsync(path);
        return MarkdownSerializer.Parse(text);
    }

    public async Task SaveTaskAsync(KanbanTask task)
    {
        await EnsureBoardNotArchivedAsync(task.BoardId);
        Directory.CreateDirectory(TasksDir(task.BoardId));
        await File.WriteAllTextAsync(TaskFile(task.BoardId, task.Id), MarkdownSerializer.Serialize(task));
    }

    public async Task DeleteTaskAsync(string boardId, string taskId)
    {
        await EnsureBoardNotArchivedAsync(boardId);
        var path = TaskFile(boardId, taskId);
        if (File.Exists(path)) File.Delete(path);
    }

    private async Task EnsureBoardNotArchivedAsync(string boardId)
    {
        var file = BoardFile(boardId);
        if (!File.Exists(file)) return;

        var json = await File.ReadAllTextAsync(file);
        var board = JsonSerializer.Deserialize<Board>(json);
        if (board is { IsArchived: true })
            throw new InvalidOperationException("This board is archived and cannot be modified.");
    }
}
