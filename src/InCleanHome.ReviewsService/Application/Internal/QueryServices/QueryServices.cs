using InCleanHome.ReviewsService.Domain.Model.Aggregates;
using InCleanHome.ReviewsService.Domain.Model.Queries;
using InCleanHome.ReviewsService.Domain.Repositories;
using InCleanHome.ReviewsService.Domain.Services;

namespace InCleanHome.ReviewsService.Application.Internal.QueryServices;

public class ReviewQueryService(IReviewRepository repository) : IReviewQueryService
{
    public Task<Review?> Handle(GetReviewByBookingIdQuery q) => repository.FindByBookingIdAsync(q.BookingId);
    public Task<IEnumerable<Review>> Handle(GetReviewsByWorkerIdQuery q) => repository.FindByWorkerIdAsync(q.WorkerId);
    public Task<IEnumerable<Review>> Handle(GetReviewsByClientIdQuery q) => repository.FindByClientIdAsync(q.ClientId);
}

public class ReportQueryService(IReportRepository repository) : IReportQueryService
{
    public Task<IEnumerable<Report>> Handle(GetAllReportsQuery q) => repository.ListAsync(q.StatusFilter);
    public Task<IEnumerable<Report>> Handle(GetReportsByReportedUserIdQuery q) => repository.FindByReportedUserIdAsync(q.ReportedUserId);
    public Task<Report?> Handle(GetReportByIdQuery q) => repository.FindByIdAsync(q.Id);
}

public class SuspensionAppealQueryService(ISuspensionAppealRepository repository) : ISuspensionAppealQueryService
{
    public Task<IEnumerable<SuspensionAppeal>> Handle(GetAllSuspensionAppealsQuery q) => repository.ListAsync(q.StatusFilter);
    public Task<SuspensionAppeal?> Handle(GetSuspensionAppealByIdQuery q) => repository.FindByIdAsync(q.Id);
    public Task<IEnumerable<SuspensionAppeal>> Handle(GetSuspensionAppealsByUserIdQuery q) => repository.FindByUserIdAsync(q.UserId);
}
