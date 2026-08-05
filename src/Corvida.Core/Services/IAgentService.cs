using System.Collections.Generic;
using System.Threading.Tasks;
using Corvida.Models;

namespace Corvida.Services;

public interface IAgentService
{
    Task<List<Agent>> GetAgentsAsync();
    Task<Agent?> GetAgentAsync(string agentId);
    Task<Agent> CreateAgentAsync(string name);
    Task SaveAgentAsync(Agent agent);
    Task DeleteAgentAsync(string agentId);
}
