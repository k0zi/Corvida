using System.Text;

namespace Corvida.Services;

public static class SkillAgentYamlSerializer
{
    public static (string DisplayName, string ShortDescription) Parse(string text)
    {
        var displayName = string.Empty;
        var shortDescription = string.Empty;

        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            var colon = line.IndexOf(':');
            if (colon < 0) continue;
            var key = line[..colon].Trim();
            var value = line[(colon + 1)..].Trim().Trim('"');
            switch (key)
            {
                case "display_name": displayName = value; break;
                case "short_description": shortDescription = value; break;
            }
        }

        return (displayName, shortDescription);
    }

    public static string Serialize(string displayName, string shortDescription)
    {
        var sb = new StringBuilder();
        sb.AppendLine("interface:");
        sb.AppendLine($"  display_name: \"{displayName}\"");
        sb.AppendLine($"  short_description: \"{shortDescription}\"");
        return sb.ToString();
    }
}
