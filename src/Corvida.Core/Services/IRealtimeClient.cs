using System;
using System.Threading.Tasks;
using Corvida.Models;

namespace Corvida.Services;

public interface IRealtimeClient
{
    Task StartAsync();
    Task StopAsync();

    event Action<Board>? BoardChanged;
    event Action<string>? BoardDeleted;
    event Action<string, KanbanTask>? TaskChanged;
    event Action<string, string>? TaskDeleted;
    event Action<Agent>? AgentChanged;
    event Action<string>? AgentDeleted;
}
