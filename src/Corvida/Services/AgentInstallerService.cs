using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Corvida.Models;

namespace Corvida.Services;

public class AgentInstallerService(IAgentService agentService) : IAgentInstallerService
{
    public async Task<bool> IsInstalledAsync(AgentTarget target, string targetRoot)
    {
        var root = PathUtil.Expand(targetRoot);
        var manifest = await AgentInstallManifest.LoadAsync();

        // Deliberately not "every agent has a file": with no agents at all the old check passed
        // vacuously and the UI claimed an install that had written nothing. An install that hit a
        // conflict is still an install, so one surviving file we own is the honest signal.
        return manifest.EntriesFor(root).Any(e => File.Exists(Path.Combine(root, e.FileName)));
    }

    public async Task<AgentInstallResult> InstallAsync(AgentTarget target, string targetRoot)
    {
        var root = PathUtil.Expand(targetRoot);
        var agents = await agentService.GetAgentsAsync();
        if (agents.Count == 0) return AgentInstallResult.Empty;

        Directory.CreateDirectory(root);

        var format = AgentFileFormats.For(target);
        var manifest = await AgentInstallManifest.LoadAsync();
        var owned = manifest.EntriesFor(root);

        var written = 0;
        var conflicts = new List<string>();
        var keptModified = new List<string>();
        var next = new List<AgentInstallEntry>();

        foreach (var (agent, fileName) in Resolve(agents, format))
        {
            var path = Path.Combine(root, fileName);
            var wasOurs = owned.FirstOrDefault(e =>
                string.Equals(e.FileName, fileName, StringComparison.OrdinalIgnoreCase));

            if (File.Exists(path))
            {
                if (wasOurs is null)
                {
                    conflicts.Add(agent.Name);
                    continue;
                }

                // We wrote it, but it has been edited since. Treat the edit as the user adopting
                // the file: leave it alone and disown it, so from here on it is somebody else's.
                if (AgentInstallManifest.Hash(await File.ReadAllTextAsync(path)) != wasOurs.Sha256)
                {
                    keptModified.Add(fileName);
                    continue;
                }
            }

            var content = format.Render(agent, Path.GetFileNameWithoutExtension(fileName));
            await File.WriteAllTextAsync(path, content);
            next.Add(new AgentInstallEntry(agent.Id, fileName, AgentInstallManifest.Hash(content)));
            written++;
        }

        // Anything we wrote before and no longer want — an agent that was renamed, or deleted from
        // Corvida entirely — would otherwise stay in the folder forever and keep being offered.
        var stale = owned.Where(e =>
            next.All(n => !string.Equals(n.FileName, e.FileName, StringComparison.OrdinalIgnoreCase)) &&
            !keptModified.Contains(e.FileName)).ToList();

        var (removed, kept) = DeleteOwned(root, stale);
        keptModified.AddRange(kept);

        manifest.SetEntriesFor(root, next);
        await manifest.SaveAsync();

        return new AgentInstallResult(written, removed, conflicts, keptModified);
    }

    public async Task<AgentInstallResult> UninstallAsync(AgentTarget target, string targetRoot)
    {
        var root = PathUtil.Expand(targetRoot);
        var manifest = await AgentInstallManifest.LoadAsync();
        var owned = manifest.EntriesFor(root);
        if (owned.Count == 0) return AgentInstallResult.Empty;

        var (removed, kept) = DeleteOwned(root, owned);

        // Files left behind because the user edited them are disowned rather than kept on the
        // books: still claiming them would leave the target reporting itself as installed.
        manifest.SetEntriesFor(root, []);
        await manifest.SaveAsync();

        return new AgentInstallResult(0, removed, [], kept);
    }

    /// <summary>
    /// Pairs each agent with the file name it gets, keeping two agents that slugify alike apart.
    /// </summary>
    private static IEnumerable<(Agent Agent, string FileName)> Resolve(
        IReadOnlyList<Agent> agents, IAgentFileFormat format)
    {
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var agent in agents)
        {
            var slug = AgentSlug.For(agent);
            if (!taken.Add(slug))
            {
                slug = AgentSlug.Disambiguate(slug, agent);
                taken.Add(slug);
            }
            yield return (agent, format.FileName(slug));
        }
    }

    /// <summary>
    /// Deletes the given Corvida-written files, leaving behind any the user has since edited.
    /// </summary>
    /// <remarks>The caller drops every entry passed here from the manifest either way.</remarks>
    private static (int Removed, List<string> Kept) DeleteOwned(string root, IEnumerable<AgentInstallEntry> entries)
    {
        var removed = 0;
        var kept = new List<string>();

        foreach (var entry in entries)
        {
            var path = Path.Combine(root, entry.FileName);
            if (!File.Exists(path)) continue;

            if (AgentInstallManifest.Hash(File.ReadAllText(path)) != entry.Sha256)
            {
                kept.Add(entry.FileName);
                continue;
            }

            File.Delete(path);
            removed++;
        }

        return (removed, kept);
    }
}
