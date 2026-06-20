namespace InCleanHome.ReviewsService.Infrastructure.Messaging.Events;

// Published by Reviews Service 

public record ReviewSubmittedEvent
{
    public int ReviewId { get; init; }
    public int BookingId { get; init; }
    public int ClientId { get; init; }
    public int WorkerId { get; init; }
    public int Rating { get; init; }
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}

public record ReportSubmittedEvent
{
    public int ReportId { get; init; }
    public int ReporterId { get; init; }
    public int ReportedUserId { get; init; }
    public string Reason { get; init; } = string.Empty;
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}

public record ReportConfirmedEvent
{
    public int ReportId { get; init; }
    public int ReportedUserId { get; init; }
    public string? ConfirmedBy { get; init; }
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}

public record SuspensionAppealSubmittedEvent
{
    public int AppealId { get; init; }
    public int UserId { get; init; }
    public string Justification { get; init; } = string.Empty;
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}

// Consumed by Reviews Service

/// <summary>
/// Published by Booking Service when a booking completes. Reviews uses this
/// to know which bookings are reviewable. Currently we DON'T pre-create
/// review entries — the client submits the review via POST /api/v1/reviews
/// referencing the bookingId. The event is consumed for audit only.
/// </summary>
public record BookingCompletedEvent
{
    public int BookingId { get; init; }
    public int ClientId { get; init; }
    public int WorkerId { get; init; }
    public string ClientName { get; init; } = string.Empty;
    public string WorkerName { get; init; } = string.Empty;
    public decimal TotalAmount { get; init; }
    public decimal PlatformFee { get; init; }
    public decimal WorkerEarning { get; init; }
    public int PaymentMethodId { get; init; }
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}
