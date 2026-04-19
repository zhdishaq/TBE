using Testcontainers.RabbitMq;
using Xunit;

namespace TBE.BackofficeService.Tests.TestFixtures;

/// <summary>
/// Phase 06 test fixture that boots a fresh RabbitMQ container per
/// test collection. Used by <c>DeadLetterQueueTests</c> and the
/// cross-service 4-eyes tests to exercise MassTransit consumers
/// against a real broker (the in-memory harness doesn't model
/// <c>_error</c> queues faithfully).
/// </summary>
public sealed class RabbitMqFixture : IAsyncLifetime
{
    private readonly RabbitMqContainer _container = new RabbitMqBuilder().Build();

    public string AmqpUri => _container.GetConnectionString();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}

[CollectionDefinition(nameof(RabbitMqFixture))]
public sealed class RabbitMqFixtureCollection : ICollectionFixture<RabbitMqFixture>
{
}
