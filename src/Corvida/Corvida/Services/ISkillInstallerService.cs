using System.Threading.Tasks;

namespace Corvida.Services;

public interface ISkillInstallerService
{
    bool IsInstalled(string targetRoot);
    Task InstallAsync(string targetRoot);
    Task UninstallAsync(string targetRoot);
}
