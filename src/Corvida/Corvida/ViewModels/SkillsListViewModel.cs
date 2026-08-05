using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Corvida.Models;
using Corvida.Services;

namespace Corvida.ViewModels;

public partial class SkillsListViewModel : ViewModelBase
{
    private readonly ISkillService _skillService;
    private readonly IDialogService _dialogService;
    private readonly Action<Skill> _onEditSkill;

    [ObservableProperty]
    private ObservableCollection<Skill> _skills = new();

    public SkillsListViewModel(ISkillService skillService, IDialogService dialogService, Action<Skill> onEditSkill)
    {
        _skillService = skillService;
        _dialogService = dialogService;
        _onEditSkill = onEditSkill;
    }

    public async Task LoadAsync()
    {
        var skills = await _skillService.GetSkillsAsync();
        Skills = new ObservableCollection<Skill>(skills);
    }

    [RelayCommand]
    private async Task AddSkill()
    {
        var name = await _dialogService.ShowInputDialogAsync("Add Skill", "Name:", "e.g. My Custom Skill");
        if (name is null) return;

        var skill = await _skillService.CreateSkillAsync(name);
        Skills.Add(skill);
        _onEditSkill(skill);
    }

    [RelayCommand]
    private void EditSkill(Skill skill) => _onEditSkill(skill);

    [RelayCommand]
    private async Task DeleteSkill(Skill skill)
    {
        var confirmed = await _dialogService.ShowConfirmDialogAsync(
            "Delete Skill",
            $"Permanently delete skill '{skill.Name}'? This cannot be undone.");
        if (!confirmed) return;

        await _skillService.DeleteSkillAsync(skill.Id);
        Skills.Remove(skill);
    }
}
