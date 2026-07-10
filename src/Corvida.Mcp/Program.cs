using Corvida.Mcp;
using Corvida.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

// Logging must go to stderr — stdout is reserved for MCP JSON-RPC messages
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);
builder.Logging.SetMinimumLevel(LogLevel.Warning);

builder.Services.AddSingleton<ISettingsService, SettingsService>();

builder.Services.AddSingleton<BoardService>();
builder.Services.AddSingleton<HttpBoardService>();
builder.Services.AddSingleton<IBoardService, StorageAwareBoardService>();

builder.Services.AddSingleton<TaskService>();
builder.Services.AddSingleton<HttpTaskService>();
builder.Services.AddSingleton<ITaskService, StorageAwareTaskService>();

builder.Services.AddHttpClient("CorvidaApi");
builder.Services.AddHostedService<SettingsLoader>();

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();
