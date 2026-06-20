using InCleanHome.ReviewsService.Domain.Model.Aggregates;

namespace InCleanHome.ReviewsService.Domain.Repositories;

public interface IBaseRepository<TEntity>
{
    Task AddAsync(TEntity entity);
    Task<TEntity?> FindByIdAsync(int id);
    void Update(TEntity entity);
    void Remove(TEntity entity);
    Task<IEnumerable<TEntity>> ListAsync();
}

public interface IUnitOfWork { Task CompleteAsync(); }

public interface IReviewRepository : IBaseRepository<Review>
{
    Task<Review?> FindByBookingIdAsync(int bookingId);
    Task<IEnumerable<Review>> FindByWorkerIdAsync(int workerId);
    Task<IEnumerable<Review>> FindByClientIdAsync(int clientId);
}

public interface IReportRepository : IBaseRepository<Report>
{
    Task<IEnumerable<Report>> ListAsync(string? statusFilter);
    Task<IEnumerable<Report>> FindByReportedUserIdAsync(int reportedUserId);
    Task<int> CountConfirmedAgainstUserIdAsync(int userId);
}

public interface ISuspensionAppealRepository : IBaseRepository<SuspensionAppeal>
{
    Task<IEnumerable<SuspensionAppeal>> ListAsync(string? statusFilter);
    Task<IEnumerable<SuspensionAppeal>> FindByUserIdAsync(int userId);
    Task<SuspensionAppeal?> FindPendingByUserIdAsync(int userId);
}
