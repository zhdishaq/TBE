using MassTransit;
using Microsoft.Extensions.Logging;
using TBE.Contracts.Events;

namespace TBE.BookingService.Application.Consumers;

/// <summary>
/// Phase 1 test consumer — verifies end-to-end RabbitMQ message delivery.
/// Replaced by the booking saga state machine in Phase 3.
/// </summary>
public class TestBookingConsumer : IConsumer<BookingInitiated>
{
    private readonly ILogger<TestBookingConsumer> _logger;

    public TestBookingConsumer(ILogger<TestBookingConsumer> logger)
    {
        _logger = logger;
    }

    public Task Consume(ConsumeContext<BookingInitiated> context)
    {
        _logger.LogInformation(
            "TestBookingConsumer received BookingInitiated: BookingId={BookingId}, Channel={Channel}, ProductType={ProductType}",
            context.Message.BookingId,
            context.Message.Channel,
            context.Message.ProductType);

        return Task.CompletedTask;
    }
}
