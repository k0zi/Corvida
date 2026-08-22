using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using Corvida.Models;
using Corvida.Services;
using Material.Icons;

namespace Corvida.ViewModels;

public partial class AgentsPageViewModel : PageBase
{
    private readonly IAgentService _agentService;

    private readonly Stack<ViewModelBase> _navStack = new();
    private readonly AgentsListViewModel _listVm;

    [ObservableProperty]
    private ViewModelBase _currentViewModel = null!;

    public override string MenuTitle => "Agents";
    public override MaterialIconKind Icon => MaterialIconKind.AccountGroup;
    public override int DisplayOrder => 10;

    public AgentsPageViewModel(IAgentService agentService, IDialogService dialogService)
    {
        _agentService = agentService;

        _listVm = new AgentsListViewModel(agentService, dialogService, NavigateToAgentEditor);
        _navStack.Push(_listVm);
        CurrentViewModel = _listVm;

        // In local mode the store is readable immediately. In server mode the host calls
        // RefreshAsync once it can reach the API — see the note there.
        _ = _listVm.LoadAsync();
    }

    /// <summary>
    /// Reloads the list.
    /// </summary>
    /// <remarks>
    /// The constructor-time load runs before the host has finished seeding the built-in agents
    /// (and, in server mode, before it knows whether a token is needed), so it can legitimately
    /// come back empty. The host calls this afterwards.
    /// </remarks>
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

    private void NavigateToAgentEditor(Agent agent)
    {
        var editorVm = new AgentEditorViewModel(
            agent, _agentService,
            onSaved: savedAgent =>
            {
                _ = _listVm.LoadAsync();
                GoBack();
            },
            onBack: GoBack);

        NavigateTo(editorVm);
    }
}
