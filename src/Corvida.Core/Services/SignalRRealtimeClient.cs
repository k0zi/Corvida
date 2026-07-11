using System;
using System.Threading.Tasks;
using Corvida.Models;
using Microsoft.AspNetCore.SignalR.Client;

namespace Corvida.Services;

public class SignalRRealtimeClient(ISettingsService settings) : IRealtimeClient
{
    private string Base => (settings.Settings.ServerUrl ?? "http://localhost:5000").TrimEnd('/');

    private HubConnection? _connection;

    public event Action<Board>? BoardChanged;
    public event Action<string>? BoardDeleted;
    public event Action<string, KanbanTask>? TaskChanged;
    public event Action<string, string>? TaskDeleted;

    public async Task StartAsync()
    {
        if (settings.Settings.StorageMode != StorageMode.ServerHosted) return;
        if (_connection is not null) return;

        var connection = new HubConnectionBuilder()
            .WithUrl($"{Base}/hubs/kanban")
            .WithAutomaticReconnect()
            .Build();

        connection.On<Board>("BoardChanged", board => BoardChanged?.Invoke(board));
        connection.On<string>("BoardDeleted", boardId => BoardDeleted?.Invoke(boardId));
        connection.On<string, KanbanTask>("TaskChanged", (boardId, task) => TaskChanged?.Invoke(boardId, task));
        connection.On<string, string>("TaskDeleted", (boardId, taskId) => TaskDeleted?.Invoke(boardId, taskId));

        _connection = connection;

        try
        {
            await connection.StartAsync();
        }
        catch (Exception)
        {
            // Live updates are a convenience layer; the app must keep working via manual
            // refresh if the server/hub is unreachable. WithAutomaticReconnect will keep
            // retrying transient drops once connected.
        }
    }

    public async Task StopAsync()
    {
        if (_connection is null) return;

        await _connection.DisposeAsync();
        _connection = null;
    }
}
