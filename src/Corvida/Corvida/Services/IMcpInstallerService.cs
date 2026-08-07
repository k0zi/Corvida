namespace Corvida.Services;

public interface IMcpInstallerService
{
    bool IsInstalled(McpTarget target, string configPath);
    void Install(McpTarget target, string configPath, string mcpProjectPath);
    void Uninstall(McpTarget target, string configPath);
}
