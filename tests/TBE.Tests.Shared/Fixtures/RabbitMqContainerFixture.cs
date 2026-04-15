using Testcontainers.RabbitMq;
using Xunit;

namespace TBE.Tests.Shared.Fixtures;

/// <summary>
/// Boots a RabbitMQ container for the duration of a test collection.
/// Exposes <see cref="AmqpUri"/> for MassTransit bus configuration in integration tests.
/// </summary>
public sealed class RabbitMqContainerFixture : IAsyncLifetime
{
    private readonly RabbitMqContainer _container = new RabbitMqBuilder().Build();

    public string AmqpUri => _container.GetConnectionString();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}

[CollectionDefinition(nameof(RabbitMqContainerFixture))]
public sealed class RabbitMqContainerFixtureCollection : ICollectionFixture<RabbitMqContainerFixture>
{
}
