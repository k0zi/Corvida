using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Corvida.Services;

public class AgentInstallerService(IAgentService agentService) : IAgentInstallerService
{
    public async Task<bool> IsInstalledAsync(string targetRoot)
    {
        var root = ExpandPath(targetRoot);
        var agents = await agentService.GetAgentsAsync();

        foreach (var agent in agents)
        {
            var slug = Slugify(agent.Name);
            if (!File.Exists(Path.Combine(root, slug + ".md")))
                return false;
        }
        return true;
    }

    public async Task InstallAsync(string targetRoot)
    {
        var root = ExpandPath(targetRoot);
        Directory.CreateDirectory(root);

        var agents = await agentService.GetAgentsAsync();
        foreach (var agent in agents)
        {
            var slug = Slugify(agent.Name);
            var content = SkillMarkdownSerializer.Serialize(slug, agent.Description, agent.Personality);
            await File.WriteAllTextAsync(Path.Combine(root, slug + ".md"), content);
        }
    }

    public async Task UninstallAsync(string targetRoot)
    {
        var root = ExpandPath(targetRoot);
        if (!Directory.Exists(root)) return;

        var agents = await agentService.GetAgentsAsync();
        foreach (var agent in agents)
        {
            var slug = Slugify(agent.Name);
            var path = Path.Combine(root, slug + ".md");
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private static string Slugify(string value)
    {
        var lowered = value.ToLowerInvariant();
        var replaced = Regex.Replace(lowered, "[^a-z0-9]+", "-");
        return replaced.Trim('-');
    }

    private static string ExpandPath(string path)
    {
        path = path.Trim();
        if (path.StartsWith('~'))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            path = home + path[1..];
        }
        return path;
    }
}
