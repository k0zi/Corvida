using System.Globalization;
using System.Threading.RateLimiting;
using Corvida.Api;
using Corvida.Api.Auth;
using Corvida.Api.Data;
using Corvida.Api.Endpoints;
using Corvida.Api.Hubs;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

var connectionString = builder.Configuration.GetConnectionString("CorvidaApi");

builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseNpgsql(
        connectionString,
        npgsql => npgsql.EnableRetryOnFailure()
    ));

builder.Services.AddCorvidaAuth(builder.Configuration);
builder.Services.AddHostedService<MigrationHostedService>();

builder.Services.AddOpenApi();
builder.Services.AddSignalR();

// Defence in depth over Identity's own lockout. Partitioned by client IP, which is the only
// identity available this early in the pipeline. No GlobalLimiter — that would throttle the
// SignalR hub and the health probes too.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.OnRejected = static async (context, ct) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            context.HttpContext.Response.Headers.RetryAfter =
                ((int)retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);

        // snake_case, OAuth-shaped, so a client's token parser does not choke on it.
        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsync(
            """{"error":"slow_down","error_description":"Too many requests. Try again shortly."}""", ct);
    };

    options.AddPolicy(RateLimitPolicies.Token, PerIpFixedWindow(permitLimit: 10, TimeSpan.FromMinutes(1)));
    options.AddPolicy(RateLimitPolicies.Enable, PerIpFixedWindow(permitLimit: 3, TimeSpan.FromMinutes(5)));
});

static Func<HttpContext, RateLimitPartition<string>> PerIpFixedWindow(int permitLimit, TimeSpan window) =>
    context => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permitLimit,
            Window = window,
            QueueLimit = 0,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            AutoReplenishment = true,
        });

// Browser-hosted clients (Corvida.Web) call this API cross-origin.
//
// The origin list is explicit rather than AllowAnyOrigin: with a token endpoint on this host,
// "Access-Control-Allow-Origin: *" would let any page the user happens to visit call the API and
// read the response. Override via Auth:AllowedOrigins (Auth__AllowedOrigins__0=... in env).
const string CorsPolicy = "CorvidaWeb";
string[] defaultOrigins = ["http://localhost:5180", "https://localhost:7180"];
var allowedOrigins = builder.Configuration.GetSection("Auth:AllowedOrigins").Get<string[]>() is { Length: > 0 } configured
    ? configured
    : defaultOrigins;

builder.Services.AddCors(options =>
    options.AddPolicy(CorsPolicy, policy => policy
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()));

// PascalCase JSON — matches desktop System.Text.Json defaults (no PropertyNamingPolicy).
// Note this does NOT apply to the OAuth token endpoint, which must stay snake_case per RFC 6749.
builder.Services.ConfigureHttpJsonOptions(opt =>
    opt.SerializerOptions.PropertyNamingPolicy = null);

var app = builder.Build();

if (builder.Configuration.AllowsInsecureTransport())
{
    app.Logger.LogWarning(
        "Auth:AllowInsecureTransport is enabled: tokens and passwords travel unencrypted. " +
        "Put a TLS-terminating reverse proxy in front of the API before exposing it beyond localhost.");
}

app.UseCors(CorsPolicy);

// After UseCors so a 429 still carries CORS headers and the browser client can read it; before
// UseAuthentication because OpenIddict handles /connect/token from inside that middleware, so a
// flood must be stopped before it reaches password hashing.
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapDefaultEndpoints();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapTokenEndpoint();
app.MapAuthEndpoints();
app.MapUserEndpoints();
app.MapApiKeyEndpoints();
app.MapBoardEndpoints();
app.MapTaskEndpoints();
app.MapAgentEndpoints();
app.MapHub<KanbanHub>("/hubs/kanban").RequireAuthorization(Policies.User);

app.Run();

public partial class Program { }
