using System.Threading.Tasks;
using Corvida.Models;

namespace Corvida.Services;

public class StorageAwareTaskService(
    TaskService local,
    HttpTaskService http,
    ISettingsService settings) : ITaskService
{
    private ITaskService Active =>
        settings.Settings.StorageMode == StorageMode.ServerHosted ? http : local;

    public Task<KanbanTask?> GetTaskAsync(string boardId, string taskId) =>
        Active.GetTaskAsync(boardId, taskId);

    public Task SaveTaskAsync(KanbanTask task) => Active.SaveTaskAsync(task);

    public Task DeleteTaskAsync(string boardId, string taskId) =>
        Active.DeleteTaskAsync(boardId, taskId);
}
