using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Corvida.Models;
using Corvida.Services;

namespace Corvida.ViewModels;

public partial class UsersListViewModel : ViewModelBase
{
    private readonly IUserAdminService _userService;
    private readonly IDialogService _dialogService;
    private readonly Action<UserAccount> _onEditUser;

    [ObservableProperty] private ObservableCollection<UserAccount> _users = new();
    [ObservableProperty] private string? _errorMessage;

    public UsersListViewModel(
        IUserAdminService userService, IDialogService dialogService, Action<UserAccount> onEditUser)
    {
        _userService = userService;
        _dialogService = dialogService;
        _onEditUser = onEditUser;
    }

    public async Task LoadAsync()
    {
        try
        {
            Users = new ObservableCollection<UserAccount>(await _userService.GetUsersAsync());
            ErrorMessage = null;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task AddUser()
    {
        var email = await _dialogService.ShowInputDialogAsync("Add User", "Email address:", "you@example.com");
        if (email is null) return;

        var password = await _dialogService.ShowPasswordDialogAsync(
            "Add User", $"Choose a password for {email} (at least 8 characters).", "Create");
        if (password is null) return;

        var role = await _dialogService.ShowPickerDialogAsync("Add User", UserRoles.All);
        if (role is null) return;

        try
        {
            var created = await _userService.CreateUserAsync(email, password, email, role);
            Users.Add(created);
            _onEditUser(created);
        }
        catch (Exception ex)
        {
            await _dialogService.ShowConfirmDialogAsync("Could Not Add User", ex.Message);
        }
    }

    [RelayCommand]
    private void EditUser(UserAccount user) => _onEditUser(user);

    [RelayCommand]
    private async Task DeleteUser(UserAccount user)
    {
        var confirmed = await _dialogService.ShowConfirmDialogAsync(
            "Delete User",
            $"Permanently delete the account '{user.UserName}'? They will no longer be able to sign " +
            "in. Boards, tasks and agents are not affected. This cannot be undone.");
        if (!confirmed) return;

        try
        {
            await _userService.DeleteUserAsync(user.Id);
            Users.Remove(user);
        }
        catch (Exception ex)
        {
            await _dialogService.ShowConfirmDialogAsync("Could Not Delete User", ex.Message);
        }
    }
}
