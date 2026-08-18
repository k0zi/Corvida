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

// Allows browser-hosted clients (e.g. Corvida.Web) to call this API cross-origin.
const string CorsPolicy = "CorvidaWeb";
builder.Services.AddCors(options =>
    options.AddPolicy(CorsPolicy, policy => policy
        .AllowAnyOrigin()
        .AllowAnyHeader()
        .AllowAnyMethod()));

// PascalCase JSON — matches desktop System.Text.Json defaults (no PropertyNamingPolicy)
builder.Services.ConfigureHttpJsonOptions(opt =>
    opt.SerializerOptions.PropertyNamingPolicy = null);

var app = builder.Build();

app.UseCors(CorsPolicy);

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