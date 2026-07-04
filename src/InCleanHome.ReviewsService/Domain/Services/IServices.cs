using InCleanHome.ReviewsService.Domain.Model.Aggregates;
using InCleanHome.ReviewsService.Domain.Model.Commands;
using InCleanHome.ReviewsService.Domain.Model.Queries;

namespace InCleanHome.ReviewsService.Domain.Services;

// Reviews
public interface IReviewCommandService
{
    Task<Review> Handle(SubmitReviewCommand command);
}

public interface IReviewQueryService
{
    Task<Review?> Handle(GetReviewByBookingIdQuery query);
    Task<IEnumerable<Review>> Handle(GetReviewsByWorkerIdQuery query);
    Task<IEnumerable<Review>> Handle(GetReviewsByClientIdQuery query);
}

// Reports
public interface IReportCommandService
{
    Task<Report> Handle(SubmitReportCommand command);
    Task<Report?> Handle(ConfirmReportCommand command);
    Task<Report?> Handle(DismissReportCommand command);
}

public interface IReportQueryService
{
    Task<IEnumerable<Report>> Handle(GetAllReportsQuery query);
    Task<IEnumerable<Report>> Handle(GetReportsByReportedUserIdQuery query);
    Task<Report?> Handle(GetReportByIdQuery query);
}

// Suspension Appeals
public interface ISuspensionAppealCommandService
{
    Task<SuspensionAppeal> Handle(SubmitSuspensionAppealCommand command);
    Task<SuspensionAppeal?> Handle(AcceptSuspensionAppealCommand command);
    Task<SuspensionAppeal?> Handle(RejectSuspensionAppealCommand command);
}

public interface ISuspensionAppealQueryService
{
    Task<IEnumerable<SuspensionAppeal>> Handle(GetAllSuspensionAppealsQuery query);
    Task<SuspensionAppeal?> Handle(GetSuspensionAppealByIdQuery query);
    Task<IEnumerable<SuspensionAppeal>> Handle(GetSuspensionAppealsByUserIdQuery query);
}

// Report Appeals
public interface IReportAppealCommandService
{
    Task<ReportAppeal> Handle(SubmitReportAppealCommand command);
    Task<ReportAppeal?> Handle(AcceptReportAppealCommand command);
    Task<ReportAppeal?> Handle(RejectReportAppealCommand command);
}

public interface IReportAppealQueryService
{
    Task<IEnumerable<ReportAppeal>> Handle(GetAllReportAppealsQuery query);
    Task<ReportAppeal?> Handle(GetReportAppealByIdQuery query);
    Task<IEnumerable<ReportAppeal>> Handle(GetReportAppealsByUserIdQuery query);
}
