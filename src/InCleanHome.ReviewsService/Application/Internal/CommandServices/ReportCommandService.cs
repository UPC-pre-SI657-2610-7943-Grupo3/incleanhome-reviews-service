using InCleanHome.ReviewsService.Domain.Model.Aggregates;
using InCleanHome.ReviewsService.Domain.Model.Commands;
using InCleanHome.ReviewsService.Domain.Repositories;
using InCleanHome.ReviewsService.Domain.Services;
using InCleanHome.ReviewsService.Infrastructure.ExternalServices.CommunicationService;
using InCleanHome.ReviewsService.Infrastructure.ExternalServices.IamService;
using InCleanHome.ReviewsService.Infrastructure.Messaging.Events;
using MassTransit;

namespace InCleanHome.ReviewsService.Application.Internal.CommandServices;

public class ReportCommandService(
    IReportRepository repository,
    IIamServiceClient iamClient,
    ICommunicationServiceClient communicationClient,
    IUnitOfWork unitOfWork,
    IPublishEndpoint publishEndpoint,
    IConfiguration configuration,
    IHttpContextAccessor httpContextAccessor,
    ILogger<ReportCommandService> logger) : IReportCommandService
{
    public async Task<Report> Handle(SubmitReportCommand c)
    {
        var report = new Report(c.ReporterUserId, c.ReportedUserId, c.ReportedRole, c.Reason, c.Details);
        await repository.AddAsync(report);
        await unitOfWork.CompleteAsync();

        await SafePublishAsync(new ReportSubmittedEvent
        {
            ReportId       = report.Id,
            ReporterId     = report.ReporterUserId,
            ReportedUserId = report.ReportedUserId,
            Reason         = report.Reason
        });

        // HTTP fallback: notify the reported user that they have a pending report.
        await communicationClient.CreateNotificationAsync(
            userId:         report.ReportedUserId,
            type:           "report_submitted",
            title:          "Cuenta reportada",
            body:           "Tu cuenta ha recibido un reporte. El equipo de administración lo revisará pronto.",
            link:           null,
            idempotencyKey: $"report_submitted:{report.Id}");

        return report;
    }

    public async Task<Report?> Handle(ConfirmReportCommand c)
    {
        var report = await repository.FindByIdAsync(c.ReportId);
        if (report is null) return null;
        report.Confirm(c.AdminUserId, c.AdminNotes);
        repository.Update(report);
        await unitOfWork.CompleteAsync();

        await SafePublishAsync(new ReportConfirmedEvent
        {
            ReportId       = report.Id,
            ReportedUserId = report.ReportedUserId,
            ConfirmedBy    = c.AdminUserId.ToString()
        });

        // HTTP fallback: notify the reported user.
        await communicationClient.CreateNotificationAsync(
            userId:         report.ReportedUserId,
            type:           "report_confirmed",
            title:          "Reporte confirmado",
            body:           "El administrador ha confirmado un reporte sobre tu cuenta.",
            link:           null,
            idempotencyKey: $"report_confirmed:{report.Id}");

        // Auto-suspension if a user has accumulated too many confirmed reports.
        var threshold = configuration.GetValue<int?>("Reviews:ReportThresholdToSuspend") ?? 3;
        var defaultDays = configuration.GetValue<int?>("Reviews:DefaultSuspensionDays") ?? 30;

        var confirmedCount = await repository.CountConfirmedAgainstUserIdAsync(report.ReportedUserId);
        if (confirmedCount >= threshold)
        {
            var bearer = GetBearer();
            var ok = await iamClient.SuspendUserAsync(
                report.ReportedUserId, defaultDays,
                $"Suspensión automática por {confirmedCount} reportes confirmados.",
                bearer);
            if (!ok)
                logger.LogWarning("Auto-suspension failed for user {Id}", report.ReportedUserId);
        }

        return report;
    }

    public async Task<Report?> Handle(DismissReportCommand c)
    {
        var report = await repository.FindByIdAsync(c.ReportId);
        if (report is null) return null;
        report.Dismiss(c.AdminUserId, c.AdminNotes);
        repository.Update(report);
        await unitOfWork.CompleteAsync();

        // HTTP fallback: notify the reported user that the report was dismissed.
        await communicationClient.CreateNotificationAsync(
            userId:         report.ReportedUserId,
            type:           "report_dismissed",
            title:          "Reporte descartado",
            body:           "El administrador ha revisado el reporte sobre tu cuenta y lo ha descartado.",
            link:           null,
            idempotencyKey: $"report_dismissed:{report.Id}");

        return report;
    }

    private string GetBearer()
    {
        var http = httpContextAccessor.HttpContext;
        if (http is null) return string.Empty;
        var raw = http.Request.Headers["Authorization"].FirstOrDefault() ?? string.Empty;
        return raw.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? raw["Bearer ".Length..] : raw;
    }

    private async Task SafePublishAsync<T>(T evt) where T : class
    {
        try { await publishEndpoint.Publish(evt); }
        catch (Exception ex) { logger.LogWarning(ex, "Failed to publish {Type}", typeof(T).Name); }
    }
}
