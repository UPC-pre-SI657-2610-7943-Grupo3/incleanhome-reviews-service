namespace InCleanHome.ReviewsService.Domain.Model.Queries;

// Reviews
public record GetReviewByBookingIdQuery(int BookingId);
public record GetReviewsByWorkerIdQuery(int WorkerId);
public record GetReviewsByClientIdQuery(int ClientId);

// Reports
public record GetAllReportsQuery(string? StatusFilter);
public record GetReportsByReportedUserIdQuery(int ReportedUserId);
public record GetReportByIdQuery(int Id);

// Suspension Appeals
public record GetAllSuspensionAppealsQuery(string? StatusFilter);
public record GetSuspensionAppealByIdQuery(int Id);
public record GetSuspensionAppealsByUserIdQuery(int UserId);
