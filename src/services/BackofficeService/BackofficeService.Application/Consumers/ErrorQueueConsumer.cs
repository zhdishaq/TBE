using System.Text.Json.Nodes;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace TBE.BackofficeService.Application.Consumers;

/// <summary>
/// Plan 06-01 Task 3 stub — D-58 dead-letter capture consumer.
///
/// Registered by <c>Program.cs</c> against every <c>{queue}_error</c>
/// MassTransit publishes when a consumer exhausts its retry policy. The
/// real implementation (Task 4) persists a <c>DeadLetterQueueRow</c> with
/// <c>MT-Fault-Message</c>, <c>MT-Fault-InputAddress</c>, <c>MT-MessageType</c>
/// headers, truncates the failure reason to 1000 chars, and swallows any
/// persistence failure so the error queue never infinite-loops.
///
/// This stub is load-bearing only for <em>compile</em>: Program.cs wires
/// <c>x.AddConsumer&lt;ErrorQueueConsumer&gt;()</c> and 10 receive
/// endpoints call <c>e.ConfigureConsumer&lt;ErrorQueueConsumer&gt;()</c>
/// during the Task 3 bootstrap. Task 4 replaces the body end-to-end.
/// </summary>
public sealed class ErrorQueueConsumer : IConsumer<JsonObject>
{
    private readonly ILogger<ErrorQueueConsumer> _logger;

    public ErrorQueueConsumer(ILogger<ErrorQueueConsumer> logger)
    {
        _logger = logger;
    }

    public Task Consume(ConsumeContext<JsonObject> context)
    {
        // Task 3 placeholder — Task 4 supplies the real persistence body.
        _logger.LogInformation(
            "dlq-stub received {MessageType} from {SourceAddress} (id={MessageId})",
            context.Headers.Get<string>("MT-MessageType"),
            context.SourceAddress,
            context.MessageId);
        return Task.CompletedTask;
    }
}
