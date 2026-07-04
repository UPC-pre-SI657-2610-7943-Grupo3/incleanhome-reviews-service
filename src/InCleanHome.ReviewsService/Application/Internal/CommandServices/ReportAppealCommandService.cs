using InCleanHome.ReviewsService.Domain.Model.Aggregates;
using InCleanHome.ReviewsService.Domain.Model.Commands;
using InCleanHome.ReviewsService.Domain.Repositories;
using InCleanHome.ReviewsService.Domain.Services;
using InCleanHome.ReviewsService.Infrastructure.ExternalServices.CommunicationService;

namespace InCleanHome.ReviewsService.Application.Internal.CommandServices;

/// <summary>
///   Command service for the ReportAppeal aggregate. Mirrors the semantics of
///   <see cref="SuspensionAppealCommandService"/> but for report contests
///   (a reported user disputing that specific report).
/// </summary>
/// <remarks>
///   Uses the HTTP fallback path (<see cref="ICommunicationServiceClient"/>)
///   to notify the user when the admin accepts or rejects their appeal, so
///   the notification lands even if the broker is unhealthy — consistent
///   with the rest of the sin-bus tree.
/// </remarks>
public class ReportAppealCommandService(
    IReportAppealRepository repository,
    IReportRepository reportRepository,
    ICommunicationServiceClient communicationClient,
    IUnitOfWork unitOfWork,
    ILogger<ReportAppealCommandService> logger) : IReportAppealCommandService
{
    public async Task<ReportAppeal> Handle(SubmitReportAppealCommand c)
    {
        // 1. Validar que el reporte exista y que el usuario que reclama sea
        //    efectivamente el reportado — nadie más puede reclamar contra un
        //    reporte que no le afecta.
        var report = await reportRepository.FindByIdAsync(c.ReportId)
            ?? throw new InvalidOperationException("El reporte referenciado no existe.");

        if (report.ReportedUserId != c.UserId)
            throw new InvalidOperationException(
                "Solo el usuario reportado puede reclamar contra este reporte.");

        // 2. No permitir dos reclamos pendientes contra el mismo reporte.
        var pending = await repository.FindPendingByReportIdAsync(c.ReportId);
        if (pending is not null)
            throw new InvalidOperationException(
                "Ya tienes un reclamo pendiente contra este reporte.");

        var appeal = new ReportAppeal(c.ReportId, c.UserId, c.Reason);
        await repository.AddAsync(appeal);
        await unitOfWork.CompleteAsync();

        logger.LogInformation(
            "[ReportAppeal] submitted id={Id} reportId={ReportId} by userId={UserId}",
            appeal.Id, appeal.ReportId, appeal.UserId);

        return appeal;
    }

    public async Task<ReportAppeal?> Handle(AcceptReportAppealCommand c)
    {
        var appeal = await repository.FindByIdAsync(c.AppealId);
        if (appeal is null) return null;

        appeal.Accept(c.AdminUserId, c.Response);
        repository.Update(appeal);
        await unitOfWork.CompleteAsync();

        // Aceptar el reclamo implica DESCARTAR el reporte asociado (queda como
        // "dismissed"), porque el admin le da la razón al usuario reportado.
        var report = await reportRepository.FindByIdAsync(appeal.ReportId);
        if (report is not null && report.Status != "dismissed")
        {
            report.Dismiss(c.AdminUserId, $"Descartado por reclamo aceptado: {c.Response}");
            reportRepository.Update(report);
            await unitOfWork.CompleteAsync();
        }

        // HTTP fallback para notificar al usuario que su reclamo fue aceptado.
        await communicationClient.CreateNotificationAsync(
            userId:         appeal.UserId,
            type:           "report_appeal_accepted",
            title:          "Reclamo de reporte aceptado",
            body:           "El administrador aceptó tu reclamo. El reporte sobre tu cuenta fue descartado.",
            link:           null,
            idempotencyKey: $"report_appeal_accepted:{appeal.Id}");

        return appeal;
    }

    public async Task<ReportAppeal?> Handle(RejectReportAppealCommand c)
    {
        var appeal = await repository.FindByIdAsync(c.AppealId);
        if (appeal is null) return null;

        appeal.Reject(c.AdminUserId, c.Response);
        repository.Update(appeal);
        await unitOfWork.CompleteAsync();

        await communicationClient.CreateNotificationAsync(
            userId:         appeal.UserId,
            type:           "report_appeal_rejected",
            title:          "Reclamo de reporte rechazado",
            body:           "El administrador rechazó tu reclamo. El reporte sobre tu cuenta se mantiene.",
            link:           null,
            idempotencyKey: $"report_appeal_rejected:{appeal.Id}");

        return appeal;
    }
}
