using System.Net.Mime;
using InCleanHome.ReviewsService.Domain.Model.Commands;
using InCleanHome.ReviewsService.Domain.Model.Queries;
using InCleanHome.ReviewsService.Domain.Services;
using InCleanHome.ReviewsService.Infrastructure.Pipeline;
using InCleanHome.ReviewsService.Interfaces.REST.Resources;
using InCleanHome.ReviewsService.Interfaces.REST.Transform;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace InCleanHome.ReviewsService.Interfaces.REST.Controllers;

[ApiController]
[Route("api/v1/reviews")]
[Produces(MediaTypeNames.Application.Json)]
[SwaggerTag("Reviews — 1-5 star ratings for completed bookings")]
public class ReviewsController(
    IReviewCommandService commandService,
    IReviewQueryService queryService) : ControllerBase
{
    [HttpPost]
    [SwaggerOperation("Submit Review", "Client submits a review for a completed booking.")]
    public async Task<IActionResult> Submit([FromBody] SubmitReviewResource body)
    {
        var current = (AuthenticatedUser?)HttpContext.Items["User"];
        if (current is null) return Unauthorized();
        if (!current.IsClient()) return StatusCode(403, new { error = "Only clients can submit reviews." });

        try
        {
            var review = await commandService.Handle(new SubmitReviewCommand(
                body.BookingId, current.UserId, body.Rating, body.Comment));
            return Ok(ReviewResourceFromEntityAssembler.ToResourceFromEntity(review));
        }
        catch (Exception e) { return BadRequest(new { error = e.Message }); }
    }

    [HttpGet("worker/{workerId:int}")]
    [SwaggerOperation("Get Reviews by Worker")]
    public async Task<IActionResult> GetByWorker(int workerId)
    {
        var reviews = await queryService.Handle(new GetReviewsByWorkerIdQuery(workerId));
        return Ok(reviews.Select(ReviewResourceFromEntityAssembler.ToResourceFromEntity));
    }

}

[ApiController]
[Route("api/v1/reports")]
[Produces(MediaTypeNames.Application.Json)]
[SwaggerTag("Reports — user-to-user complaints reviewed by admins")]
public class ReportsController(
    IReportCommandService commandService,
    IReportQueryService queryService) : ControllerBase
{
    [HttpPost]
    [SwaggerOperation("Submit Report", "Any authenticated user can submit a report.")]
    public async Task<IActionResult> Submit([FromBody] SubmitReportResource body)
    {
        var current = (AuthenticatedUser?)HttpContext.Items["User"];
        if (current is null) return Unauthorized();

        try
        {
            var report = await commandService.Handle(new SubmitReportCommand(
                current.UserId, body.ReportedUserId, body.ReportedRole, body.Reason, body.Details));
            return Ok(ReportResourceFromEntityAssembler.ToResourceFromEntity(report));
        }
        catch (Exception e) { return BadRequest(new { error = e.Message }); }
    }

    [HttpGet]
    [SwaggerOperation("List Reports (admin)")]
    public async Task<IActionResult> List([FromQuery] string? status)
    {
        var current = (AuthenticatedUser?)HttpContext.Items["User"];
        if (current is null) return Unauthorized();
        if (!current.IsAdmin()) return Forbid();

        var reports = await queryService.Handle(new GetAllReportsQuery(status));
        return Ok(reports.Select(ReportResourceFromEntityAssembler.ToResourceFromEntity));
    }

    [HttpGet("me")]
    [SwaggerOperation("Get Reports Against Me",
        "Returns reports where the current user is the REPORTED user (so the worker/client knows they were reported).")]
    public async Task<IActionResult> GetReportsAgainstMe()
    {
        var current = (AuthenticatedUser?)HttpContext.Items["User"];
        if (current is null) return Unauthorized();

        var reports = await queryService.Handle(new GetReportsByReportedUserIdQuery(current.UserId));
        return Ok(reports.Select(ReportResourceFromEntityAssembler.ToResourceFromEntity));
    }

    [HttpGet("user/{userId:int}")]
    [SwaggerOperation("Reports Against User (internal composition)",
        "Returns all reports where the given user is the REPORTED party. " +
        "Used by Search Service to count confirmed reports and show the badge " +
        "on the public worker profile. Requires authentication but NOT admin role.")]
    public async Task<IActionResult> GetReportsAgainstUser(int userId)
    {
        var current = (AuthenticatedUser?)HttpContext.Items["User"];
        if (current is null) return Unauthorized();

        var reports = await queryService.Handle(new GetReportsByReportedUserIdQuery(userId));
        return Ok(reports.Select(ReportResourceFromEntityAssembler.ToResourceFromEntity));
    }

    [HttpPatch("{id:int}/confirm")]
    [SwaggerOperation("Confirm Report (admin)",
        "Admin confirms the report. May trigger auto-suspension of the reported user via IAM.")]
    public async Task<IActionResult> Confirm(int id, [FromBody] ConfirmDismissReportResource body)
    {
        var current = (AuthenticatedUser?)HttpContext.Items["User"];
        if (current is null) return Unauthorized();
        if (!current.IsAdmin()) return Forbid();

        var report = await commandService.Handle(new ConfirmReportCommand(id, current.UserId, body.AdminNotes));
        if (report is null) return NotFound();
        return Ok(ReportResourceFromEntityAssembler.ToResourceFromEntity(report));
    }

    [HttpPatch("{id:int}/dismiss")]
    [SwaggerOperation("Dismiss Report (admin)")]
    public async Task<IActionResult> Dismiss(int id, [FromBody] ConfirmDismissReportResource body)
    {
        var current = (AuthenticatedUser?)HttpContext.Items["User"];
        if (current is null) return Unauthorized();
        if (!current.IsAdmin()) return Forbid();

        var report = await commandService.Handle(new DismissReportCommand(id, current.UserId, body.AdminNotes));
        if (report is null) return NotFound();
        return Ok(ReportResourceFromEntityAssembler.ToResourceFromEntity(report));
    }
}

[ApiController]
[Route("api/v1/suspension-appeals")]
[Produces(MediaTypeNames.Application.Json)]
[SwaggerTag("Suspension appeals — suspended users contest their suspension")]
public class SuspensionAppealsController(
    ISuspensionAppealCommandService commandService,
    ISuspensionAppealQueryService queryService) : ControllerBase
{
    [HttpPost]
    [SwaggerOperation("Submit Appeal", "The current user submits an appeal about their own suspension.")]
    public async Task<IActionResult> Submit([FromBody] SubmitSuspensionAppealResource body)
    {
        var current = (AuthenticatedUser?)HttpContext.Items["User"];
        if (current is null) return Unauthorized();

        try
        {
            var appeal = await commandService.Handle(new SubmitSuspensionAppealCommand(current.UserId, body.Reason));
            return Ok(SuspensionAppealResourceFromEntityAssembler.ToResourceFromEntity(appeal));
        }
        catch (Exception e) { return BadRequest(new { error = e.Message }); }
    }

    [HttpGet("me")]
    [SwaggerOperation("My Appeals",
        "Returns the current user's own appeals. The frontend expects 200 with the latest appeal data " +
        "or an empty list — but we keep returning a list for consistency.")]
    public async Task<IActionResult> Me()
    {
        var current = (AuthenticatedUser?)HttpContext.Items["User"];
        if (current is null) return Unauthorized();
        var appeals = await queryService.Handle(new GetSuspensionAppealsByUserIdQuery(current.UserId));
        return Ok(appeals.Select(SuspensionAppealResourceFromEntityAssembler.ToResourceFromEntity));
    }

    [HttpGet("pending")]
    [SwaggerOperation("List Pending Appeals (admin)",
        "Returns only appeals with status='pending'. Used by AdminSuspensionAppealsView.")]
    public async Task<IActionResult> Pending()
    {
        var current = (AuthenticatedUser?)HttpContext.Items["User"];
        if (current is null) return Unauthorized();
        if (!current.IsAdmin()) return Forbid();

        var appeals = await queryService.Handle(new GetAllSuspensionAppealsQuery("pending"));
        return Ok(appeals.Select(SuspensionAppealResourceFromEntityAssembler.ToResourceFromEntity));
    }

    [HttpGet]
    [SwaggerOperation("List Appeals (admin)", "Optionally filter by ?status=pending|accepted|rejected.")]
    public async Task<IActionResult> List([FromQuery] string? status)
    {
        var current = (AuthenticatedUser?)HttpContext.Items["User"];
        if (current is null) return Unauthorized();
        if (!current.IsAdmin()) return Forbid();

        var appeals = await queryService.Handle(new GetAllSuspensionAppealsQuery(status));
        return Ok(appeals.Select(SuspensionAppealResourceFromEntityAssembler.ToResourceFromEntity));
    }

    [HttpPatch("{id:int}/accept")]
    [SwaggerOperation("Accept Appeal (admin)",
        "Admin accepts the appeal. The IAM suspension must be cleared manually afterwards.")]
    public async Task<IActionResult> Accept(int id, [FromBody] DecideAppealResource body)
    {
        var current = (AuthenticatedUser?)HttpContext.Items["User"];
        if (current is null) return Unauthorized();
        if (!current.IsAdmin()) return Forbid();

        try
        {
            var appeal = await commandService.Handle(new AcceptSuspensionAppealCommand(id, current.UserId, body.Response));
            if (appeal is null) return NotFound();
            return Ok(SuspensionAppealResourceFromEntityAssembler.ToResourceFromEntity(appeal));
        }
        catch (Exception e) { return BadRequest(new { error = e.Message }); }
    }

    [HttpPatch("{id:int}/reject")]
    [SwaggerOperation("Reject Appeal (admin)")]
    public async Task<IActionResult> Reject(int id, [FromBody] DecideAppealResource body)
    {
        var current = (AuthenticatedUser?)HttpContext.Items["User"];
        if (current is null) return Unauthorized();
        if (!current.IsAdmin()) return Forbid();

        try
        {
            var appeal = await commandService.Handle(new RejectSuspensionAppealCommand(id, current.UserId, body.Response));
            if (appeal is null) return NotFound();
            return Ok(SuspensionAppealResourceFromEntityAssembler.ToResourceFromEntity(appeal));
        }
        catch (Exception e) { return BadRequest(new { error = e.Message }); }
    }
}

/// <summary>
///   Report appeals — a user disputes a report filed against them.
///   Follows the same UX as <see cref="SuspensionAppealsController"/>.
/// </summary>
[ApiController]
[Route("api/v1/report-appeals")]
[Produces(MediaTypeNames.Application.Json)]
[SwaggerTag("Report appeals — reported users contest a report")]
public class ReportAppealsController(
    IReportAppealCommandService commandService,
    IReportAppealQueryService queryService) : ControllerBase
{
    [HttpPost]
    [SwaggerOperation("Submit Report Appeal",
        "The current user submits an appeal about a specific report filed against them.")]
    public async Task<IActionResult> Submit([FromBody] SubmitReportAppealResource body)
    {
        var current = (AuthenticatedUser?)HttpContext.Items["User"];
        if (current is null) return Unauthorized();

        try
        {
            var appeal = await commandService.Handle(
                new SubmitReportAppealCommand(body.ReportId, current.UserId, body.Reason));
            return Ok(ReportAppealResourceFromEntityAssembler.ToResourceFromEntity(appeal));
        }
        catch (Exception e) { return BadRequest(new { error = e.Message }); }
    }

    [HttpGet("me")]
    [SwaggerOperation("My Report Appeals", "Returns the current user's own report appeals.")]
    public async Task<IActionResult> Me()
    {
        var current = (AuthenticatedUser?)HttpContext.Items["User"];
        if (current is null) return Unauthorized();
        var appeals = await queryService.Handle(new GetReportAppealsByUserIdQuery(current.UserId));
        return Ok(appeals.Select(ReportAppealResourceFromEntityAssembler.ToResourceFromEntity));
    }

    [HttpGet("pending")]
    [SwaggerOperation("List Pending Report Appeals (admin)")]
    public async Task<IActionResult> Pending()
    {
        var current = (AuthenticatedUser?)HttpContext.Items["User"];
        if (current is null) return Unauthorized();
        if (!current.IsAdmin()) return Forbid();

        var appeals = await queryService.Handle(new GetAllReportAppealsQuery("pending"));
        return Ok(appeals.Select(ReportAppealResourceFromEntityAssembler.ToResourceFromEntity));
    }

    [HttpGet]
    [SwaggerOperation("List Report Appeals (admin)",
        "Optionally filter by ?status=pending|accepted|rejected.")]
    public async Task<IActionResult> List([FromQuery] string? status)
    {
        var current = (AuthenticatedUser?)HttpContext.Items["User"];
        if (current is null) return Unauthorized();
        if (!current.IsAdmin()) return Forbid();

        var appeals = await queryService.Handle(new GetAllReportAppealsQuery(status));
        return Ok(appeals.Select(ReportAppealResourceFromEntityAssembler.ToResourceFromEntity));
    }

    [HttpPatch("{id:int}/accept")]
    [SwaggerOperation("Accept Report Appeal (admin)",
        "Admin accepts the appeal — the underlying report is automatically dismissed.")]
    public async Task<IActionResult> Accept(int id, [FromBody] DecideAppealResource body)
    {
        var current = (AuthenticatedUser?)HttpContext.Items["User"];
        if (current is null) return Unauthorized();
        if (!current.IsAdmin()) return Forbid();

        try
        {
            var appeal = await commandService.Handle(
                new AcceptReportAppealCommand(id, current.UserId, body.Response));
            if (appeal is null) return NotFound();
            return Ok(ReportAppealResourceFromEntityAssembler.ToResourceFromEntity(appeal));
        }
        catch (Exception e) { return BadRequest(new { error = e.Message }); }
    }

    [HttpPatch("{id:int}/reject")]
    [SwaggerOperation("Reject Report Appeal (admin)")]
    public async Task<IActionResult> Reject(int id, [FromBody] DecideAppealResource body)
    {
        var current = (AuthenticatedUser?)HttpContext.Items["User"];
        if (current is null) return Unauthorized();
        if (!current.IsAdmin()) return Forbid();

        try
        {
            var appeal = await commandService.Handle(
                new RejectReportAppealCommand(id, current.UserId, body.Response));
            if (appeal is null) return NotFound();
            return Ok(ReportAppealResourceFromEntityAssembler.ToResourceFromEntity(appeal));
        }
        catch (Exception e) { return BadRequest(new { error = e.Message }); }
    }
}
