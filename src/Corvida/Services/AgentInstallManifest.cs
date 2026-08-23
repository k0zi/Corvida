using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Corvida.Services;

/// <summary>One exported agent definition Corvida has written to a target folder.</summary>
public record AgentInstallEntry(string AgentId, string FileName, string Sha256);

/// <summary>
/// Records which files in a target tool's agents folder were written by Corvida.
/// </summary>
/// <remarks>
/// Install used to write and delete <c>{slug}.md</c> unconditionally, so an agent named after one
/// of the user's own hand-written subagents silently replaced it — and uninstall then deleted it.
/// Ownership can't be marked inside the file either: none of the target tools document what they do
/// with an unrecognised frontmatter key, and a rejected key would cost us the whole agent. So the
/// record lives on Corvida's side, next to settings.json.
/// </remarks>
public class AgentInstallManifest
{
    private static readonly string ManifestPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Corvida", "agent-installs.json");

    private static readonly SemaphoreSlim Gate = new(1, 1);

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public Dictionary<string, List<AgentInstallEntry>> Roots { get; set; } = new();

    public static async Task<AgentInstallManifest> LoadAsync()
    {
        await Gate.WaitAsync();
        try
        {
            if (!File.Exists(ManifestPath)) return new AgentInstallManifest();
            var json = await File.ReadAllTextAsync(ManifestPath);
            return JsonSerializer.Deserialize<AgentInstallManifest>(json, JsonOpts) ?? new AgentInstallManifest();
        }
        catch (Exception e) when (e is JsonException or IOException)
        {
            // A manifest we cannot read is treated as "we own nothing here", which degrades to
            // refusing to overwrite rather than to clobbering the user's files.
            return new AgentInstallManifest();
        }
        finally
        {
            Gate.Release();
        }
    }

    public async Task SaveAsync()
    {
        await Gate.WaitAsync();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ManifestPath)!);
            await File.WriteAllTextAsync(ManifestPath, JsonSerializer.Serialize(this, JsonOpts));
        }
        finally
        {
            Gate.Release();
        }
    }

    public List<AgentInstallEntry> EntriesFor(string root) =>
        Roots.TryGetValue(Key(root), out var entries) ? entries : [];

    public void SetEntriesFor(string root, List<AgentInstallEntry> entries)
    {
        var key = Key(root);
        if (entries.Count == 0) Roots.Remove(key);
        else Roots[key] = entries;
    }

    /// <summary>True when Corvida wrote <paramref name="fileName"/> into <paramref name="root"/>.</summary>
    public bool Owns(string root, string fileName) =>
        EntriesFor(root).Any(e => string.Equals(e.FileName, fileName, StringComparison.OrdinalIgnoreCase));

    public static string Hash(string content) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(content)));

    // Target roots come from editable text boxes, so "~/.claude/agents", "~/.claude/agents/" and an
    // already-expanded absolute path all have to resolve to the same manifest key.
    private static string Key(string root) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
}
