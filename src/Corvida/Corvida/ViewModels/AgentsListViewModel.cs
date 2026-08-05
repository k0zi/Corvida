using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Corvida.Models;
using Corvida.Services;

namespace Corvida.ViewModels;

public partial class AgentsListViewModel : ViewModelBase
{
    private readonly IAgentService _agentService;
    private readonly IDialogService _dialogService;
    private readonly Action<Agent> _onEditAgent;

    [ObservableProperty]
    private ObservableCollection<Agent> _agents = new();

    public AgentsListViewModel(IAgentService agentService, IDialogService dialogService, Action<Agent> onEditAgent)
    {
        _agentService = agentService;
        _dialogService = dialogService;
        _onEditAgent = onEditAgent;
    }

    public async Task LoadAsync()
    {
        var agents = await _agentService.GetAgentsAsync();
        Agents = new ObservableCollection<Agent>(agents);
    }

    [RelayCommand]
    private async Task AddAgent()
    {
        var name = await _dialogService.ShowInputDialogAsync("Add Agent", "Name:", "e.g. Ada");
        if (name is null) return;

        var agent = await _agentService.CreateAgentAsync(name);
        Agents.Add(agent);
        _onEditAgent(agent);
    }

    [RelayCommand]
    private void EditAgent(Agent agent) => _onEditAgent(agent);

    [RelayCommand]
    private async Task DeleteAgent(Agent agent)
    {
        var confirmed = await _dialogService.ShowConfirmDialogAsync(
            "Delete Agent",
            $"Permanently delete agent '{agent.Name}'? They will be removed from every board's " +
            "membership. This cannot be undone.");
        if (!confirmed) return;

        try
        {
            await _agentService.DeleteAgentAsync(agent.Id);
            Agents.Remove(agent);
        }
        catch (InvalidOperationException ex)
        {
            await _dialogService.ShowConfirmDialogAsync("Cannot Delete Agent", ex.Message);
        }
    }
}
