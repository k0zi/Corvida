using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using YamlDotNet.Serialization;

namespace Corvida.Services;

// Registers the Corvida MCP server ("corvida") with an AI tool by partially merging a single
// entry into that tool's own config file — never touching any other key in the file, since
// these are the user's real, actively-used configs (Claude Code's ~/.claude.json in particular
// holds project history and other settings unrelated to MCP).
public class McpInstallerService : IMcpInstallerService
{
    private const string ServerName = "corvida";

    // Launcher symlink installed by the corvida .deb/.rpm (packaging/nfpm.yaml). Preferred over the real
    // path below it because it is what a user would type, and it survives a change of install layout.
    private const string PackagedServerPath = "/usr/bin/corvida-mcp";

    private static readonly IDeserializer YamlDeserializer = new DeserializerBuilder().Build();
    private static readonly ISerializer YamlSerializer = new SerializerBuilder().Build();

    public bool IsInstalled(McpTarget target, string configPath)
    {
        var path = PathUtil.Expand(configPath);
        if (!File.Exists(path)) return false;

        try
        {
            return target switch
            {
                McpTarget.ClaudeCode => JsonContainer(path, "mcpServers")?[ServerName] is not null,
                McpTarget.OpenCode => JsonContainer(path, "mcp")?[ServerName] is not null,
                McpTarget.Hermes => YamlContainer(LoadYamlRoot(path)).ContainsKey(ServerName),
                _ => false,
            };
        }
        catch (Exception ex) when (ex is JsonException or YamlDotNet.Core.YamlException)
        {
            return false;
        }
    }

    public string? BundledServerPath
    {
        get
        {
            if (OperatingSystem.IsLinux() && File.Exists(PackagedServerPath))
                return PackagedServerPath;

            // The package lays the server out as mcp/ beside the app; a portable copy does the same.
            var beside = Path.Combine(AppContext.BaseDirectory, "mcp",
                OperatingSystem.IsWindows() ? "Corvida.Mcp.exe" : "Corvida.Mcp");
            return File.Exists(beside) ? beside : null;
        }
    }

    public void Install(McpTarget target, string configPath, string mcpProjectPath)
    {
        var path = PathUtil.Expand(configPath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var (command, args) = ServerCommand(mcpProjectPath);

        switch (target)
        {
            case McpTarget.ClaudeCode:
                WriteJson(path, "mcpServers", new JsonObject
                {
                    ["command"] = command,
                    ["args"] = JsonStringArray(args),
                });
                break;

            case McpTarget.OpenCode:
                // OpenCode takes the executable and its arguments as one array.
                WriteJson(path, "mcp", new JsonObject
                {
                    ["type"] = "local",
                    ["command"] = JsonStringArray([command, .. args]),
                    ["enabled"] = true,
                });
                break;

            case McpTarget.Hermes:
                var root = LoadYamlRoot(path);
                YamlContainer(root)[ServerName] = new Dictionary<object, object>
                {
                    ["command"] = command,
                    ["args"] = args.Cast<object>().ToList(),
                };
                File.WriteAllText(path, YamlSerializer.Serialize(root));
                break;
        }
    }

    public void Uninstall(McpTarget target, string configPath)
    {
        var path = PathUtil.Expand(configPath);
        if (!File.Exists(path)) return;

        switch (target)
        {
            case McpTarget.ClaudeCode:
                RemoveJsonEntry(path, "mcpServers");
                break;

            case McpTarget.OpenCode:
                RemoveJsonEntry(path, "mcp");
                break;

            case McpTarget.Hermes:
                var root = LoadYamlRoot(path);
                if (YamlContainer(root).Remove(ServerName))
                    File.WriteAllText(path, YamlSerializer.Serialize(root));
                break;
        }
    }

    // A project path means a source checkout run through the SDK; without one, the bundled server.
    private (string Command, string[] Args) ServerCommand(string mcpProjectPath)
    {
        if (!string.IsNullOrWhiteSpace(mcpProjectPath))
            return ("dotnet", ["run", "-c", "Release", "--project", mcpProjectPath]);

        return (BundledServerPath ?? throw new InvalidOperationException(
            "This build of Corvida has no bundled MCP server (it is a source build). Set the path to Corvida.Mcp.csproj."), []);
    }

    private static JsonArray JsonStringArray(params string[] values) =>
        new(values.Select(v => (JsonNode)JsonValue.Create(v)!).ToArray());

    private static JsonObject? JsonContainer(string path, string containerKey)
    {
        var root = ParseJsonObject(path);
        return root?[containerKey] as JsonObject;
    }

    private static void WriteJson(string path, string containerKey, JsonObject entry)
    {
        var root = ParseJsonObject(path) ?? new JsonObject();
        if (root[containerKey] is not JsonObject container)
        {
            container = new JsonObject();
            root[containerKey] = container;
        }
        container[ServerName] = entry;
        File.WriteAllText(path, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void RemoveJsonEntry(string path, string containerKey)
    {
        var root = ParseJsonObject(path);
        if (root?[containerKey] is JsonObject container && container.Remove(ServerName))
            File.WriteAllText(path, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private static JsonObject? ParseJsonObject(string path)
    {
        if (!File.Exists(path)) return null;
        var text = File.ReadAllText(path);
        if (string.IsNullOrWhiteSpace(text)) return null;
        return JsonNode.Parse(text) as JsonObject;
    }

    private static Dictionary<object, object> LoadYamlRoot(string path)
    {
        if (!File.Exists(path)) return new Dictionary<object, object>();
        var text = File.ReadAllText(path);
        if (string.IsNullOrWhiteSpace(text)) return new Dictionary<object, object>();
        return YamlDeserializer.Deserialize<Dictionary<object, object>>(text) ?? new Dictionary<object, object>();
    }

    private static Dictionary<object, object> YamlContainer(Dictionary<object, object> root)
    {
        if (root.TryGetValue("mcp_servers", out var existing) && existing is Dictionary<object, object> servers)
            return servers;

        servers = new Dictionary<object, object>();
        root["mcp_servers"] = servers;
        return servers;
    }
}
