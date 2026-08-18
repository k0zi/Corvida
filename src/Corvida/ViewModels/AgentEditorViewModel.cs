using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Corvida.Models;
using Corvida.Services;

namespace Corvida.ViewModels;

public partial class AgentEditorViewModel : ViewModelBase
{
    private readonly IAgentService _agentService;
    private readonly Action<Agent> _onSaved;
    private readonly Action _onBack;
    private readonly Agent _agent;

    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private string _personality = string.Empty;
    [ObservableProperty] private string _color = "#4C6EF5";
    [ObservableProperty] private string? _avatarDataUri;

    public IReadOnlyList<string> ColorPalette { get; } = new[]
    {
        "#4C6EF5", "#F03E3E", "#F76707", "#F59F00", "#66A80F",
        "#0CA678", "#0C8599", "#1971C2", "#7048E8", "#AE3EC9",
        "#D6336C", "#495057",
    };

    public AgentEditorViewModel(Agent agent, IAgentService agentService, Action<Agent> onSaved, Action onBack)
    {
        _agent = agent;
        _agentService = agentService;
        _onSaved = onSaved;
        _onBack = onBack;

        Name = agent.Name;
        Description = agent.Description;
        Personality = agent.Personality;
        Color = agent.Color;
        AvatarDataUri = agent.AvatarDataUri;
    }

    [RelayCommand]
    private async Task Save()
    {
        _agent.Name = Name.Trim();
        _agent.Description = Description.Trim();
        _agent.Personality = Personality;
        _agent.Color = Color;
        _agent.AvatarDataUri = AvatarDataUri;
        await _agentService.SaveAgentAsync(_agent);
        _onSaved(_agent);
    }

    [RelayCommand]
    private void SelectColor(string hex) => Color = hex;

    [RelayCommand]
    private void GoBack() => _onBack();
}
