using InCleanHome.ReviewsService.Domain.Model.Aggregates;
using InCleanHome.ReviewsService.Domain.Model.Commands;
using InCleanHome.ReviewsService.Domain.Repositories;
using InCleanHome.ReviewsService.Domain.Services;
using InCleanHome.ReviewsService.Infrastructure.ExternalServices.IamService;
using InCleanHome.ReviewsService.Infrastructure.Messaging.Events;
using MassTransit;

namespace InCleanHome.ReviewsService.Application.Internal.CommandServices;

public class SuspensionAppealCommandService(
    ISuspensionAppealRepository repository,
    IIamServiceClient iamClient,
    IUnitOfWork unitOfWork,
    IPublishEndpoint publishEndpoint,
    IHttpContextAccessor httpContextAccessor,
    ILogger<SuspensionAppealCommandService> logger) : ISuspensionAppealCommandService
{
    public async Task<SuspensionAppeal> Handle(SubmitSuspensionAppealCommand c)
    {
        var existingPending = await repository.FindPendingByUserIdAsync(c.UserId);
        if (existingPending is not null)
            throw new InvalidOperationException("Ya tienes una apelación pendiente de revisión.");

        var appeal = new SuspensionAppeal(c.UserId, c.Reason);
        await repository.AddAsync(appeal);
        await unitOfWork.CompleteAsync();

        await SafePublishAsync(new SuspensionAppealSubmittedEvent
        {
            AppealId      = appeal.Id,
            UserId        = appeal.UserId,
            Justification = appeal.Reason
        });

        return appeal;
    }

    public async Task<SuspensionAppeal?> Handle(AcceptSuspensionAppealCommand c)
    {
        var appeal = await repository.FindByIdAsync(c.AppealId);
        if (appeal is null) return null;

        appeal.Accept(c.AdminUserId, c.Response);
        repository.Update(appeal);
        await unitOfWork.CompleteAsync();

        // Lift the suspension via IAM Service. The IAM admin endpoint is
        // /api/admin/users/{id}/clear-suspension — currently only admins can
        // call it. For now we log a TODO: a future service-to-service token
        // could let Reviews lift suspensions automatically.
        // For now an admin will need to manually clear the suspension on IAM.
        logger.LogInformation(
            "Appeal {Id} accepted by admin {AdminId}. Admin must clear suspension via IAM endpoint.",
            appeal.Id, c.AdminUserId);

        return appeal;
    }

    public async Task<SuspensionAppeal?> Handle(RejectSuspensionAppealCommand c)
    {
        var appeal = await repository.FindByIdAsync(c.AppealId);
        if (appeal is null) return null;
        appeal.Reject(c.AdminUserId, c.Response);
        repository.Update(appeal);
        await unitOfWork.CompleteAsync();
        return appeal;
    }

    private async Task SafePublishAsync<T>(T evt) where T : class
    {
        try { await publishEndpoint.Publish(evt); }
        catch (Exception ex) { logger.LogWarning(ex, "Failed to publish {Type}", typeof(T).Name); }
    }
}
