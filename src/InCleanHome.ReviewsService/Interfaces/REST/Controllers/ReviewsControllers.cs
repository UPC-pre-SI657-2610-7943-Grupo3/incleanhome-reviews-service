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

    [HttpGet("booking/{bookingId:int}")]
    [SwaggerOperation("Get Review by Booking", "Returns the review for a booking, or 404 if no review.")]
    public async Task<IActionResult> GetByBooking(int bookingId)
    {
        var r = await queryService.Handle(new GetReviewByBookingIdQuery(bookingId));
        if (r is null) return NotFound();
        return Ok(ReviewResourceFromEntityAssembler.ToResourceFromEntity(r));
    }

    [HttpGet("worker/{workerId:int}")]
    [SwaggerOperation("Get Reviews by Worker")]
    public async Task<IActionResult> GetByWorker(int workerId)
    {
        var reviews = await queryService.Handle(new GetReviewsByWorkerIdQuery(workerId));
        return Ok(reviews.Select(ReviewResourceFromEntityAssembler.ToResourceFromEntity));
    }

    [HttpGet("client/{clientId:int}")]
    [SwaggerOperation("Get Reviews by Client")]
    public async Task<IActionResult> GetByClient(int clientId)
    {
        var reviews = await queryService.Handle(new GetReviewsByClientIdQuery(clientId));
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

    [HttpGet("user/{userId:int}")]
    [SwaggerOperation("Get Reports by Reported User (admin)")]
    public async Task<IActionResult> GetByReportedUser(int userId)
    {
        var current = (AuthenticatedUser?)HttpContext.Items["User"];
        if (current is null) return Unauthorized();
        if (!current.IsAdmin()) return Forbid();

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

    [HttpGet("mine")]
    [SwaggerOperation("My Appeals")]
    public async Task<IActionResult> Mine()
    {
        var current = (AuthenticatedUser?)HttpContext.Items["User"];
        if (current is null) return Unauthorized();
        var appeals = await queryService.Handle(new GetSuspensionAppealsByUserIdQuery(current.UserId));
        return Ok(appeals.Select(SuspensionAppealResourceFromEntityAssembler.ToResourceFromEntity));
    }

    [HttpGet]
    [SwaggerOperation("List Appeals (admin)")]
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
