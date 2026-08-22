using System;
using System.Threading.Tasks;
using Corvida.Models;

namespace Corvida.Services;

public interface IRealtimeClient
{
    Task StartAsync();
    Task StopAsync();

    /// <summary>
    /// Tears down and rebuilds the connection, picking up changed credentials or server URL.
    /// </summary>
    Task RestartAsync();

    event Action<Board>? BoardChanged;
    event Action<string>? BoardDeleted;
    event Action<string, KanbanTask>? TaskChanged;
    event Action<string, string>? TaskDeleted;
    event Action<Agent>? AgentChanged;
    event Action<string>? AgentDeleted;

    /// <summary>
    /// Raised when the server switches required authentication on or off, just before it drops
    /// existing connections — so the client can show a login screen rather than a connection error.
    /// </summary>
    event Action<bool>? AuthStateChanged;
}
