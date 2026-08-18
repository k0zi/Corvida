using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using Corvida.Models;
using Corvida.Services;
using Material.Icons;

namespace Corvida.ViewModels;

public partial class SkillsPageViewModel : PageBase
{
    private readonly ISkillService _skillService;

    private readonly Stack<ViewModelBase> _navStack = new();
    private readonly SkillsListViewModel _listVm;

    [ObservableProperty]
    private ViewModelBase _currentViewModel = null!;

    public override string MenuTitle => "Skills";
    public override MaterialIconKind Icon => MaterialIconKind.Toolbox;
    public override int DisplayOrder => 20;

    public SkillsPageViewModel(ISkillService skillService, IDialogService dialogService)
    {
        _skillService = skillService;

        _listVm = new SkillsListViewModel(skillService, dialogService, NavigateToSkillEditor);
        _navStack.Push(_listVm);
        CurrentViewModel = _listVm;

        _ = _listVm.LoadAsync();
    }

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

    private void NavigateToSkillEditor(Skill skill)
    {
        var editorVm = new SkillEditorViewModel(
            skill, _skillService,
            onSaved: savedSkill =>
            {
                _ = _listVm.LoadAsync();
                GoBack();
            },
            onBack: GoBack);

        NavigateTo(editorVm);
    }
}
