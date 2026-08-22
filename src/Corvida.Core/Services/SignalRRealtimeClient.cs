using System;
using System.Threading;
using System.Threading.Tasks;
using Corvida.Models;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;

namespace Corvida.Services;

public class SignalRRealtimeClient(
    ISettingsService settings,
    ITokenProvider tokens,
    ILogger<SignalRRealtimeClient> logger) : IRealtimeClient
{
    private string Base => (settings.Settings.ServerUrl ?? "http://localhost:5083").TrimEnd('/');

    // StartAsync/StopAsync are driven by UI events (sign-in, sign-out, settings save), so they
    // can overlap; the connection field must not be mutated by two of them at once.
    private readonly SemaphoreSlim _gate = new(1, 1);

    private HubConnection? _connection;

    public event Action<Board>? BoardChanged;
    public event Action<string>? BoardDeleted;
    public event Action<string, KanbanTask>? TaskChanged;
    public event Action<string, string>? TaskDeleted;
    public event Action<Agent>? AgentChanged;
    public event Action<string>? AgentDeleted;
    public event Action<bool>? AuthStateChanged;

    public async Task StartAsync()
    {
        if (settings.Settings.StorageMode != StorageMode.ServerHosted) return;

        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            // Already live (or reconnecting on its own) — nothing to do.
            if (_connection is { State: not HubConnectionState.Disconnected }) return;

            // A previous attempt left a dead connection behind: drop it so we can retry.
            // WithAutomaticReconnect only covers drops *after* a successful connect, so without
            // this a server that was down at launch would never be picked up again.
            if (_connection is not null)
            {
                await _connection.DisposeAsync().ConfigureAwait(false);
                _connection = null;
            }

            var connection = BuildConnection();

            try
            {
                await connection.StartAsync().ConfigureAwait(false);
                _connection = connection;
            }
            catch (Exception ex)
            {
                // Live updates are a convenience layer; the app must keep working via manual
                // refresh if the server/hub is unreachable. Leaving _connection null means the
                // next StartAsync/RestartAsync tries again instead of giving up for good.
                logger.LogWarning(ex, "Realtime hub connection to {Base} failed; live updates are off", Base);
                await connection.DisposeAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task StopAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_connection is null) return;

            await _connection.DisposeAsync().ConfigureAwait(false);
            _connection = null;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Tears down and rebuilds the connection. Call this whenever the credentials or the server URL
    /// change — <see cref="StartAsync"/> alone will not replace a connection that is already live.
    /// </summary>
    public async Task RestartAsync()
    {
        await StopAsync().ConfigureAwait(false);
        await StartAsync().ConfigureAwait(false);
    }

    private HubConnection BuildConnection()
    {
        var connection = new HubConnectionBuilder()
            // Invoked on every negotiate and every reconnect, so sharing the provider with the
            // HTTP pipeline gives refresh-on-reconnect for free. WebSocket clients cannot send an
            // Authorization header, so SignalR appends the token as ?access_token= — which the
            // server's token validation reads by default.
            .WithUrl($"{Base}/hubs/kanban", options =>
                options.AccessTokenProvider = async () =>
                    await tokens.GetAccessTokenAsync().ConfigureAwait(false))
            .WithAutomaticReconnect()
            .Build();

        connection.On<Board>("BoardChanged", board => BoardChanged?.Invoke(board));
        connection.On<string>("BoardDeleted", boardId => BoardDeleted?.Invoke(boardId));
        connection.On<string, KanbanTask>("TaskChanged", (boardId, task) => TaskChanged?.Invoke(boardId, task));
        connection.On<string, string>("TaskDeleted", (boardId, taskId) => TaskDeleted?.Invoke(boardId, taskId));
        connection.On<Agent>("AgentChanged", agent => AgentChanged?.Invoke(agent));
        connection.On<string>("AgentDeleted", agentId => AgentDeleted?.Invoke(agentId));
        connection.On<bool>("AuthStateChanged", required => AuthStateChanged?.Invoke(required));

        return connection;
    }
}
