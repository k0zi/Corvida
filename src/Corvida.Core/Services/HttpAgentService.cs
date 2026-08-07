using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Corvida.Models;

namespace Corvida.Services;

public class HttpAgentService(IHttpClientFactory factory, ISettingsService settings) : IAgentService
{
    private HttpClient Client => factory.CreateClient("CorvidaApi");
    private string Base => (settings.Settings.ServerUrl ?? "http://localhost:5083").TrimEnd('/');

    public async Task<List<Agent>> GetAgentsAsync()
    {
        try
        {
            return await Client.GetFromJsonAsync<List<Agent>>($"{Base}/api/agents") ?? [];
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new InvalidOperationException($"Cannot reach Corvida server at {Base}. Check your Server URL setting.", ex);
        }
    }

    public async Task<Agent?> GetAgentAsync(string agentId)
    {
        try
        {
            var resp = await Client.GetAsync($"{Base}/api/agents/{agentId}");
            if (resp.StatusCode == HttpStatusCode.NotFound) return null;
            resp.EnsureSuccessStatusCode();
            return await resp.Content.ReadFromJsonAsync<Agent>();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new InvalidOperationException($"Cannot reach Corvida server at {Base}. Check your Server URL setting.", ex);
        }
    }

    public async Task<Agent> CreateAgentAsync(string name)
    {
        try
        {
            var resp = await Client.PostAsJsonAsync($"{Base}/api/agents", new { Name = name });
            resp.EnsureSuccessStatusCode();
            return (await resp.Content.ReadFromJsonAsync<Agent>())!;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new InvalidOperationException($"Cannot reach Corvida server at {Base}. Check your Server URL setting.", ex);
        }
    }

    public async Task SaveAgentAsync(Agent agent)
    {
        try
        {
            var resp = await Client.PutAsJsonAsync($"{Base}/api/agents/{agent.Id}", agent);
            resp.EnsureSuccessStatusCode();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new InvalidOperationException($"Cannot reach Corvida server at {Base}. Check your Server URL setting.", ex);
        }
    }

    public async Task DeleteAgentAsync(string agentId)
    {
        try
        {
            var resp = await Client.DeleteAsync($"{Base}/api/agents/{agentId}");
            if (resp.StatusCode == HttpStatusCode.NotFound) return;

            if (resp.StatusCode == HttpStatusCode.Conflict)
            {
                var problem = await resp.Content.ReadFromJsonAsync<Dictionary<string, string>>();
                throw new InvalidOperationException(
                    problem is not null && problem.TryGetValue("error", out var msg)
                        ? msg
                        : "Agent has assigned tasks and cannot be deleted.");
            }

            resp.EnsureSuccessStatusCode();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new InvalidOperationException($"Cannot reach Corvida server at {Base}. Check your Server URL setting.", ex);
        }
    }
}
