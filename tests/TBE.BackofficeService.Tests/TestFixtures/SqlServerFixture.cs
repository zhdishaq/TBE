using Testcontainers.MsSql;
using Xunit;

namespace TBE.BackofficeService.Tests.TestFixtures;

/// <summary>
/// Phase 06 test fixture that boots a fresh MSSQL container per
/// test collection. Mirrors the shared <c>MsSqlContainerFixture</c>
/// pattern in <c>tests/TBE.Tests.Shared/Fixtures/</c> but lives under
/// the BackofficeService.Tests project so VALIDATION.md Wave 0
/// deliverables have a homed file path.
/// </summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder()
        .WithPassword("Tbe!TestPwd1")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}

[CollectionDefinition(nameof(SqlServerFixture))]
public sealed class SqlServerFixtureCollection : ICollectionFixture<SqlServerFixture>
{
}
