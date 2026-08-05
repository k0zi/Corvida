using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Corvida.Models;
using Corvida.Services;

namespace Corvida.ViewModels;

public partial class SkillEditorViewModel : ViewModelBase
{
    private readonly ISkillService _skillService;
    private readonly Action<Skill> _onSaved;
    private readonly Action _onBack;
    private readonly Skill _skill;

    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private string _body = string.Empty;
    [ObservableProperty] private string _displayName = string.Empty;
    [ObservableProperty] private string _shortDescription = string.Empty;

    public SkillEditorViewModel(Skill skill, ISkillService skillService, Action<Skill> onSaved, Action onBack)
    {
        _skill = skill;
        _skillService = skillService;
        _onSaved = onSaved;
        _onBack = onBack;

        Name = skill.Name;
        Description = skill.Description;
        Body = skill.Body;
        DisplayName = skill.DisplayName;
        ShortDescription = skill.ShortDescription;
    }

    [RelayCommand]
    private async Task Save()
    {
        _skill.Name = Name.Trim();
        _skill.Description = Description.Trim();
        _skill.Body = Body;
        _skill.DisplayName = DisplayName.Trim();
        _skill.ShortDescription = ShortDescription.Trim();
        await _skillService.SaveSkillAsync(_skill);
        _onSaved(_skill);
    }

    [RelayCommand]
    private void GoBack() => _onBack();
}
