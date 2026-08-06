using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Corvida.Models;

namespace Corvida.Services;

public static class BuiltInAgentSeeder
{
    private static readonly Dictionary<string, string> Colors = new()
    {
        ["developer"] = "#1971C2",
        ["code-reviewer"] = "#0CA678",
        ["orchestrator"] = "#7048E8",
        ["architect"] = "#F59F00",
        ["security-reviewer"] = "#F03E3E",
    };

    public static async Task EnsureSeededAsync(IAgentService agentService)
    {
        var templatesDir = Path.Combine(AppContext.BaseDirectory, "Agents");
        if (!Directory.Exists(templatesDir)) return;

        var existingIds = (await agentService.GetAgentsAsync()).Select(a => a.Id).ToHashSet();

        foreach (var file in Directory.GetFiles(templatesDir, "*.md"))
        {
            var slug = Path.GetFileNameWithoutExtension(file);
            var id = "builtin-" + slug;
            if (existingIds.Contains(id)) continue;

            var (name, description, body) = SkillMarkdownSerializer.Parse(await File.ReadAllTextAsync(file));

            await agentService.SaveAgentAsync(new Agent
            {
                Id = id,
                Name = name,
                Description = description,
                Personality = body,
                Color = Colors.GetValueOrDefault(slug, "#4C6EF5"),
            });
        }
    }
}
