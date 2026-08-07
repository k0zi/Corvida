using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Corvida.Models;

namespace Corvida.Services;

public class HttpTaskService(IHttpClientFactory factory, ISettingsService settings) : ITaskService
{
    private HttpClient Client => factory.CreateClient("CorvidaApi");
    private string Base => (settings.Settings.ServerUrl ?? "http://localhost:5083").TrimEnd('/');

    public async Task<KanbanTask?> GetTaskAsync(string boardId, string taskId)
    {
        try
        {
            var resp = await Client.GetAsync($"{Base}/api/boards/{boardId}/tasks/{taskId}");
            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
            resp.EnsureSuccessStatusCode();
            return await resp.Content.ReadFromJsonAsync<KanbanTask>();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new InvalidOperationException($"Cannot reach Corvida server at {Base}. Check your Server URL setting.", ex);
        }
    }

    public async Task SaveTaskAsync(KanbanTask task)
    {
        try
        {
            var resp = await Client.PutAsJsonAsync($"{Base}/api/boards/{task.BoardId}/tasks/{task.Id}", task);
            resp.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            throw new InvalidOperationException("This board is archived and cannot be modified.", ex);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new InvalidOperationException($"Cannot reach Corvida server at {Base}. Check your Server URL setting.", ex);
        }
    }

    public async Task DeleteTaskAsync(string boardId, string taskId)
    {
        try
        {
            var resp = await Client.DeleteAsync($"{Base}/api/boards/{boardId}/tasks/{taskId}");
            if (resp.StatusCode != System.Net.HttpStatusCode.NotFound)
                resp.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            throw new InvalidOperationException("This board is archived and cannot be modified.", ex);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new InvalidOperationException($"Cannot reach Corvida server at {Base}. Check your Server URL setting.", ex);
        }
    }
}
