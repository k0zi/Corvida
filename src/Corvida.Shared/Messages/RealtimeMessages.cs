using Corvida.Models;

namespace Corvida.Messages;

public record BoardChangedMessage(Board Board);
public record BoardDeletedMessage(string BoardId);
public record TaskChangedMessage(string BoardId, KanbanTask Task);
public record TaskDeletedMessage(string BoardId, string TaskId);
public record AgentChangedMessage(Agent Agent);
public record AgentDeletedMessage(string AgentId);
