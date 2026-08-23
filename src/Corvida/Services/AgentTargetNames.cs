using System;

namespace Corvida.Services;

public static class AgentTargetNames
{
    public static string Display(AgentTarget target) => target switch
    {
        AgentTarget.ClaudeCode => "Claude Code",
        AgentTarget.OpenCode => "OpenCode",
        _ => throw new ArgumentOutOfRangeException(nameof(target)),
    };
}
