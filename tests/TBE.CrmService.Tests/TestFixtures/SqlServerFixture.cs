using Testcontainers.MsSql;
using Xunit;

namespace TBE.CrmService.Tests.TestFixtures;

/// <summary>
/// Plan 06-04 test fixture that boots a fresh MSSQL container per
/// test collection. Mirrors the <c>SqlServerFixture</c> pattern in
/// <c>tests/TBE.BackofficeService.Tests/TestFixtures/SqlServerFixture.cs</c>
/// so CRM projections tests can build on the same pre-warmed container
/// abstraction once Docker is wired in CI.
///
/// <para>
/// Tests that take this fixture MUST also be tagged
/// <c>Trait("Category","RedPlaceholder")</c> until the CI baseline
/// grows a Docker-enabled worker; the baseline filter drops them with
/// <c>--filter Category!=RedPlaceholder</c>.
/// </para>
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
