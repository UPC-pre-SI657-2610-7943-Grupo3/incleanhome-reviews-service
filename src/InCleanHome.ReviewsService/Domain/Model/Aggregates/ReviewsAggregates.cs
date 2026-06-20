using System.ComponentModel.DataAnnotations.Schema;
using EntityFrameworkCore.CreatedUpdatedDate.Contracts;

namespace InCleanHome.ReviewsService.Domain.Model.Aggregates;

// Review 
/// <summary>
/// Review aggregate root — 1-5 rating + comment posted by a client about a completed booking.
/// </summary>
public class Review : IEntityWithCreatedUpdatedDate
{
    public int Id { get; private set; }
    public int BookingId { get; private set; }
    public int ClientId { get; private set; }
    public int WorkerId { get; private set; }
    public int Rating { get; private set; }
    public string Comment { get; private set; } = string.Empty;

    [Column("CreatedAt")] public DateTimeOffset? CreatedDate { get; set; }
    [Column("UpdatedAt")] public DateTimeOffset? UpdatedDate { get; set; }

    public Review() { }

    public Review(int bookingId, int clientId, int workerId, int rating, string? comment)
    {
        if (rating < 1 || rating > 5)
            throw new ArgumentException("Rating must be between 1 and 5.");

        BookingId = bookingId;
        ClientId  = clientId;
        WorkerId  = workerId;
        Rating    = rating;
        Comment   = comment ?? string.Empty;
    }
}

// Report

/// <summary>
/// Report aggregate root — a user reports another profile for review by moderators.
/// </summary>
public class Report : IEntityWithCreatedUpdatedDate
{
    public int Id { get; private set; }
    public int ReporterUserId { get; private set; }
    public int ReportedUserId { get; private set; }
    public string ReportedRole { get; private set; } = string.Empty;
    public string Reason { get; private set; } = string.Empty;
    public string Details { get; private set; } = string.Empty;
    public string Status { get; private set; } = "open";
    public int? ConfirmedByAdminUserId { get; private set; }
    public DateTimeOffset? ConfirmedAt { get; private set; }
    public string AdminNotes { get; private set; } = string.Empty;

    [Column("CreatedAt")] public DateTimeOffset? CreatedDate { get; set; }
    [Column("UpdatedAt")] public DateTimeOffset? UpdatedDate { get; set; }

    public Report() { }

    public Report(int reporterUserId, int reportedUserId, string reportedRole, string reason, string? details)
    {
        if (reporterUserId == reportedUserId)
            throw new ArgumentException("You cannot report yourself.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("A reason is required.");

        ReporterUserId = reporterUserId;
        ReportedUserId = reportedUserId;
        ReportedRole   = reportedRole;
        Reason         = reason;
        Details        = details ?? string.Empty;
        Status         = "open";
    }

    public Report Confirm(int adminUserId, string? adminNotes)
    {
        Status = "confirmed";
        ConfirmedByAdminUserId = adminUserId;
        ConfirmedAt = DateTimeOffset.UtcNow;
        AdminNotes = adminNotes ?? string.Empty;
        return this;
    }

    public Report Dismiss(int adminUserId, string? adminNotes)
    {
        Status = "dismissed";
        ConfirmedByAdminUserId = adminUserId;
        ConfirmedAt = DateTimeOffset.UtcNow;
        AdminNotes = adminNotes ?? string.Empty;
        return this;
    }
}

// SuspensionAppeal 

/// <summary>
/// Suspension appeal — a suspended user contests their own suspension.
/// </summary>
public class SuspensionAppeal : IEntityWithCreatedUpdatedDate
{
    public const string StatusPending  = "pending";
    public const string StatusAccepted = "accepted";
    public const string StatusRejected = "rejected";

    public int Id { get; private set; }
    public int UserId { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public string Status { get; private set; } = StatusPending;
    public int? ReviewedByAdminUserId { get; private set; }
    public DateTimeOffset? ReviewedAt { get; private set; }
    public string AdminResponse { get; private set; } = string.Empty;

    [Column("CreatedAt")] public DateTimeOffset? CreatedDate { get; set; }
    [Column("UpdatedAt")] public DateTimeOffset? UpdatedDate { get; set; }

    public SuspensionAppeal() { }

    public SuspensionAppeal(int userId, string reason)
    {
        if (userId <= 0) throw new ArgumentException("Invalid userId");
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("El motivo del reclamo no puede estar vacío.");

        UserId = userId;
        Reason = reason.Trim();
        Status = StatusPending;
    }

    public void Accept(int adminUserId, string response)
    {
        EnsurePending();
        ReviewedByAdminUserId = adminUserId;
        ReviewedAt            = DateTimeOffset.UtcNow;
        AdminResponse         = (response ?? string.Empty).Trim();
        Status                = StatusAccepted;
    }

    public void Reject(int adminUserId, string response)
    {
        EnsurePending();
        ReviewedByAdminUserId = adminUserId;
        ReviewedAt            = DateTimeOffset.UtcNow;
        AdminResponse         = (response ?? string.Empty).Trim();
        Status                = StatusRejected;
    }

    private void EnsurePending()
    {
        if (Status != StatusPending)
            throw new InvalidOperationException(
                $"Solo se pueden revisar reclamos en estado '{StatusPending}'. Este está en '{Status}'.");
    }
}
