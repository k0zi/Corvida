namespace Corvida.Services;

public interface IMcpInstallerService
{
    /// <summary>
    /// The MCP server shipped with this app (<c>/usr/bin/corvida-mcp</c> from the corvida package, or
    /// <c>mcp/Corvida.Mcp</c> beside the app), or null when running from a source build without one.
    /// </summary>
    string? BundledServerPath { get; }

    bool IsInstalled(McpTarget target, string configPath);

    /// <summary>
    /// Registers the server with <paramref name="target"/>. With a blank <paramref name="mcpProjectPath"/>
    /// the <see cref="BundledServerPath"/> is registered; otherwise <c>dotnet run</c> on that project.
    /// </summary>
    void Install(McpTarget target, string configPath, string mcpProjectPath);
    void Uninstall(McpTarget target, string configPath);
}
