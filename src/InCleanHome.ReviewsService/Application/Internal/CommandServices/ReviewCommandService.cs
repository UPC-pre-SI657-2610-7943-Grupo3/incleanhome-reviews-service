using InCleanHome.ReviewsService.Domain.Model.Aggregates;
using InCleanHome.ReviewsService.Domain.Model.Commands;
using InCleanHome.ReviewsService.Domain.Repositories;
using InCleanHome.ReviewsService.Domain.Services;
using InCleanHome.ReviewsService.Infrastructure.ExternalServices.BookingService;
using InCleanHome.ReviewsService.Infrastructure.ExternalServices.ProfileService;
using InCleanHome.ReviewsService.Infrastructure.Messaging.Events;
using MassTransit;

namespace InCleanHome.ReviewsService.Application.Internal.CommandServices;

public class ReviewCommandService(
    IReviewRepository repository,
    IBookingServiceClient bookingClient,
    IProfileServiceClient profileClient,
    IUnitOfWork unitOfWork,
    IPublishEndpoint publishEndpoint,
    IHttpContextAccessor httpContextAccessor,
    ILogger<ReviewCommandService> logger) : IReviewCommandService
{
    public async Task<Review> Handle(SubmitReviewCommand c)
    {
        var bearer = GetBearer();
        var booking = await bookingClient.GetBookingAsync(c.BookingId, bearer)
            ?? throw new InvalidOperationException("Booking not found or not completed.");

        if (booking.ClientId != c.ClientId)
            throw new InvalidOperationException("This booking does not belong to you.");
        if (!string.Equals(booking.Status, "completed", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("You can only review a completed booking.");

        var existing = await repository.FindByBookingIdAsync(c.BookingId);
        if (existing is not null)
            throw new InvalidOperationException("You already reviewed this booking.");

        // Snapshot the client's name from the booking — denormalized into the
        // Review so the worker's profile can render it without an extra HTTP call.
        var review = new Review(c.BookingId, c.ClientId, booking.WorkerId, c.Rating, c.Comment, booking.ClientName);
        await repository.AddAsync(review);
        await unitOfWork.CompleteAsync();

        // Update the worker's running rating SYNCHRONOUSLY via HTTP.
        // This mirrors the monolith's in-process ACL behaviour: rating updates
        // were never queued, they happened in the same transaction context.
        // We don't fail the review if this HTTP call fails — the RabbitMQ
        // event below is the retry path — but we log it loudly so it's visible.
        var registered = await profileClient.RegisterReviewAsync(review.WorkerId, review.Rating);
        if (!registered)
            logger.LogWarning(
                "[ReviewCommandService] Direct HTTP rating update failed for worker {WorkerId}; " +
                "relying on RabbitMQ ReviewSubmittedEvent fallback.",
                review.WorkerId);

        await SafePublishAsync(new ReviewSubmittedEvent
        {
            ReviewId  = review.Id,
            BookingId = review.BookingId,
            ClientId  = review.ClientId,
            WorkerId  = review.WorkerId,
            Rating    = review.Rating
        });

        return review;
    }

    private string GetBearer()
    {
        var http = httpContextAccessor.HttpContext;
        if (http is null) return string.Empty;
        var raw = http.Request.Headers["Authorization"].FirstOrDefault() ?? string.Empty;
        return raw.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? raw["Bearer ".Length..] : raw;
    }

    private async Task SafePublishAsync<T>(T evt) where T : class
    {
        try { await publishEndpoint.Publish(evt); }
        catch (Exception ex) { logger.LogWarning(ex, "Failed to publish {Type}", typeof(T).Name); }
    }
}
