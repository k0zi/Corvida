using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Corvida.Models;

namespace Corvida.Views.Dialogs;

public partial class BoardMembersDialogContent : UserControl
{
    private readonly Action<List<string>?> _complete;
    private readonly ObservableCollection<Agent> _available;
    private readonly ObservableCollection<Agent> _members;

    public BoardMembersDialogContent(IReadOnlyList<Agent> allAgents, List<string> currentMemberIds, Action<List<string>?> complete)
    {
        InitializeComponent();
        _complete = complete;

        _members = new ObservableCollection<Agent>(currentMemberIds
            .Select(id => allAgents.FirstOrDefault(a => a.Id == id))
            .Where(a => a is not null)
            .Select(a => a!));
        _available = new ObservableCollection<Agent>(
            allAgents.Where(a => !currentMemberIds.Contains(a.Id)));

        AvailableList.ItemsSource = _available;
        MembersList.ItemsSource = _members;
    }

    private void Add_Click(object? sender, RoutedEventArgs e)
    {
        if (AvailableList.SelectedItem is not Agent agent) return;
        _available.Remove(agent);
        _members.Add(agent);
    }

    private void Remove_Click(object? sender, RoutedEventArgs e)
    {
        if (MembersList.SelectedItem is not Agent agent) return;
        _members.Remove(agent);
        _available.Add(agent);
    }

    private void MoveUp_Click(object? sender, RoutedEventArgs e)
    {
        if (MembersList.SelectedItem is not Agent agent) return;
        var idx = _members.IndexOf(agent);
        if (idx <= 0) return;
        _members.Move(idx, idx - 1);
    }

    private void MoveDown_Click(object? sender, RoutedEventArgs e)
    {
        if (MembersList.SelectedItem is not Agent agent) return;
        var idx = _members.IndexOf(agent);
        if (idx < 0 || idx >= _members.Count - 1) return;
        _members.Move(idx, idx + 1);
    }

    private void Ok_Click(object? sender, RoutedEventArgs e) => _complete(_members.Select(a => a.Id).ToList());

    private void Cancel_Click(object? sender, RoutedEventArgs e) => _complete(null);
}
