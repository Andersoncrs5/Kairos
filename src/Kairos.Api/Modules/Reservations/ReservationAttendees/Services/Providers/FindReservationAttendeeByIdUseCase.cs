using Kairos.Api.Common.Page;
using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Reservations.ReservationAttendees.DTOs.Commands;
using Kairos.Api.Modules.Reservations.ReservationAttendees.DTOs.Request;
using Kairos.Api.Modules.Reservations.ReservationAttendees.Models;
using Kairos.Api.Modules.Reservations.ReservationAttendees.Services.Interfaces;
using Kairos.Api.Modules.Reservations.ReservationAttendees.Services.Rules;

namespace Kairos.Api.Modules.Reservations.ReservationAttendees.Services.Providers;

public sealed class FindReservationAttendeeByIdUseCase(IUnitOfWork uow)
    : IFindReservationAttendeeByIdUseCase
{
    private const string ViewAllPermission = "reservations.read";

    public async Task<Result<ReservationAttendeeModel>> ExecuteAsync(
        FindReservationAttendeeByIdCommand command,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        // ---------------------------------------------------------------
        // 1. Validate command
        // ---------------------------------------------------------------

        if (command is null)
        {
            return Result<ReservationAttendeeModel>.ValidationFailure(
                ["Attendee search command is required."]);
        }

        if (!ReservationAttendeeRules.TryValidate(
                command,
                "Invalid attendee search command.",
                out string[] errors))
        {
            return Result<ReservationAttendeeModel>.ValidationFailure(errors);
        }

        // ---------------------------------------------------------------
        // 2. Validate identifiers
        // ---------------------------------------------------------------

        if (!ReservationAttendeeRules.TryParseUuid(
                command.ReservationId,
                out string reservationId))
        {
            return Result<ReservationAttendeeModel>.ValidationFailure(
                ["Reservation ID must be a valid UUID."]);
        }

        if (!ReservationAttendeeRules.TryParseUuid(
                command.AttendeeId,
                out string attendeeId))
        {
            return Result<ReservationAttendeeModel>.ValidationFailure(
                ["Attendee ID must be a valid UUID."]);
        }

        if (!ReservationAttendeeRules.TryParseUuid(
                command.RequesterUserId,
                out string requesterUserId))
        {
            return Result<ReservationAttendeeModel>.ValidationFailure(
                ["Requester user ID must be a valid UUID."]);
        }

        // ---------------------------------------------------------------
        // 3. Find attendee with Reservation, User and Inviter loaded
        //    (reuses Find, which already includes the navigations)
        // ---------------------------------------------------------------

        Page<ReservationAttendeeModel> page =
            await uow.ReservationAttendeeRepository.Find(
                new FilterReservationAttendeeDto
                {
                    Id = attendeeId,
                    ReservationId = reservationId,
                    Page = 1,
                    PageSize = 1
                },
                ct);

        ReservationAttendeeModel? attendee = page.Data.FirstOrDefault();

        if (attendee is null)
        {
            return Result<ReservationAttendeeModel>.NotFound(
                "Attendee not found.");
        }

        // ---------------------------------------------------------------
        // 4. Visibility: the attendee, the organizer, another attendee
        //    of the same reservation, or someone with "view all".
        //    Anyone else gets NotFound, so existence is not revealed.
        // ---------------------------------------------------------------

        bool allowed =
            attendee.UserId == requesterUserId ||
            attendee.Reservation?.UserId == requesterUserId ||
            await uow.ReservationAttendeeRepository
                .ExistsByReservationAndUserAsync(
                    reservationId,
                    requesterUserId,
                    ct) ||
            await uow.UserRepository.ExistsPermission(
                ViewAllPermission,
                requesterUserId);

        if (!allowed)
        {
            return Result<ReservationAttendeeModel>.NotFound(
                "Attendee not found.");
        }

        return Result<ReservationAttendeeModel>.Success(attendee);
    }
}