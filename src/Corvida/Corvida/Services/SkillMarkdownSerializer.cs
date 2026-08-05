using System.Collections.Generic;
using System.Text;

namespace Corvida.Services;

public static class SkillMarkdownSerializer
{
    public static (string Name, string Description, string Body) Parse(string text)
    {
        var name = string.Empty;
        var description = string.Empty;
        var lines = text.Split('\n');
        var inFrontmatter = false;
        var pastFrontmatter = false;
        var fmDelimiterCount = 0;
        var bodyLines = new List<string>();

        foreach (var raw in lines)
        {
            var line = raw.TrimEnd('\r');
            if (!pastFrontmatter)
            {
                if (line.TrimEnd() == "---")
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
                        case "name": name = value; break;
                        case "description": description = value; break;
                    }
                }
            }
            else
            {
                bodyLines.Add(raw.TrimEnd('\r'));
            }
        }

        var body = string.Join('\n', bodyLines).Trim();
        return (name, description, body);
    }

    public static string Serialize(string name, string description, string body)
    {
        var sb = new StringBuilder();
        sb.AppendLine("---");
        sb.AppendLine($"name: {name}");
        sb.AppendLine($"description: {description}");
        sb.AppendLine("---");
        sb.AppendLine();
        sb.Append(body);
        return sb.ToString();
    }
}
