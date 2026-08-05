using Corvida.Api.Data;
using Corvida.Api.Endpoints;
using Corvida.Api.Hubs;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseNpgsql(
        builder.Configuration.GetConnectionString("CorvidaApi"),
        npgsql => npgsql.EnableRetryOnFailure()
    ));

builder.Services.AddOpenApi();
builder.Services.AddSignalR();

// PascalCase JSON — matches desktop System.Text.Json defaults (no PropertyNamingPolicy)
builder.Services.ConfigureHttpJsonOptions(opt =>
    opt.SerializerOptions.PropertyNamingPolicy = null);

var app = builder.Build();

app.MapDefaultEndpoints();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}


await using var scope = app.Services.CreateAsyncScope();
var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
await db.Database.CreateExecutionStrategy().ExecuteAsync(() => db.Database.MigrateAsync());

app.MapBoardEndpoints();
app.MapTaskEndpoints();
app.MapAgentEndpoints();
app.MapHub<KanbanHub>("/hubs/kanban");

app.Run();

public partial class Program { }