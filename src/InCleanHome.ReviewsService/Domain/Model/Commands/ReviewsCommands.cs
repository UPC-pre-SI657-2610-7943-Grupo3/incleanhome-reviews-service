namespace InCleanHome.ReviewsService.Domain.Model.Commands;

// Reviews
public record SubmitReviewCommand(int BookingId, int ClientId, int Rating, string? Comment);

// Reports
public record SubmitReportCommand(
    int ReporterUserId, int ReportedUserId, string ReportedRole, string Reason, string? Details);
public record ConfirmReportCommand(int ReportId, int AdminUserId, string? AdminNotes);
public record DismissReportCommand(int ReportId, int AdminUserId, string? AdminNotes);

// Suspension Appeals
public record SubmitSuspensionAppealCommand(int UserId, string Reason);
public record AcceptSuspensionAppealCommand(int AppealId, int AdminUserId, string Response);
public record RejectSuspensionAppealCommand(int AppealId, int AdminUserId, string Response);

// Report Appeals — un usuario reportado apela contra el reporte específico.
public record SubmitReportAppealCommand(int ReportId, int UserId, string Reason);
public record AcceptReportAppealCommand(int AppealId, int AdminUserId, string Response);
public record RejectReportAppealCommand(int AppealId, int AdminUserId, string Response);
