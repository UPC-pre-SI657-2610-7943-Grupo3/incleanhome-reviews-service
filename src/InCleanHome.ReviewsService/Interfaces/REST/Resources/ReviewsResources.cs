namespace InCleanHome.ReviewsService.Interfaces.REST.Resources;

// Reviews
public record SubmitReviewResource(int BookingId, int Rating, string? Comment);
public record ReviewResource(
    int Id, int BookingId, int ClientId, int WorkerId,
    int Rating, string Comment, string ClientName, DateTimeOffset? CreatedAt);

// Reports
public record SubmitReportResource(int ReportedUserId, string ReportedRole, string Reason, string? Details);
public record ConfirmDismissReportResource(string? AdminNotes);
public record ReportResource(
    int Id, int ReporterUserId, int ReportedUserId, string ReportedRole,
    string Reason, string Details, string Status,
    int? ConfirmedByAdminUserId, DateTimeOffset? ConfirmedAt, string AdminNotes,
    DateTimeOffset? CreatedAt);

// Suspension Appeals
public record SubmitSuspensionAppealResource(string Reason);
public record DecideAppealResource(string Response);
public record SuspensionAppealResource(
    int Id, int UserId, string Reason, string Status,
    int? ReviewedByAdminUserId, DateTimeOffset? ReviewedAt, string AdminResponse,
    DateTimeOffset? CreatedAt);

// Report Appeals
public record SubmitReportAppealResource(int ReportId, string Reason);
public record ReportAppealResource(
    int Id, int ReportId, int UserId, string Reason, string Status,
    int? ReviewedByAdminUserId, DateTimeOffset? ReviewedAt, string AdminResponse,
    DateTimeOffset? CreatedAt);
