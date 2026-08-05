using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Corvida.Models;
using SukiUI.Controls;

namespace Corvida.Views.Dialogs;

public partial class BoardMembersDialog : SukiWindow
{
    public List<string>? Result { get; private set; }

    private readonly ObservableCollection<Agent> _available;
    private readonly ObservableCollection<Agent> _members;

    public BoardMembersDialog(IReadOnlyList<Agent> allAgents, List<string> currentMemberIds)
    {
        InitializeComponent();

        _members = new ObservableCollection<Agent>(currentMemberIds
            .Select(id => allAgents.FirstOrDefault(a => a.Id == id))
            .Where(a => a is not null)
            .Select(a => a!));
        _available = new ObservableCollection<Agent>(
            allAgents.Where(a => !currentMemberIds.Contains(a.Id)));

        this.FindControl<ListBox>("AvailableList")!.ItemsSource = _available;
        this.FindControl<ListBox>("MembersList")!.ItemsSource = _members;
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void Add_Click(object? sender, RoutedEventArgs e)
    {
        if (this.FindControl<ListBox>("AvailableList")!.SelectedItem is not Agent agent) return;
        _available.Remove(agent);
        _members.Add(agent);
    }

    private void Remove_Click(object? sender, RoutedEventArgs e)
    {
        if (this.FindControl<ListBox>("MembersList")!.SelectedItem is not Agent agent) return;
        _members.Remove(agent);
        _available.Add(agent);
    }

    private void MoveUp_Click(object? sender, RoutedEventArgs e)
    {
        if (this.FindControl<ListBox>("MembersList")!.SelectedItem is not Agent agent) return;
        var idx = _members.IndexOf(agent);
        if (idx <= 0) return;
        _members.Move(idx, idx - 1);
    }

    private void MoveDown_Click(object? sender, RoutedEventArgs e)
    {
        if (this.FindControl<ListBox>("MembersList")!.SelectedItem is not Agent agent) return;
        var idx = _members.IndexOf(agent);
        if (idx < 0 || idx >= _members.Count - 1) return;
        _members.Move(idx, idx + 1);
    }

    private void Ok_Click(object? sender, RoutedEventArgs e)
    {
        Result = _members.Select(a => a.Id).ToList();
        Close();
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close();
}
