using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace Corvida.Services;

public class ExportService(HttpBoardService boardService, HttpTaskService taskService) : IExportService
{
    private static readonly JsonSerializerOptions WriteOpts = new() { WriteIndented = true };

    public async Task ExportAsync(string targetFolder)
    {
        var boards = await boardService.GetBoardsAsync();

        foreach (var board in boards)
        {
            var boardDir = Path.Combine(targetFolder, "boards", board.Id);
            var tasksDir = Path.Combine(boardDir, "tasks");
            Directory.CreateDirectory(tasksDir);

            var json = JsonSerializer.Serialize(board, WriteOpts);
            await File.WriteAllTextAsync(Path.Combine(boardDir, "board.json"), json);

            foreach (var group in board.Groups)
            {
                foreach (var taskId in group.TaskIds)
                {
                    var task = await taskService.GetTaskAsync(board.Id, taskId);
                    if (task is null) continue;
                    var md = MarkdownSerializer.Serialize(task);
                    await File.WriteAllTextAsync(Path.Combine(tasksDir, taskId + ".md"), md);
                }
            }
        }
    }
}
