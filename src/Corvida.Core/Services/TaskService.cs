using System.IO;
using System.Threading.Tasks;
using Corvida.Models;

namespace Corvida.Services;

public class TaskService : ITaskService
{
    private readonly ISettingsService _settings;

    public TaskService(ISettingsService settings) => _settings = settings;

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
        Directory.CreateDirectory(TasksDir(task.BoardId));
        await File.WriteAllTextAsync(TaskFile(task.BoardId, task.Id), MarkdownSerializer.Serialize(task));
    }

    public Task DeleteTaskAsync(string boardId, string taskId)
    {
        var path = TaskFile(boardId, taskId);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }
}
