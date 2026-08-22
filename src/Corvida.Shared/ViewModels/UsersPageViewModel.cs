using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using Corvida.Models;
using Corvida.Services;
using Material.Icons;

namespace Corvida.ViewModels;

/// <summary>
/// Administrator-only account management. Follows the same list/editor shape as the Agents page.
/// </summary>
public partial class UsersPageViewModel : PageBase, IAdminOnlyPage
{
    private readonly IUserAdminService _userService;
    private readonly IDialogService _dialogService;

    private readonly Stack<ViewModelBase> _navStack = new();
    private readonly UsersListViewModel _listVm;

    [ObservableProperty]
    private ViewModelBase _currentViewModel = null!;

    public override string MenuTitle => "Users";
    public override MaterialIconKind Icon => MaterialIconKind.AccountKey;
    public override int DisplayOrder => 90;

    public UsersPageViewModel(IUserAdminService userService, IDialogService dialogService)
    {
        _userService = userService;
        _dialogService = dialogService;

        _listVm = new UsersListViewModel(userService, dialogService, NavigateToUserEditor);
        _navStack.Push(_listVm);
        CurrentViewModel = _listVm;
    }

    /// <summary>Loads the list. Called when the page becomes reachable, not in the constructor —
    /// the API rejects these calls until an administrator has signed in.</summary>
    public System.Threading.Tasks.Task RefreshAsync() => _listVm.LoadAsync();

    private void NavigateTo(ViewModelBase vm)
    {
        _navStack.Push(vm);
        CurrentViewModel = vm;
    }

    private void GoBack()
    {
        if (_navStack.Count <= 1) return;
        _navStack.Pop();
        CurrentViewModel = _navStack.Peek();
    }

    private void NavigateToUserEditor(UserAccount user)
    {
        var editorVm = new UserEditorViewModel(
            user, _userService, _dialogService,
            onSaved: savedUser =>
            {
                _ = _listVm.LoadAsync();
                GoBack();
            },
            onBack: GoBack);

        NavigateTo(editorVm);
    }
}
