using System;
using System.Text;
using Corvida.Models;

namespace Corvida.Services;

/// <summary>Renders a Corvida <see cref="Agent"/> in one target tool's agent-definition format.</summary>
public interface IAgentFileFormat
{
    string FileName(string slug);
    string Render(Agent agent, string slug);
}

public static class AgentFileFormats
{
    public static IAgentFileFormat For(AgentTarget target) => target switch
    {
        AgentTarget.ClaudeCode => new ClaudeCodeAgentFormat(),
        AgentTarget.OpenCode => new OpenCodeAgentFormat(),
        _ => throw new ArgumentOutOfRangeException(nameof(target)),
    };

    /// <summary>The delegation trigger every target needs, on one line.</summary>
    /// <remarks>
    /// The Description box is prose, so it is flattened rather than escaped — a multi-line trigger
    /// is legal YAML but reads badly in the tools' agent pickers. An agent with no description at
    /// all would never be delegated to, so its name stands in.
    /// </remarks>
    internal static string Trigger(Agent agent)
    {
        var flattened = YamlScalar.Flatten(agent.Description);
        return flattened.Length > 0 ? flattened : $"The {agent.Name} agent.";
    }
}

/// <summary>
/// Claude Code: <c>~/.claude/agents/{name}.md</c>, frontmatter <c>name</c> + <c>description</c>,
/// body used verbatim as the subagent's system prompt.
/// </summary>
/// <remarks>
/// <c>tools</c> and <c>model</c> are left out on purpose: omitting them gives the subagent the full
/// tool set and the parent's model, which is the right default for an agent Corvida knows nothing
/// else about. <c>color</c> is left out because Claude Code does not document what values it takes,
/// and Corvida's per-agent colour is a hex string that may well not be one of them.
/// </remarks>
public class ClaudeCodeAgentFormat : IAgentFileFormat
{
    public string FileName(string slug) => slug + ".md";

    public string Render(Agent agent, string slug)
    {
        var sb = new StringBuilder();
        sb.AppendLine("---");
        sb.AppendLine($"name: {YamlScalar.Encode(slug)}");
        sb.AppendLine($"description: {YamlScalar.Encode(AgentFileFormats.Trigger(agent))}");
        sb.AppendLine("---");
        sb.AppendLine();
        sb.Append(agent.Personality);
        return sb.ToString();
    }
}

/// <summary>
/// OpenCode: <c>~/.config/opencode/agent/{name}.md</c>, body used as the system prompt.
/// </summary>
/// <remarks>
/// Two differences from Claude Code that matter, both silent if you get them wrong. OpenCode
/// derives the agent's name from the file path, not a <c>name</c> frontmatter key — and it strips
/// only the <c>agent/</c> prefix, so a nested file would be named <c>team/reviewer</c>; definitions
/// are therefore written flat. And <c>mode</c> defaults to <c>all</c>, which puts the agent in the
/// primary-agent switcher; exported Corvida agents are things to delegate to, so they say
/// <c>subagent</c> explicitly.
/// </remarks>
public class OpenCodeAgentFormat : IAgentFileFormat
{
    public string FileName(string slug) => slug + ".md";

    public string Render(Agent agent, string slug)
    {
        var sb = new StringBuilder();
        sb.AppendLine("---");
        sb.AppendLine($"description: {YamlScalar.Encode(AgentFileFormats.Trigger(agent))}");
        sb.AppendLine("mode: subagent");
        sb.AppendLine("---");
        sb.AppendLine();
        sb.Append(agent.Personality);
        return sb.ToString();
    }
}
