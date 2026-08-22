using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Corvida.Models;
using Corvida.Services;

namespace Corvida.ViewModels;

public partial class UserEditorViewModel : ViewModelBase
{
    private readonly IUserAdminService _userService;
    private readonly IDialogService _dialogService;
    private readonly Action<UserAccount> _onSaved;
    private readonly Action _onBack;
    private readonly UserAccount _user;

    [ObservableProperty] private string _userName = string.Empty;
    [ObservableProperty] private string _displayName = string.Empty;
    [ObservableProperty] private string _role = UserRoles.User;
    [ObservableProperty] private bool _isLockedOut;
    [ObservableProperty] private string? _errorMessage;

    public IReadOnlyList<string> Roles { get; } = UserRoles.All;

    public UserEditorViewModel(
        UserAccount user,
        IUserAdminService userService,
        IDialogService dialogService,
        Action<UserAccount> onSaved,
        Action onBack)
    {
        _user = user;
        _userService = userService;
        _dialogService = dialogService;
        _onSaved = onSaved;
        _onBack = onBack;

        UserName = user.UserName;
        DisplayName = user.DisplayName;
        Role = user.Role;
        IsLockedOut = user.IsLockedOut;
    }

    [RelayCommand]
    private async Task Save()
    {
        _user.DisplayName = DisplayName.Trim();
        _user.Role = Role;

        try
        {
            var saved = await _userService.SaveUserAsync(_user);
            ErrorMessage = null;
            _onSaved(saved);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task ResetPassword()
    {
        var password = await _dialogService.ShowPasswordDialogAsync(
            "Reset Password",
            $"Choose a new password for {UserName} (at least 8 characters). They are not told about " +
            "this change, so pass it on yourself.",
            "Reset");
        if (password is null) return;

        try
        {
            await _userService.SetPasswordAsync(_user.Id, currentPassword: null, password);
            ErrorMessage = null;
            await _dialogService.ShowConfirmDialogAsync("Password Reset", $"The password for {UserName} was changed.");
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private void GoBack() => _onBack();
}
