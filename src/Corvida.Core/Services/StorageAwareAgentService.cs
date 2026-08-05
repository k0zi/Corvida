using System.Collections.Generic;
using System.Threading.Tasks;
using Corvida.Models;

namespace Corvida.Services;

public class StorageAwareAgentService(
    AgentService local,
    HttpAgentService http,
    ISettingsService settings) : IAgentService
{
    private IAgentService Active =>
        settings.Settings.StorageMode == StorageMode.ServerHosted ? http : local;

    public Task<List<Agent>> GetAgentsAsync()          => Active.GetAgentsAsync();
    public Task<Agent?> GetAgentAsync(string agentId)   => Active.GetAgentAsync(agentId);
    public Task<Agent> CreateAgentAsync(string name)   => Active.CreateAgentAsync(name);
    public Task SaveAgentAsync(Agent agent)              => Active.SaveAgentAsync(agent);
    public Task DeleteAgentAsync(string agentId)        => Active.DeleteAgentAsync(agentId);
}
