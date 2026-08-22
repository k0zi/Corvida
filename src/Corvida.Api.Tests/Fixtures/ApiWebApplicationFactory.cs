using Corvida.Api.Auth;
using Corvida.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Corvida.Api.Tests.Fixtures;

public sealed class ApiWebApplicationFactory(string connectionString)
    : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            // Both contexts must be repointed at the test container. Missing AuthDbContext here
            // would leave it on the (null) configured connection string, and every request would
            // fail inside the auth gate rather than in anything the test is about.
            Repoint<AppDbContext>(services, opt => opt.UseNpgsql(connectionString));
            Repoint<AuthDbContext>(services, opt => opt.UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsHistoryTable(
                    AuthSchema.MigrationsHistoryTable, AuthSchema.Name)));
        });
    }

    private static void Repoint<TContext>(
        IServiceCollection services, Action<DbContextOptionsBuilder> configure)
        where TContext : DbContext
    {
        var descriptor = services.SingleOrDefault(
            d => d.ServiceType == typeof(DbContextOptions<TContext>));
        if (descriptor != null)
            services.Remove(descriptor);

        services.AddDbContext<TContext>(configure);
    }
}
