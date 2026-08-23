using System.Collections.Generic;
using System.Threading.Tasks;

namespace Corvida.Services;

/// <summary>The coding-agent tools Corvida can export its agents to.</summary>
/// <remarks>
/// Hermes is deliberately absent. It has no on-disk agent definitions at all — its subagents are
/// spawned at runtime from a goal string via its <c>delegate_task</c> tool — so nothing ever read
/// the <c>~/.hermes/agents/</c> folder Corvida used to write into. Hermes remains a target for
/// skills and for the MCP server, which it genuinely does load.
/// </remarks>
public enum AgentTarget { ClaudeCode, OpenCode }

/// <summary>What one install or uninstall run actually did.</summary>
/// <param name="Written">Definitions created or refreshed.</param>
/// <param name="Removed">Definitions deleted, including ones left over from a renamed agent.</param>
/// <param name="Conflicts">
/// Agents skipped because a file Corvida did not write already occupies their path. Reported rather
/// than overwritten — that path is somebody else's agent.
/// </param>
/// <param name="KeptModified">
/// Corvida-written files left in place because they were edited after Corvida wrote them.
/// </param>
public record AgentInstallResult(
    int Written,
    int Removed,
    IReadOnlyList<string> Conflicts,
    IReadOnlyList<string> KeptModified)
{
    public static readonly AgentInstallResult Empty = new(0, 0, [], []);
}

public interface IAgentInstallerService
{
    Task<bool> IsInstalledAsync(AgentTarget target, string targetRoot);
    Task<AgentInstallResult> InstallAsync(AgentTarget target, string targetRoot);
    Task<AgentInstallResult> UninstallAsync(AgentTarget target, string targetRoot);
}
