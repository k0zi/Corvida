using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace Corvida.Api.Tests.Fixtures;

[CollectionDefinition("postgres")]
public class PostgresCollection : ICollectionFixture<PostgresContainerFixture> { }

public sealed class PostgresContainerFixture : IAsyncLifetime
{
    private const string PostgresPassword = "testpw";
    private const string PostgresUser = "postgres";
    private const string PostgresDb = "postgres";
    private const int PostgresPort = 5432;

    private readonly IContainer _container = new ContainerBuilder()
        .WithImage("docker.io/postgres:16")
        .WithEnvironment("POSTGRES_PASSWORD", PostgresPassword)
        .WithEnvironment("POSTGRES_USER", PostgresUser)
        .WithEnvironment("POSTGRES_DB", PostgresDb)
        .WithPortBinding(PostgresPort, true)
        .WithWaitStrategy(Wait.ForUnixContainer()
            .UntilMessageIsLogged("database system is ready to accept connections"))
        .Build();

    public string ConnectionString =>
        $"Host=localhost;Port={_container.GetMappedPublicPort(PostgresPort)}" +
        $";Username={PostgresUser};Password={PostgresPassword};Database={PostgresDb}";

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}
