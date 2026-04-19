using System.Text.Json.Nodes;
using MassTransit;
using Microsoft.Extensions.Logging;
using TBE.BackofficeService.Application.Entities;
using TBE.BackofficeService.Infrastructure;

// Namespace kept as Application.Consumers so Program.cs + Task 4
// integration test fully-qualified references match the 06-01-PLAN
// file manifest. Physical project assignment is Infrastructure because
// the consumer depends on the EF DbContext (DI boundary).
namespace TBE.BackofficeService.Application.Consumers;

/// <summary>
/// Plan 06-01 Task 4 — D-58 dead-letter capture consumer.
///
/// MassTransit publishes to <c>{queue}_error</c> after a consumer
/// exhausts its retry policy. This consumer is registered against every
/// known <c>_error</c> queue in Program.cs and persists the full
/// envelope into <c>backoffice.DeadLetterQueue</c> so BO-09 list/search
/// + BO-10 requeue/resolve have durable state to render.
///
/// Header conventions (MassTransit fault headers):
///   - MT-Fault-Message       → failure reason (first 1000 chars; overflow truncated).
///   - MT-Fault-InputAddress  → originating queue (rabbitmq://host/vhost/queue).
///   - MT-MessageType         → fully-qualified CLR type of the original message.
///
/// Pitfall: NEVER throw from this consumer. If persistence itself fails
/// (SQL unavailable, constraint violation) we log + swallow — re-throwing
/// would send the envelope back to <c>_error_error</c> and infinite-loop.
/// </summary>
public sealed class ErrorQueueConsumer : IConsumer<JsonObject>
{
    private readonly BackofficeDbContext _db;
    private readonly ILogger<ErrorQueueConsumer> _logger;

    public ErrorQueueConsumer(
        BackofficeDbContext db,
        ILogger<ErrorQueueConsumer> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<JsonObject> context)
    {
        try
        {
            var faultMessage = context.Headers.Get<string>("MT-Fault-Message") ?? string.Empty;
            var inputAddress = context.Headers.Get<string>("MT-Fault-InputAddress")
                ?? context.SourceAddress?.ToString()
                ?? string.Empty;
            var messageType = context.Headers.Get<string>("MT-MessageType") ?? string.Empty;

            // Derive the bare queue name from the AMQP URI tail (…/vhost/queue).
            var originalQueue = ExtractQueueName(inputAddress);

            var row = new DeadLetterQueueRow
            {
                Id = Guid.NewGuid(),
                MessageId = context.MessageId ?? Guid.NewGuid(),
                CorrelationId = context.CorrelationId,
                MessageType = Truncate(messageType, 256),
                OriginalQueue = Truncate(originalQueue, 256),
                Payload = context.Message.ToJsonString(),
                FailureReason = Truncate(faultMessage, 1000),
                FirstFailedAt = DateTime.UtcNow,
                RequeueCount = 0,
            };

            _db.DeadLetterQueue.Add(row);
            await _db.SaveChangesAsync(context.CancellationToken);

            _logger.LogInformation(
                "dlq-capture {Queue} {MessageType} {MessageId}",
                row.OriginalQueue,
                row.MessageType,
                row.MessageId);
        }
        catch (Exception ex)
        {
            // Do NOT throw — we would end up on {queue}_error_error.
            _logger.LogError(ex, "dlq-persist-failed {MessageId}", context.MessageId);
        }
    }

    private static string ExtractQueueName(string address)
    {
        if (string.IsNullOrEmpty(address)) return string.Empty;
        var lastSlash = address.LastIndexOf('/');
        return lastSlash >= 0 && lastSlash < address.Length - 1
            ? address[(lastSlash + 1)..]
            : address;
    }

    private static string Truncate(string value, int max) =>
        string.IsNullOrEmpty(value) || value.Length <= max
            ? value ?? string.Empty
            : value[..max];
}
