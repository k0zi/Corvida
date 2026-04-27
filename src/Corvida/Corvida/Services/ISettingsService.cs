using System.Threading.Tasks;
using Corvida.Models;

namespace Corvida.Services;

public interface ISettingsService
{
    AppSettings Settings { get; }
    Task LoadAsync();
    Task SaveAsync();
}
