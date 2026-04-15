using Testcontainers.MsSql;
using Xunit;

namespace TBE.Tests.Shared.Fixtures;

/// <summary>
/// Boots an MSSQL container for the duration of a test collection.
/// Exposes <see cref="ConnectionString"/> for tests that need a real SQL Server.
/// </summary>
public sealed class MsSqlContainerFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder()
        .WithPassword("Tbe!TestPwd1")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}

[CollectionDefinition(nameof(MsSqlContainerFixture))]
public sealed class MsSqlContainerFixtureCollection : ICollectionFixture<MsSqlContainerFixture>
{
}
