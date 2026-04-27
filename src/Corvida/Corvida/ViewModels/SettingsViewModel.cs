using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Corvida.Services;
using Material.Icons;

namespace Corvida.ViewModels;

public partial class SettingsViewModel : PageBase
{
    private readonly ISettingsService _settingsService;

    [ObservableProperty] private string _dataPath = string.Empty;

    public override string MenuTitle => "Settings";
    public override MaterialIconKind Icon => MaterialIconKind.Cog;
    public override int DisplayOrder => 99;

    public SettingsViewModel(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        DataPath = settingsService.Settings.DataPath;
    }

    [RelayCommand]
    private async Task Save()
    {
        _settingsService.Settings.DataPath = DataPath.Trim();
        await _settingsService.SaveAsync();
    }

    [RelayCommand]
    private async Task BrowseFolder()
    {
        // Avalonia folder picker requires TopLevel - handled in code-behind via FolderPicker event
        await Task.CompletedTask;
    }
}
