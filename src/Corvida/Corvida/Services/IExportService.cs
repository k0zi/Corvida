using System.Threading.Tasks;

namespace Corvida.Services;

public interface IExportService
{
    Task ExportAsync(string targetFolder);
}
