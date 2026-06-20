using InCleanHome.ReviewsService.Infrastructure.Messaging.Events;
using MassTransit;

namespace InCleanHome.ReviewsService.Infrastructure.Messaging.Consumers;

/// <summary>
/// Consumes <see cref="BookingCompletedEvent"/>. Reviews are submitted via
/// POST /api/v1/reviews referencing the bookingId, so we don't pre-create
/// review entries here — this consumer is purely for audit / observability.
/// Future: could pre-create a placeholder "review pending" row.
/// </summary>
public class BookingCompletedConsumer(
    ILogger<BookingCompletedConsumer> logger) : IConsumer<BookingCompletedEvent>
{
    public Task Consume(ConsumeContext<BookingCompletedEvent> ctx)
    {
        var e = ctx.Message;
        logger.LogInformation(
            "[BookingCompleted] booking={BookingId} client={ClientId} worker={WorkerId}",
            e.BookingId, e.ClientId, e.WorkerId);
        return Task.CompletedTask;
    }
}
