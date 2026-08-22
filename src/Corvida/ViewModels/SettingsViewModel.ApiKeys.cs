using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using Corvida.Services;

namespace Corvida.ViewModels;

/// <summary>
/// Access keys for non-interactive clients — in practice, the MCP server.
/// </summary>
public partial class SettingsViewModel
{
    /// <summary>Loads the key list, if the signed-in user is allowed to see it.</summary>
    public async Task RefreshApiKeysAsync()
    {
        if (!AuthRequired)
        {
            ApiKeys.Clear();
            CanManageApiKeys = false;
            return;
        }

        try
        {
            var keys = await _apiKeyService.GetKeysAsync();
            ApiKeys = new ObservableCollection<ApiKeyInfo>(keys);
            CanManageApiKeys = true;
        }
        catch (Exception)
        {
            // Not an administrator, or the server is unreachable — hide the section rather than
            // showing an error the user can do nothing about.
            CanManageApiKeys = false;
        }
    }

    [RelayCommand]
    private async Task CreateApiKey()
    {
        var name = await _dialogService.ShowInputDialogAsync(
            "New Access Key", "What is this key for?", "e.g. MCP server");
        if (name is null) return;

        try
        {
            var created = await _apiKeyService.CreateKeyAsync(name);
            NewApiKey = created.Key;
            await RefreshApiKeysAsync();
        }
        catch (Exception ex)
        {
            await _dialogService.ShowConfirmDialogAsync("Could Not Create Key", ex.Message);
        }
    }

    [RelayCommand]
    private async Task RevokeApiKey(ApiKeyInfo key)
    {
        var confirmed = await _dialogService.ShowConfirmDialogAsync(
            "Revoke Access Key",
            $"Revoke '{key.Name}'? Any client using it loses access immediately.");
        if (!confirmed) return;

        try
        {
            await _apiKeyService.RevokeKeyAsync(key.Id);
            await RefreshApiKeysAsync();
        }
        catch (Exception ex)
        {
            await _dialogService.ShowConfirmDialogAsync("Could Not Revoke Key", ex.Message);
        }
    }

    /// <summary>Copies the freshly created key into settings so the MCP server picks it up.</summary>
    [RelayCommand]
    private async Task UseNewKeyForMcp()
    {
        if (string.IsNullOrWhiteSpace(NewApiKey)) return;

        _settingsService.Settings.ApiKey = NewApiKey;
        await _settingsService.SaveAsync();

        await _dialogService.ShowConfirmDialogAsync(
            "Key Saved",
            "The MCP server will use this key the next time it starts. It is stored in " +
            "settings.json in plain text, so treat that file as a credential.");
    }

    [RelayCommand]
    private void DismissNewKey() => NewApiKey = null;
}
