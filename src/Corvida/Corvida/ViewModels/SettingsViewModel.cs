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
    private readonly ISkillInstallerService _skillInstallerService;
    private Func<Task>? _onSaved;

    [ObservableProperty] private string _dataPath = string.Empty;
    [ObservableProperty] private string _serverUrl = string.Empty;
    [ObservableProperty] private bool _isLocalFolder;
    [ObservableProperty] private bool _isServerHosted;

    [ObservableProperty] private string _claudeCodeSkillsPath = "~/.claude/skills";
    [ObservableProperty] private string _opencodeSkillsPath = "~/.config/opencode/skills";
    [ObservableProperty] private string _hermesSkillsPath = "~/.hermes/skills";
    [ObservableProperty] private bool _claudeCodeSkillsInstalled;
    [ObservableProperty] private bool _opencodeSkillsInstalled;
    [ObservableProperty] private bool _hermesSkillsInstalled;

    public override string MenuTitle => "Settings";
    public override MaterialIconKind Icon => MaterialIconKind.Cog;
    public override int DisplayOrder => 99;

    public SettingsViewModel(ISettingsService settingsService, IDialogService dialogService, IExportService exportService, ISkillInstallerService skillInstallerService)
    {
        _settingsService = settingsService;
        _dialogService = dialogService;
        _exportService = exportService;
        _skillInstallerService = skillInstallerService;

        DataPath = settingsService.Settings.DataPath;
        ServerUrl = settingsService.Settings.ServerUrl ?? string.Empty;
        _isLocalFolder = settingsService.Settings.StorageMode == StorageMode.LocalFolder;
        _isServerHosted = settingsService.Settings.StorageMode == StorageMode.ServerHosted;

        _claudeCodeSkillsInstalled = _skillInstallerService.IsInstalled(ClaudeCodeSkillsPath);
        _opencodeSkillsInstalled = _skillInstallerService.IsInstalled(OpencodeSkillsPath);
        _hermesSkillsInstalled = _skillInstallerService.IsInstalled(HermesSkillsPath);
    }

    partial void OnClaudeCodeSkillsPathChanged(string value) => ClaudeCodeSkillsInstalled = _skillInstallerService.IsInstalled(value);
    partial void OnOpencodeSkillsPathChanged(string value) => OpencodeSkillsInstalled = _skillInstallerService.IsInstalled(value);
    partial void OnHermesSkillsPathChanged(string value) => HermesSkillsInstalled = _skillInstallerService.IsInstalled(value);

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

    [RelayCommand]
    private Task ToggleClaudeCodeSkills() => ToggleSkillInstallAsync(ClaudeCodeSkillsPath, installed => ClaudeCodeSkillsInstalled = installed);

    [RelayCommand]
    private Task ToggleOpencodeSkills() => ToggleSkillInstallAsync(OpencodeSkillsPath, installed => OpencodeSkillsInstalled = installed);

    [RelayCommand]
    private Task ToggleHermesSkills() => ToggleSkillInstallAsync(HermesSkillsPath, installed => HermesSkillsInstalled = installed);

    private async Task ToggleSkillInstallAsync(string path, Action<bool> setInstalled)
    {
        try
        {
            if (_skillInstallerService.IsInstalled(path))
                await _skillInstallerService.UninstallAsync(path);
            else
                await _skillInstallerService.InstallAsync(path);
            setInstalled(_skillInstallerService.IsInstalled(path));
        }
        catch (System.Exception ex)
        {
            await _dialogService.ShowConfirmDialogAsync("Skill Install Failed", ex.Message);
        }
    }
}
