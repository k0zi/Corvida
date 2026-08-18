using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Corvida.Models;
using Corvida.ViewModels;

namespace Corvida.Views;

public partial class SkillsListView : UserControl
{
    public SkillsListView() => InitializeComponent();

    private void SkillCard_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if ((e.Source as Control)?.FindAncestorOfType<Button>() is not null) return;

        if (sender is Control { DataContext: Skill skill } &&
            DataContext is SkillsListViewModel vm)
            vm.EditSkillCommand.Execute(skill);
    }
}
