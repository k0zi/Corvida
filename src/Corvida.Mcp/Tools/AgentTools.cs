using System.ComponentModel;
using System.Text.Json;
using Corvida.Services;
using ModelContextProtocol.Server;

namespace Corvida.Mcp.Tools;

[McpServerToolType]
public sealed class AgentTools(IAgentService agents)
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    [McpServerTool, Description(
        "List all agents. Returns each agent's ID, name, and color.")]
    public async Task<string> list_agents(CancellationToken cancellationToken)
    {
        var all = await agents.GetAgentsAsync();
        var result = all.Select(a => new { a.Id, a.Name, a.Color });
        return JsonSerializer.Serialize(result, JsonOpts);
    }

    [McpServerTool, Description(
        "Get a agent's full details by ID, including their markdown personality and avatar.")]
    public async Task<string> get_agent(
        [Description("The agent ID")] string agentId,
        CancellationToken cancellationToken)
    {
        var agent = await agents.GetAgentAsync(agentId);
        return agent is null
            ? """{"error":"Agent not found"}"""
            : JsonSerializer.Serialize(agent, JsonOpts);
    }

    [McpServerTool, Description(
        "Create a new agent. Optionally set their markdown personality and accent color.")]
    public async Task<string> create_agent(
        [Description("The agent's display name")] string name,
        [Description("Optional markdown personality description")] string? personality,
        [Description("Optional hex color, e.g. #4C6EF5")] string? color,
        CancellationToken cancellationToken)
    {
        var agent = await agents.CreateAgentAsync(name);
        if (personality is not null) agent.Personality = personality;
        if (color is not null) agent.Color = color;
        if (personality is not null || color is not null)
            await agents.SaveAgentAsync(agent);

        return JsonSerializer.Serialize(new { agent.Id, agent.Name, agent.Color }, JsonOpts);
    }

    [McpServerTool, Description(
        "Update fields on an existing agent. Only non-null arguments are applied; omit a field to leave it unchanged.")]
    public async Task<string> update_agent(
        [Description("The agent ID to update")] string agentId,
        [Description("New display name, or null to keep existing")] string? name,
        [Description("New markdown personality, or null to keep existing")] string? personality,
        [Description("New hex color, or null to keep existing")] string? color,
        CancellationToken cancellationToken)
    {
        var agent = await agents.GetAgentAsync(agentId);
        if (agent is null) return """{"error":"Agent not found"}""";

        if (name is not null) agent.Name = name;
        if (personality is not null) agent.Personality = personality;
        if (color is not null) agent.Color = color;

        await agents.SaveAgentAsync(agent);
        return JsonSerializer.Serialize(agent, JsonOpts);
    }

    [McpServerTool, Description(
        "Delete a agent permanently. Fails if the agent still has tasks assigned to them; " +
        "unassign those tasks first. Otherwise they are removed from every board's membership.")]
    public async Task<string> delete_agent(
        [Description("The agent ID to delete")] string agentId,
        CancellationToken cancellationToken)
    {
        try
        {
            await agents.DeleteAgentAsync(agentId);
            return """{"ok":true}""";
        }
        catch (InvalidOperationException ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, JsonOpts);
        }
    }
}
