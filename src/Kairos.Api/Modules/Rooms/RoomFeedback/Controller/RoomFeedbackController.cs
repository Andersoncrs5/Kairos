using System.ComponentModel.DataAnnotations;
using Asp.Versioning;
using Kairos.Api.Common.Base.Controller;
using Kairos.Api.Common.Page;
using Kairos.Api.Common.Res;
using Kairos.Api.Common.Result;
using Kairos.Api.Config.Authentication;
using Kairos.Api.Config.DB.Transaction;
using Kairos.Api.Modules.Rooms.RoomFeedback.DTOs.Commands;
using Kairos.Api.Modules.Rooms.RoomFeedback.DTOs.Request;
using Kairos.Api.Modules.Rooms.RoomFeedback.DTOs.Response;
using Kairos.Api.Modules.Rooms.RoomFeedback.Mapper;
using Kairos.Api.Modules.Rooms.RoomFeedback.Models;
using Kairos.Api.Modules.Rooms.RoomFeedback.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kairos.Api.Modules.Rooms.RoomFeedback.Controller;

[Route("api/v{version:apiVersion}")]
[ApiVersion("1.0")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public sealed class RoomFeedbackController(
    IInboxTransactionUseCase<CreateRoomFeedbackCommand, RoomFeedbackModel, ICreateRoomFeedbackUseCase> createFeedbackUseCase,
    IInboxTransactionUseCase<UpdateRoomFeedbackCommand, RoomFeedbackModel, IUpdateRoomFeedbackUseCase> updateFeedbackUseCase,
    IInboxTransactionUseCase<DeleteRoomFeedbackCommand, bool, IDeleteRoomFeedbackUseCase> deleteFeedbackUseCase,
    IInboxTransactionUseCase<FindRoomFeedbacksCommand, Page<RoomFeedbackModel>, IFindRoomFeedbacksUseCase> findFeedbacksUseCase,
    IInboxTransactionUseCase<FindRoomFeedbackByIdCommand, RoomFeedbackModel, IFindRoomFeedbackByIdUseCase> findFeedbackByIdUseCase,
    IInboxTransactionUseCase<GetRoomFeedbackSummaryCommand, RoomFeedbackSummaryDto, IGetRoomFeedbackSummaryUseCase> getSummaryUseCase
) : ApiControllerBase
{
    // ---------------------------------------------------------------
    // Create
    // ---------------------------------------------------------------

    /// <summary>
    /// Reviews the room used in a reservation.
    /// </summary>
    /// <remarks>
    /// Only the organizer or an attendee who accepted the invitation can
    /// review, once the reservation has ended and was not cancelled.
    /// One review per person per reservation. The author is identified
    /// through the authenticated JWT. Requires the X-Inbox-Id header.
    /// </remarks>
    [HttpPost("reservations/{reservationId}/feedbacks")]
    [RequirePermissions("rooms.feedbacks.create")]
    [ProducesResponseType(typeof(ResponseHttp<RoomFeedbackDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create(
        [FromRoute, Required] string reservationId,
        [FromBody] CreateRoomFeedbackDto dto,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        CreateRoomFeedbackCommand command = new(
            ReservationId: reservationId,
            RequesterUserId: GetAuthenticatedUserId(inboxId),
            Dto: dto);

        Result<RoomFeedbackModel> result =
            await createFeedbackUseCase.ExecuteAsync(
                inboxId,
                NewWorkerId(),
                command,
                ct);

        return ToResponse(
            result,
            RoomFeedbackMapper.ToDto,
            "Room feedback created successfully",
            "Error creating room feedback",
            inboxId,
            StatusCodes.Status201Created);
    }

    // ---------------------------------------------------------------
    // Find (list)
    // ---------------------------------------------------------------

    /// <summary>
    /// Retrieves a paginated list of room feedback.
    /// </summary>
    /// <remarks>
    /// Supports filtering by room, author, reservation, rating, issue type
    /// and content, plus pagination and sorting.
    /// Regular users only see their own feedback; users with the
    /// moderation permission see everything.
    /// </remarks>
    [HttpGet("room-feedbacks")]
    [RequirePermissions("rooms.feedbacks.list")]
    [ProducesResponseType(typeof(ResponseHttp<Page<RoomFeedbackDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Find(
        [FromQuery] FilterRoomFeedbackDto dto,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        FindRoomFeedbacksCommand command = new(
            RequesterUserId: GetAuthenticatedUserId(inboxId),
            Dto: dto);

        Result<Page<RoomFeedbackModel>> result =
            await findFeedbacksUseCase.ExecuteAsync(
                inboxId,
                NewWorkerId(),
                command,
                ct);

        return ToResponse(
            result,
            ToPageDto,
            "Room feedback retrieved successfully",
            "Error retrieving room feedback",
            inboxId);
    }

    // ---------------------------------------------------------------
    // Find by ID
    // ---------------------------------------------------------------

    /// <summary>
    /// Retrieves a room feedback by its identifier.
    /// </summary>
    /// <remarks>
    /// Only the author or a moderator can see it. Anyone else gets 404.
    /// </remarks>
    [HttpGet("room-feedbacks/{feedbackId}")]
    [RequirePermissions("rooms.feedbacks.read")]
    [ProducesResponseType(typeof(ResponseHttp<RoomFeedbackDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> FindById(
        [FromRoute, Required] string feedbackId,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        FindRoomFeedbackByIdCommand command = new(
            FeedbackId: feedbackId,
            RequesterUserId: GetAuthenticatedUserId(inboxId));

        Result<RoomFeedbackModel> result =
            await findFeedbackByIdUseCase.ExecuteAsync(
                inboxId,
                NewWorkerId(),
                command,
                ct);

        return ToResponse(
            result,
            RoomFeedbackMapper.ToDto,
            "Room feedback found",
            "Error finding room feedback",
            inboxId);
    }

    // ---------------------------------------------------------------
    // Update
    // ---------------------------------------------------------------

    /// <summary>
    /// Updates a room feedback.
    /// </summary>
    /// <remarks>
    /// Full replacement: a null Comment or IssueType clears the current
    /// value. Only the author can update.
    /// </remarks>
    [HttpPut("room-feedbacks/{feedbackId}")]
    [RequirePermissions("rooms.feedbacks.update")]
    [ProducesResponseType(typeof(ResponseHttp<RoomFeedbackDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        [FromRoute, Required] string feedbackId,
        [FromBody] UpdateRoomFeedbackDto dto,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        UpdateRoomFeedbackCommand command = new(
            FeedbackId: feedbackId,
            RequesterUserId: GetAuthenticatedUserId(inboxId),
            Dto: dto);

        Result<RoomFeedbackModel> result =
            await updateFeedbackUseCase.ExecuteAsync(
                inboxId,
                NewWorkerId(),
                command,
                ct);

        return ToResponse(
            result,
            RoomFeedbackMapper.ToDto,
            "Room feedback updated successfully",
            "Error updating room feedback",
            inboxId);
    }

    // ---------------------------------------------------------------
    // Delete
    // ---------------------------------------------------------------

    /// <summary>
    /// Deletes a room feedback.
    /// </summary>
    /// <remarks>
    /// The author or a moderator can delete it.
    /// </remarks>
    [HttpDelete("room-feedbacks/{feedbackId}")]
    [RequirePermissions("rooms.feedbacks.delete")]
    [ProducesResponseType(typeof(ResponseHttp<bool?>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(
        [FromRoute, Required] string feedbackId,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        DeleteRoomFeedbackCommand command = new(
            FeedbackId: feedbackId,
            RequesterUserId: GetAuthenticatedUserId(inboxId));

        Result<bool> result =
            await deleteFeedbackUseCase.ExecuteAsync(
                inboxId,
                NewWorkerId(),
                command,
                ct);

        return ToResponse(
            result,
            deleted => (bool?)deleted,
            "Room feedback deleted successfully",
            "Error deleting room feedback",
            inboxId);
    }

    // ---------------------------------------------------------------
    // Summary
    // ---------------------------------------------------------------

    /// <summary>
    /// Retrieves the rating summary of a room: average, total and the
    /// distribution of ratings from 1 to 5.
    /// </summary>
    /// <remarks>
    /// Aggregated data only: it does not expose who wrote what.
    /// AverageRating is null while the room has no feedback.
    /// </remarks>
    [HttpGet("rooms/{roomId}/feedbacks/summary")]
    [RequirePermissions("rooms.feedbacks.summary")]
    [ProducesResponseType(typeof(ResponseHttp<RoomFeedbackSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSummary(
        [FromRoute, Required] string roomId,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        GetRoomFeedbackSummaryCommand command = new(RoomId: roomId);

        Result<RoomFeedbackSummaryDto> result =
            await getSummaryUseCase.ExecuteAsync(
                inboxId,
                NewWorkerId(),
                command,
                ct);

        return ToResponse(
            result,
            summary => summary,
            "Room feedback summary retrieved successfully",
            "Error retrieving room feedback summary",
            inboxId);
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private static Page<RoomFeedbackDto> ToPageDto(
        Page<RoomFeedbackModel> page)
    {
        return Page<RoomFeedbackDto>.Create(
            page.Data
                .Select(RoomFeedbackMapper.ToDto)
                .ToArray(),
            page.TotalCount,
            page.PageNumber,
            page.PageSize);
    }
}