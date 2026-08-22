using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using Corvida.Models;

namespace Corvida.ViewModels;

/// <summary>
/// The "require login" switch on the Settings page.
/// </summary>
public partial class SettingsViewModel
{
    public void SetOnAuthChanged(Func<Task> onAuthChanged) => _onAuthChanged = onAuthChanged;

    /// <summary>Reads the server's current auth state so the card can show the right button.</summary>
    public async Task RefreshAuthStateAsync()
    {
        AuthMessage = null;

        // Nothing to authenticate against in local mode.
        if (_settingsService.Settings.StorageMode != StorageMode.ServerHosted)
        {
            AuthStateKnown = false;
            return;
        }

        try
        {
            var status = await _authClient.GetStatusAsync();
            AuthRequired = status.AuthRequired;
            AuthStateKnown = true;

            await RefreshApiKeysAsync();
        }
        catch (Exception ex)
        {
            AuthStateKnown = false;
            AuthMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task EnableAuth()
    {
        var email = await _dialogService.ShowInputDialogAsync(
            "Require Login",
            "Email address for the first administrator:",
            "you@example.com");
        if (email is null) return;

        var password = await _dialogService.ShowPasswordDialogAsync(
            "Require Login",
            $"Choose a password for {email} (at least 8 characters).",
            "Continue");
        if (password is null) return;

        var confirmation = await _dialogService.ShowPasswordDialogAsync(
            "Require Login",
            "Re-enter the password to confirm.",
            "Turn on login");
        if (confirmation is null) return;

        if (password != confirmation)
        {
            await _dialogService.ShowConfirmDialogAsync("Require Login", "The passwords did not match.");
            return;
        }

        try
        {
            await _authClient.EnableAuthAsync(email, password, email);
            AuthRequired = true;
            AuthMessage = null;

            if (_onAuthChanged is not null) await _onAuthChanged();
        }
        catch (Exception ex)
        {
            await _dialogService.ShowConfirmDialogAsync("Require Login Failed", ex.Message);
        }
    }

    [RelayCommand]
    private async Task DisableAuth()
    {
        var confirmed = await _dialogService.ShowConfirmDialogAsync(
            "Turn Off Login",
            "This deletes every user account, role and access token on the server, and anyone who " +
            "can reach it will have full access again. Boards, tasks and agents are not affected. " +
            "This cannot be undone.");
        if (!confirmed) return;

        var password = await _dialogService.ShowPasswordDialogAsync(
            "Turn Off Login",
            "Enter your own password to confirm.",
            "Delete all users");
        if (password is null) return;

        try
        {
            await _authClient.DisableAuthAsync(password);
            AuthRequired = false;
            AuthMessage = null;

            if (_onAuthChanged is not null) await _onAuthChanged();
        }
        catch (Exception ex)
        {
            await _dialogService.ShowConfirmDialogAsync("Turn Off Login Failed", ex.Message);
        }
    }
}
