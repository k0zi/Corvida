using System.Collections.Generic;
using System.Text;
using Corvida.Models;

namespace Corvida.Services;

public static class AgentMarkdownSerializer
{
    public static Agent Parse(string text)
    {
        var agent = new Agent();
        var lines = text.Split('\n');
        var inFrontmatter = false;
        var bodyLines = new List<string>();
        var pastFrontmatter = false;
        var fmDelimiterCount = 0;

        foreach (var raw in lines)
        {
            var line = raw.TrimEnd();
            if (!pastFrontmatter)
            {
                if (line == "---")
                {
                    fmDelimiterCount++;
                    if (fmDelimiterCount == 1) { inFrontmatter = true; continue; }
                    if (fmDelimiterCount == 2) { inFrontmatter = false; pastFrontmatter = true; continue; }
                }
                if (inFrontmatter)
                {
                    var colon = line.IndexOf(':');
                    if (colon < 0) continue;
                    var key = line[..colon].Trim();
                    var value = line[(colon + 1)..].Trim();
                    switch (key)
                    {
                        case "id": agent.Id = value; break;
                        case "name": agent.Name = value; break;
                        case "color": agent.Color = value; break;
                        case "avatar": agent.AvatarDataUri = value; break;
                    }
                }
            }
            else
            {
                bodyLines.Add(raw.TrimEnd());
            }
        }

        agent.Personality = string.Join('\n', bodyLines).Trim();
        return agent;
    }

    public static string Serialize(Agent agent)
    {
        var sb = new StringBuilder();
        sb.AppendLine("---");
        sb.AppendLine($"id: {agent.Id}");
        sb.AppendLine($"name: {agent.Name}");
        sb.AppendLine($"color: {agent.Color}");
        if (agent.AvatarDataUri is not null)
            sb.AppendLine($"avatar: {agent.AvatarDataUri}");
        sb.AppendLine("---");
        sb.AppendLine();
        sb.Append(agent.Personality);
        return sb.ToString();
    }
}
