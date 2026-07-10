using System.Threading.Tasks;
using Corvida.Models;

namespace Corvida.Services;

public interface ITaskService
{
    Task<KanbanTask?> GetTaskAsync(string boardId, string taskId);
    Task SaveTaskAsync(KanbanTask task);
    Task DeleteTaskAsync(string boardId, string taskId);
}
