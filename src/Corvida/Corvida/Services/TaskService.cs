using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
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
        return ParseMarkdown(text);
    }

    public async Task SaveTaskAsync(KanbanTask task)
    {
        Directory.CreateDirectory(TasksDir(task.BoardId));
        await File.WriteAllTextAsync(TaskFile(task.BoardId, task.Id), SerializeMarkdown(task));
    }

    public Task DeleteTaskAsync(string boardId, string taskId)
    {
        var path = TaskFile(boardId, taskId);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    private static KanbanTask ParseMarkdown(string text)
    {
        var task = new KanbanTask();
        var lines = text.Split('\n');
        var inFrontmatter = false;
        var bodyLines = new List<string>();
        var pastFrontmatter = false;
        var fmDelimiterCount = 0;

        foreach (var raw in lines)
        {
            var line = raw.TrimEnd();
            if (!pastFrontmatter)
            {
                if (line == "---")
                {
                    fmDelimiterCount++;
                    if (fmDelimiterCount == 1) { inFrontmatter = true; continue; }
                    if (fmDelimiterCount == 2) { inFrontmatter = false; pastFrontmatter = true; continue; }
                }
                if (inFrontmatter)
                {
                    var colon = line.IndexOf(':');
                    if (colon < 0) continue;
                    var key = line[..colon].Trim();
                    var value = line[(colon + 1)..].Trim();
                    switch (key)
                    {
                        case "id": task.Id = value; break;
                        case "title": task.Title = value; break;
                        case "groupId": task.GroupId = value; break;
                        case "boardId": task.BoardId = value; break;
                        case "priority": task.Priority = value; break;
                        case "created":
                            if (DateTime.TryParse(value, CultureInfo.InvariantCulture,
                                    DateTimeStyles.RoundtripKind, out var dt))
                                task.Created = dt;
                            break;
                        case "plannedStart":
                            if (DateTime.TryParse(value, CultureInfo.InvariantCulture,
                                    DateTimeStyles.RoundtripKind, out var ps))
                                task.PlannedStart = ps;
                            break;
                        case "plannedEnd":
                            if (DateTime.TryParse(value, CultureInfo.InvariantCulture,
                                    DateTimeStyles.RoundtripKind, out var pe))
                                task.PlannedEnd = pe;
                            break;
                    }
                }
            }
            else
            {
                bodyLines.Add(raw.TrimEnd());
            }
        }

        task.Description = string.Join('\n', bodyLines).Trim();
        return task;
    }

    private static string SerializeMarkdown(KanbanTask task)
    {
        var sb = new StringBuilder();
        sb.AppendLine("---");
        sb.AppendLine($"id: {task.Id}");
        sb.AppendLine($"title: {task.Title}");
        sb.AppendLine($"groupId: {task.GroupId}");
        sb.AppendLine($"boardId: {task.BoardId}");
        sb.AppendLine($"created: {task.Created:O}");
        sb.AppendLine($"priority: {task.Priority}");
        if (task.PlannedStart.HasValue)
            sb.AppendLine($"plannedStart: {task.PlannedStart.Value:O}");
        if (task.PlannedEnd.HasValue)
            sb.AppendLine($"plannedEnd: {task.PlannedEnd.Value:O}");
        sb.AppendLine("---");
        sb.AppendLine();
        sb.Append(task.Description);
        return sb.ToString();
    }
}
