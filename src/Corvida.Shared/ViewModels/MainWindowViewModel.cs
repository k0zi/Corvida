using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Corvida.Models;
using Corvida.Services;
using Material.Icons;
using SukiUI;
using SukiUI.Dialogs;

namespace Corvida.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IAuthClient _auth;
    private readonly ITokenProvider _tokens;
    private readonly ISettingsService _settings;

    public ObservableCollection<PageBase> Pages { get; }
    public ISukiDialogManager DialogManager { get; }

    [ObservableProperty] private MaterialIconKind _themeIcon = MaterialIconKind.WeatherSunny;
    [ObservableProperty] private PageBase? _activePage;

    /// <summary>False while the sign-in overlay covers the window.</summary>
    [ObservableProperty] private bool _isSignedIn = true;

    /// <summary>Whether the server currently demands authentication at all.</summary>
    [ObservableProperty] private bool _authRequired;

    [ObservableProperty] private LoginViewModel? _login;
    [ObservableProperty] private SignedInUser? _currentUser;

    /// <summary>Raised after a successful sign-in, so the host can start work that needs a token.</summary>
    public event Func<Task>? SignedIn;

    /// <summary>Raised after signing out, so the host can tear that work down.</summary>
    public event Func<Task>? SignedOutOfServer;

    /// <summary>
    /// Every registered page, including ones currently hidden from the menu. Hosts wire their
    /// callbacks against this rather than <see cref="Pages"/>, which changes with the signed-in
    /// user's role.
    /// </summary>
    public IReadOnlyList<PageBase> AllPages { get; }

    public MainWindowViewModel(
        IEnumerable<PageBase> pages,
        ISukiDialogManager dialogManager,
        IAuthClient auth,
        ITokenProvider tokens,
        ISettingsService settings)
    {
        AllPages = [.. pages.OrderBy(p => p.DisplayOrder)];
        Pages = new ObservableCollection<PageBase>(AllPages);
        DialogManager = dialogManager;
        _auth = auth;
        _tokens = tokens;
        _settings = settings;

        var theme = SukiTheme.GetInstance();
        UpdateThemeIcon(theme.ActiveBaseTheme);
        theme.OnBaseThemeChanged += variant => UpdateThemeIcon(variant);

        // A refresh failing mid-session must drop straight back to the overlay rather than
        // leaving the UI showing stale data it can no longer refresh.
        _tokens.SignedOut += () => _ = ShowLoginAsync();
    }

    /// <summary>
    /// Decides whether to show the app or the sign-in overlay. Called once at startup.
    /// </summary>
    public async Task InitializeAsync()
    {
        // Local storage has no server to authenticate against, so nothing changes.
        if (_settings.Settings.StorageMode != StorageMode.ServerHosted)
        {
            AuthRequired = false;
            IsSignedIn = true;
            UpdateVisiblePages();
            return;
        }

        try
        {
            var status = await _auth.GetStatusAsync();
            AuthRequired = status.AuthRequired;
        }
        catch (Exception)
        {
            // The server being unreachable is not a reason to lock the user out of an app that
            // still works offline in local mode — behave as before and let the pages report it.
            AuthRequired = false;
        }

        UpdateVisiblePages();

        if (!AuthRequired)
        {
            IsSignedIn = true;
            return;
        }

        await ShowLoginAsync();
    }

    /// <summary>Re-checks the server's auth state, e.g. after it was switched on or off remotely.</summary>
    public async Task OnAuthStateChangedAsync(bool authRequired)
    {
        AuthRequired = authRequired;

        if (!authRequired)
        {
            CurrentUser = null;
            IsSignedIn = true;
            Login = null;
            return;
        }

        await ShowLoginAsync();
    }

    /// <summary>
    /// Rebuilds the menu. Admin-only pages are removed rather than disabled, because a menu entry
    /// that always answers 403 is worse than no entry at all.
    /// </summary>
    private void UpdateVisiblePages()
    {
        var visible = AllPages.Where(IsVisibleToCurrentUser).ToList();

        Pages.Clear();
        foreach (var page in visible) Pages.Add(page);

        if (ActivePage is null || !visible.Contains(ActivePage))
            ActivePage = visible.FirstOrDefault();
    }

    private bool IsVisibleToCurrentUser(PageBase page)
    {
        if (page is not IAdminOnlyPage) return true;

        // With authentication off there is no user store to manage.
        return AuthRequired && CurrentUser?.IsAdmin == true;
    }

    partial void OnCurrentUserChanged(SignedInUser? value) => UpdateVisiblePages();

    partial void OnAuthRequiredChanged(bool value) => UpdateVisiblePages();

    private Task ShowLoginAsync()
    {
        CurrentUser = null;
        IsSignedIn = false;
        Login = new LoginViewModel(_auth, _tokens, OnSignedInAsync);

        return Task.CompletedTask;
    }

    private async Task OnSignedInAsync()
    {
        try
        {
            CurrentUser = await _auth.GetCurrentUserAsync();
        }
        catch (Exception)
        {
            // Identity details are a nicety; a failure here must not block a successful sign-in.
            CurrentUser = null;
        }

        IsSignedIn = true;
        Login = null;

        if (SignedIn is not null) await SignedIn();
    }

    [RelayCommand]
    private async Task SignOut()
    {
        await _tokens.SignOutAsync();

        if (SignedOutOfServer is not null) await SignedOutOfServer();

        await ShowLoginAsync();
    }

    [RelayCommand]
    private void ToggleTheme() => SukiTheme.GetInstance().SwitchBaseTheme();

    private void UpdateThemeIcon(ThemeVariant variant) =>
        ThemeIcon = variant == ThemeVariant.Dark
            ? MaterialIconKind.LightbulbVariant
            : MaterialIconKind.LightbulbOnOutline;
}
