using InCleanHome.ReviewsService.Domain.Model.Aggregates;
using InCleanHome.ReviewsService.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace InCleanHome.ReviewsService.Infrastructure.Persistence.Repositories;

public class ReviewRepository(ReviewsDbContext context)
    : BaseRepository<Review>(context), IReviewRepository
{
    public async Task<Review?> FindByBookingIdAsync(int bookingId)
        => await Context.Set<Review>().FirstOrDefaultAsync(r => r.BookingId == bookingId);

    public async Task<IEnumerable<Review>> FindByWorkerIdAsync(int workerId)
        => await Context.Set<Review>()
            .Where(r => r.WorkerId == workerId)
            .OrderByDescending(r => r.CreatedDate)
            .ToListAsync();

    public async Task<IEnumerable<Review>> FindByClientIdAsync(int clientId)
        => await Context.Set<Review>()
            .Where(r => r.ClientId == clientId)
            .OrderByDescending(r => r.CreatedDate)
            .ToListAsync();
}

public class ReportRepository(ReviewsDbContext context)
    : BaseRepository<Report>(context), IReportRepository
{
    public new async Task<IEnumerable<Report>> ListAsync(string? statusFilter)
    {
        var q = Context.Set<Report>().AsQueryable();
        if (!string.IsNullOrWhiteSpace(statusFilter))
            q = q.Where(r => r.Status == statusFilter);
        return await q.OrderByDescending(r => r.CreatedDate).ToListAsync();
    }

    public async Task<IEnumerable<Report>> FindByReportedUserIdAsync(int reportedUserId)
        => await Context.Set<Report>()
            .Where(r => r.ReportedUserId == reportedUserId)
            .OrderByDescending(r => r.CreatedDate)
            .ToListAsync();

    public async Task<int> CountConfirmedAgainstUserIdAsync(int userId)
        => await Context.Set<Report>()
            .CountAsync(r => r.ReportedUserId == userId && r.Status == "confirmed");
}

public class SuspensionAppealRepository(ReviewsDbContext context)
    : BaseRepository<SuspensionAppeal>(context), ISuspensionAppealRepository
{
    public new async Task<IEnumerable<SuspensionAppeal>> ListAsync(string? statusFilter)
    {
        var q = Context.Set<SuspensionAppeal>().AsQueryable();
        if (!string.IsNullOrWhiteSpace(statusFilter))
            q = q.Where(a => a.Status == statusFilter);
        return await q.OrderByDescending(a => a.CreatedDate).ToListAsync();
    }

    public async Task<IEnumerable<SuspensionAppeal>> FindByUserIdAsync(int userId)
        => await Context.Set<SuspensionAppeal>()
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.CreatedDate)
            .ToListAsync();

    public async Task<SuspensionAppeal?> FindPendingByUserIdAsync(int userId)
        => await Context.Set<SuspensionAppeal>()
            .FirstOrDefaultAsync(a => a.UserId == userId && a.Status == SuspensionAppeal.StatusPending);
}
