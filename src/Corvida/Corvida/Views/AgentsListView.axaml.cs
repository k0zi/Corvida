using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Corvida.Models;
using Corvida.ViewModels;

namespace Corvida.Views;

public partial class AgentsListView : UserControl
{
    public AgentsListView() => InitializeComponent();

    private void AgentCard_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if ((e.Source as Control)?.FindAncestorOfType<Button>() is not null) return;

        if (sender is Control { DataContext: Agent agent } &&
            DataContext is AgentsListViewModel vm)
            vm.EditAgentCommand.Execute(agent);
    }
}
