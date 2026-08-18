using System.Threading.Tasks;

namespace Corvida.Services;

public interface IAgentInstallerService
{
    Task<bool> IsInstalledAsync(string targetRoot);
    Task InstallAsync(string targetRoot);
    Task UninstallAsync(string targetRoot);
}
