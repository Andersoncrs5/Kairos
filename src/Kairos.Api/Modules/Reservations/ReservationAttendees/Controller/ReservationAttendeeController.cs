using System.ComponentModel.DataAnnotations;
using Asp.Versioning;
using Kairos.Api.Common.Base.Controller;
using Kairos.Api.Common.Page;
using Kairos.Api.Common.Res;
using Kairos.Api.Common.Result;
using Kairos.Api.Config.Authentication;
using Kairos.Api.Config.DB.Transaction;
using Kairos.Api.Modules.Reservations.ReservationAttendees.DTOs.Commands;
using Kairos.Api.Modules.Reservations.ReservationAttendees.DTOs.Request;
using Kairos.Api.Modules.Reservations.ReservationAttendees.DTOs.Response;
using Kairos.Api.Modules.Reservations.ReservationAttendees.Mapper;
using Kairos.Api.Modules.Reservations.ReservationAttendees.Models;
using Kairos.Api.Modules.Reservations.ReservationAttendees.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kairos.Api.Modules.Reservations.ReservationAttendees.Controller;

[Route("api/v{version:apiVersion}/reservations")]
[ApiVersion("1.0")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public sealed class ReservationAttendeeController(
    IInboxTransactionUseCase<AddReservationAttendeeCommand, ReservationAttendeeModel, IAddReservationAttendeeUseCase> addAttendeeUseCase,
    IInboxTransactionUseCase<UpdateReservationAttendeeCommand, ReservationAttendeeModel, IUpdateReservationAttendeeUseCase> updateAttendeeUseCase,
    IInboxTransactionUseCase<RespondReservationAttendeeCommand, ReservationAttendeeModel, IRespondReservationAttendeeUseCase> respondAttendeeUseCase,
    IInboxTransactionUseCase<RemoveReservationAttendeeCommand, bool, IRemoveReservationAttendeeUseCase> removeAttendeeUseCase,
    IInboxTransactionUseCase<CheckInReservationAttendeeCommand, ReservationAttendeeModel, ICheckInReservationAttendeeUseCase> checkInAttendeeUseCase,
    IInboxTransactionUseCase<FindReservationAttendeesCommand, Page<ReservationAttendeeModel>, IFindReservationAttendeesUseCase> findAttendeesUseCase,
    IInboxTransactionUseCase<FindReservationAttendeeByIdCommand, ReservationAttendeeModel, IFindReservationAttendeeByIdUseCase> findAttendeeByIdUseCase
) : ApiControllerBase
{
    // ---------------------------------------------------------------
    // Find attendees of a reservation
    // ---------------------------------------------------------------

    /// <summary>
    /// Retrieves a paginated list of the attendees of a reservation.
    /// </summary>
    /// <remarks>
    /// The reservation is taken from the route. Only the organizer and the
    /// attendees of the reservation can list it.
    /// </remarks>
    [HttpGet("{reservationId}/attendees")]
    [RequirePermissions("reservations.attendees.list")]
    [ProducesResponseType(typeof(ResponseHttp<Page<ReservationAttendeeDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Find(
        [FromRoute, Required] string reservationId,
        [FromQuery] FilterReservationAttendeeDto dto,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        string requesterUserId = GetAuthenticatedUserId(inboxId);

        // The route always wins over any "reservationId" sent in the query
        dto.ReservationId = reservationId;

        Result<Page<ReservationAttendeeModel>> result =
            await findAttendeesUseCase.ExecuteAsync(
                inboxId,
                NewWorkerId(),
                new FindReservationAttendeesCommand(requesterUserId, dto),
                ct);

        return ToResponse(
            result,
            ToPageDto,
            "Reservation attendees retrieved successfully",
            "Error retrieving reservation attendees",
            inboxId);
    }

    // ---------------------------------------------------------------
    // Find my invitations
    // ---------------------------------------------------------------

    /// <summary>
    /// Retrieves a paginated list of the invitations of the authenticated user.
    /// </summary>
    /// <remarks>
    /// Use ReservationStartsAtFrom to list only upcoming invitations.
    /// </remarks>
    [HttpGet("attendees/me")]
    [RequirePermissions("reservations.attendees.list")]
    [ProducesResponseType(typeof(ResponseHttp<Page<ReservationAttendeeDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> FindMine(
        [FromQuery] FilterReservationAttendeeDto dto,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        string requesterUserId = GetAuthenticatedUserId(inboxId);

        // The user is always the authenticated one
        dto.UserId = requesterUserId;

        Result<Page<ReservationAttendeeModel>> result =
            await findAttendeesUseCase.ExecuteAsync(
                inboxId,
                NewWorkerId(),
                new FindReservationAttendeesCommand(requesterUserId, dto),
                ct);

        return ToResponse(
            result,
            ToPageDto,
            "Invitations retrieved successfully",
            "Error retrieving invitations",
            inboxId);
    }

    // ---------------------------------------------------------------
    // Find by ID
    // ---------------------------------------------------------------

    /// <summary>
    /// Retrieves a reservation attendee by its identifier.
    /// </summary>
    [HttpGet("{reservationId}/attendees/{attendeeId}")]
    [RequirePermissions("reservations.attendees.read")]
    [ProducesResponseType(typeof(ResponseHttp<ReservationAttendeeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> FindById(
        [FromRoute, Required] string reservationId,
        [FromRoute, Required] string attendeeId,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        FindReservationAttendeeByIdCommand command = new(
            ReservationId: reservationId,
            AttendeeId: attendeeId,
            RequesterUserId: GetAuthenticatedUserId(inboxId));

        Result<ReservationAttendeeModel> result =
            await findAttendeeByIdUseCase.ExecuteAsync(
                inboxId,
                NewWorkerId(),
                command,
                ct);

        return ToResponse(
            result,
            ReservationAttendeeMapper.ToDto,
            "Reservation attendee found",
            "Error finding reservation attendee",
            inboxId);
    }

    // ---------------------------------------------------------------
    // Add
    // ---------------------------------------------------------------

    /// <summary>
    /// Invites a user to a reservation.
    /// </summary>
    /// <remarks>
    /// Only the reservation organizer can add attendees.
    /// The new attendee starts with the Pending status.
    /// </remarks>
    [HttpPost("{reservationId}/attendees")]
    [RequirePermissions("reservations.attendees.create")]
    [ProducesResponseType(typeof(ResponseHttp<ReservationAttendeeDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Add(
        [FromRoute, Required] string reservationId,
        [FromBody] AddReservationAttendeeDto dto,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        AddReservationAttendeeCommand command = new(
            ReservationId: reservationId,
            RequesterUserId: GetAuthenticatedUserId(inboxId),
            Dto: dto);

        Result<ReservationAttendeeModel> result =
            await addAttendeeUseCase.ExecuteAsync(
                inboxId,
                NewWorkerId(),
                command,
                ct);

        return ToResponse(
            result,
            ReservationAttendeeMapper.ToDto,
            "Reservation attendee added successfully",
            "Error adding reservation attendee",
            inboxId,
            StatusCodes.Status201Created);
    }

    // ---------------------------------------------------------------
    // Update (role)
    // ---------------------------------------------------------------

    /// <summary>
    /// Updates the role of a reservation attendee.
    /// </summary>
    /// <remarks>
    /// Only the reservation organizer can update attendees.
    /// </remarks>
    [HttpPut("{reservationId}/attendees/{attendeeId}")]
    [RequirePermissions("reservations.attendees.update")]
    [ProducesResponseType(typeof(ResponseHttp<ReservationAttendeeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        [FromRoute, Required] string reservationId,
        [FromRoute, Required] string attendeeId,
        [FromBody] UpdateReservationAttendeeDto dto,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        UpdateReservationAttendeeCommand command = new(
            ReservationId: reservationId,
            AttendeeId: attendeeId,
            RequesterUserId: GetAuthenticatedUserId(inboxId),
            Dto: dto);

        Result<ReservationAttendeeModel> result =
            await updateAttendeeUseCase.ExecuteAsync(
                inboxId,
                NewWorkerId(),
                command,
                ct);

        return ToResponse(
            result,
            ReservationAttendeeMapper.ToDto,
            "Reservation attendee updated successfully",
            "Error updating reservation attendee",
            inboxId);
    }

    // ---------------------------------------------------------------
    // Respond to invitation
    // ---------------------------------------------------------------

    /// <summary>
    /// Accepts, declines or tentatively accepts the invitation
    /// of the authenticated user.
    /// </summary>
    /// <remarks>
    /// The status must be Accepted, Declined or Tentative.
    /// </remarks>
    [HttpPut("{reservationId}/attendees/me/response")]
    [RequirePermissions("reservations.attendees.respond")]
    [ProducesResponseType(typeof(ResponseHttp<ReservationAttendeeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Respond(
        [FromRoute, Required] string reservationId,
        [FromBody] RespondReservationAttendeeDto dto,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        RespondReservationAttendeeCommand command = new(
            ReservationId: reservationId,
            UserId: GetAuthenticatedUserId(inboxId),
            Dto: dto);

        Result<ReservationAttendeeModel> result =
            await respondAttendeeUseCase.ExecuteAsync(
                inboxId,
                NewWorkerId(),
                command,
                ct);

        return ToResponse(
            result,
            ReservationAttendeeMapper.ToDto,
            "Invitation answered successfully",
            "Error responding to invitation",
            inboxId);
    }

    // ---------------------------------------------------------------
    // Check-in
    // ---------------------------------------------------------------

    /// <summary>
    /// Registers the presence of an attendee in a reservation.
    /// </summary>
    /// <remarks>
    /// The attendee or the organizer can check in. Only attendees who
    /// accepted the invitation can check in, while the reservation is
    /// in progress.
    /// </remarks>
    [HttpPost("{reservationId:required:guid}/attendees/{attendeeUserId:required:guid}/check-in")]
    [RequirePermissions("reservations.attendees.respond")]
    [ProducesResponseType(typeof(ResponseHttp<ReservationAttendeeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CheckIn(
        [FromRoute, Required] string reservationId,
        [FromRoute, Required] string attendeeUserId,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        CheckInReservationAttendeeCommand command = new(
            ReservationId: reservationId,
            AttendeeUserId: attendeeUserId,
            RequesterUserId: GetAuthenticatedUserId(inboxId));

        Result<ReservationAttendeeModel> result =
            await checkInAttendeeUseCase.ExecuteAsync(
                inboxId,
                NewWorkerId(),
                command,
                ct);

        return ToResponse(
            result,
            ReservationAttendeeMapper.ToDto,
            "Attendee checked in successfully",
            "Error checking in attendee",
            inboxId);
    }

    // ---------------------------------------------------------------
    // Remove
    // ---------------------------------------------------------------

    /// <summary>
    /// Removes an attendee from a reservation.
    /// </summary>
    /// <remarks>
    /// The organizer can remove any attendee.
    /// An attendee can only remove themselves.
    /// </remarks>
    [HttpDelete("{reservationId}/attendees/{attendeeId}")]
    [RequirePermissions("reservations.attendees.delete")]
    [ProducesResponseType(typeof(ResponseHttp<bool?>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Remove(
        [FromRoute, Required] string reservationId,
        [FromRoute, Required] string attendeeId,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        RemoveReservationAttendeeCommand command = new(
            ReservationId: reservationId,
            AttendeeId: attendeeId,
            RequesterUserId: GetAuthenticatedUserId(inboxId));

        Result<bool> result =
            await removeAttendeeUseCase.ExecuteAsync(
                inboxId,
                NewWorkerId(),
                command,
                ct);

        return ToResponse(
            result,
            removed => (bool?)removed,
            "Reservation attendee removed successfully",
            "Error removing reservation attendee",
            inboxId);
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private static Page<ReservationAttendeeDto> ToPageDto(
        Page<ReservationAttendeeModel> page)
    {
        return Page<ReservationAttendeeDto>.Create(
            page.Data
                .Select(ReservationAttendeeMapper.ToDto)
                .ToArray(),
            page.TotalCount,
            page.PageNumber,
            page.PageSize);
    }
}