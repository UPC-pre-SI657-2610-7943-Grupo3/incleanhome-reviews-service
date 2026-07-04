using InCleanHome.ReviewsService.Domain.Model.Aggregates;
using InCleanHome.ReviewsService.Domain.Model.Commands;
using InCleanHome.ReviewsService.Domain.Repositories;
using InCleanHome.ReviewsService.Domain.Services;
using InCleanHome.ReviewsService.Infrastructure.ExternalServices.CommunicationService;
using InCleanHome.ReviewsService.Infrastructure.ExternalServices.IamService;
using InCleanHome.ReviewsService.Infrastructure.Messaging.Events;
using MassTransit;

namespace InCleanHome.ReviewsService.Application.Internal.CommandServices;

public class SuspensionAppealCommandService(
    ISuspensionAppealRepository repository,
    IIamServiceClient iamClient,
    ICommunicationServiceClient communicationClient,
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

        // Automatically lift the IAM suspension. The admin's own bearer is
        // forwarded — /admin/users/{id}/clear-suspension is admin-only and
        // the caller of this command is already an admin (verified by the
        // controller before invoking the command).
        var bearer = GetAdminBearer();
        if (!string.IsNullOrEmpty(bearer))
        {
            var lifted = await iamClient.ClearSuspensionAsync(appeal.UserId, bearer);
            if (!lifted)
                logger.LogWarning(
                    "Appeal {Id} accepted but ClearSuspensionAsync(user={UserId}) failed. " +
                    "The user's suspension may still be active in IAM — verify manually.",
                    appeal.Id, appeal.UserId);
            else
                logger.LogInformation(
                    "Appeal {Id} accepted by admin {AdminId} and suspension for user {UserId} lifted.",
                    appeal.Id, c.AdminUserId, appeal.UserId);
        }
        else
        {
            logger.LogWarning(
                "Appeal {Id} accepted but no bearer available to clear suspension — verify manually.",
                appeal.Id);
        }

        // Publish event for Communication Service so it can push a notification
        // to the user ("Tu reclamo fue aceptado").
        await SafePublishAsync(new SuspensionAppealAcceptedEvent
        {
            AppealId      = appeal.Id,
            UserId        = appeal.UserId,
            AdminResponse = appeal.AdminResponse
        });

        // HTTP fallback: notify the user immediately even if the broker is down.
        await communicationClient.CreateNotificationAsync(
            userId:         appeal.UserId,
            type:           "appeal_accepted",
            title:          "Tu reclamo fue aceptado",
            body:           "Tu suspensión ha sido levantada. Ya puedes usar la plataforma normalmente.",
            link:           null,
            idempotencyKey: $"appeal_accepted:{appeal.Id}");

        return appeal;
    }

    private string GetAdminBearer()
    {
        var http = httpContextAccessor.HttpContext;
        if (http is null) return string.Empty;
        var raw = http.Request.Headers["Authorization"].FirstOrDefault() ?? string.Empty;
        return raw.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? raw["Bearer ".Length..]
            : string.Empty;
    }

    public async Task<SuspensionAppeal?> Handle(RejectSuspensionAppealCommand c)
    {
        var appeal = await repository.FindByIdAsync(c.AppealId);
        if (appeal is null) return null;
        appeal.Reject(c.AdminUserId, c.Response);
        repository.Update(appeal);
        await unitOfWork.CompleteAsync();

        await SafePublishAsync(new SuspensionAppealRejectedEvent
        {
            AppealId      = appeal.Id,
            UserId        = appeal.UserId,
            AdminResponse = appeal.AdminResponse
        });

        // HTTP fallback.
        await communicationClient.CreateNotificationAsync(
            userId:         appeal.UserId,
            type:           "appeal_rejected",
            title:          "Tu reclamo fue rechazado",
            body:           "El administrador revisó tu reclamo y decidió mantener la suspensión.",
            link:           null,
            idempotencyKey: $"appeal_rejected:{appeal.Id}");

        return appeal;
    }

    private async Task SafePublishAsync<T>(T evt) where T : class
    {
        try { await publishEndpoint.Publish(evt); }
        catch (Exception ex) { logger.LogWarning(ex, "Failed to publish {Type}", typeof(T).Name); }
    }
}
