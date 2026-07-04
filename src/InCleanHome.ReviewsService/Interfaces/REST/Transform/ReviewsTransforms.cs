using InCleanHome.ReviewsService.Domain.Model.Aggregates;
using InCleanHome.ReviewsService.Interfaces.REST.Resources;

namespace InCleanHome.ReviewsService.Interfaces.REST.Transform;

public static class ReviewResourceFromEntityAssembler
{
    public static ReviewResource ToResourceFromEntity(Review r)
        => new(r.Id, r.BookingId, r.ClientId, r.WorkerId, r.Rating, r.Comment, r.ClientName, r.CreatedDate);
}

public static class ReportResourceFromEntityAssembler
{
    public static ReportResource ToResourceFromEntity(Report r)
        => new(r.Id, r.ReporterUserId, r.ReportedUserId, r.ReportedRole,
               r.Reason, r.Details, r.Status,
               r.ConfirmedByAdminUserId, r.ConfirmedAt, r.AdminNotes,
               r.CreatedDate);
}

public static class SuspensionAppealResourceFromEntityAssembler
{
    public static SuspensionAppealResource ToResourceFromEntity(SuspensionAppeal a)
        => new(a.Id, a.UserId, a.Reason, a.Status,
               a.ReviewedByAdminUserId, a.ReviewedAt, a.AdminResponse,
               a.CreatedDate);
}

public static class ReportAppealResourceFromEntityAssembler
{
    public static ReportAppealResource ToResourceFromEntity(ReportAppeal a)
        => new(a.Id, a.ReportId, a.UserId, a.Reason, a.Status,
               a.ReviewedByAdminUserId, a.ReviewedAt, a.AdminResponse,
               a.CreatedDate);
}
