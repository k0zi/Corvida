using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Corvida.Services;

namespace Corvida.ViewModels;

/// <summary>
/// The sign-in form shown over the whole window while the server requires authentication.
/// </summary>
public partial class LoginViewModel : ViewModelBase
{
    private readonly IAuthClient _auth;
    private readonly ITokenProvider _tokens;
    private readonly Func<Task> _onSignedIn;

    [ObservableProperty] private string _userName = string.Empty;
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private bool _isBusy;

    public LoginViewModel(IAuthClient auth, ITokenProvider tokens, Func<Task> onSignedIn)
    {
        _auth = auth;
        _tokens = tokens;
        _onSignedIn = onSignedIn;
    }

    // Clearing the error as soon as the user edits either field stops a stale "wrong password"
    // sitting under a corrected entry.
    partial void OnUserNameChanged(string value) => ErrorMessage = null;
    partial void OnPasswordChanged(string value) => ErrorMessage = null;

    [RelayCommand]
    private async Task SignIn()
    {
        if (IsBusy) return;

        if (string.IsNullOrWhiteSpace(UserName) || string.IsNullOrEmpty(Password))
        {
            ErrorMessage = "Enter your username and password.";
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var tokens = await _auth.SignInAsync(UserName.Trim(), Password);
            await _tokens.SignInAsync(tokens);

            Password = string.Empty;
            await _onSignedIn();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            Password = string.Empty;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
