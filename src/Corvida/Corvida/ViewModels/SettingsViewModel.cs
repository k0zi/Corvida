using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Corvida.Models;
using Corvida.Services;
using Material.Icons;

namespace Corvida.ViewModels;

public partial class SettingsViewModel : PageBase
{
    private readonly ISettingsService _settingsService;
    private readonly IDialogService _dialogService;
    private readonly IExportService _exportService;
    private Func<Task>? _onSaved;

    [ObservableProperty] private string _dataPath = string.Empty;
    [ObservableProperty] private string _serverUrl = string.Empty;
    [ObservableProperty] private bool _isLocalFolder;
    [ObservableProperty] private bool _isServerHosted;

    public override string MenuTitle => "Settings";
    public override MaterialIconKind Icon => MaterialIconKind.Cog;
    public override int DisplayOrder => 99;

    public SettingsViewModel(ISettingsService settingsService, IDialogService dialogService, IExportService exportService)
    {
        _settingsService = settingsService;
        _dialogService = dialogService;
        _exportService = exportService;

        DataPath = settingsService.Settings.DataPath;
        ServerUrl = settingsService.Settings.ServerUrl ?? string.Empty;
        _isLocalFolder = settingsService.Settings.StorageMode == StorageMode.LocalFolder;
        _isServerHosted = settingsService.Settings.StorageMode == StorageMode.ServerHosted;
    }

    partial void OnIsLocalFolderChanged(bool value)
    {
        if (value) _isServerHosted = false;
        OnPropertyChanged(nameof(IsServerHosted));
    }

    partial void OnIsServerHostedChanged(bool value)
    {
        if (value) _isLocalFolder = false;
        OnPropertyChanged(nameof(IsLocalFolder));
    }

    public void SetOnSaved(Func<Task> onSaved) => _onSaved = onSaved;

    [RelayCommand]
    private async Task Save()
    {
        _settingsService.Settings.DataPath = DataPath.Trim();
        _settingsService.Settings.ServerUrl = ServerUrl.Trim();
        _settingsService.Settings.StorageMode = IsServerHosted ? StorageMode.ServerHosted : StorageMode.LocalFolder;
        await _settingsService.SaveAsync();
        if (_onSaved != null) await _onSaved();
    }

    public async Task ExportToFolderAsync(string folder)
    {
        try
        {
            await _exportService.ExportAsync(folder);
            await _dialogService.ShowConfirmDialogAsync("Export Complete", $"All boards and tasks exported to:\n{folder}");
        }
        catch (System.Exception ex)
        {
            await _dialogService.ShowConfirmDialogAsync("Export Failed", ex.Message);
        }
    }
}
