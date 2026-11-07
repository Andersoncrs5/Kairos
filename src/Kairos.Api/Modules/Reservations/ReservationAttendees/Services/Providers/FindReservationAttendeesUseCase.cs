using Kairos.Api.Common.Page;
using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Reservations.Reservation.Models;
using Kairos.Api.Modules.Reservations.ReservationAttendees.DTOs.Commands;
using Kairos.Api.Modules.Reservations.ReservationAttendees.DTOs.Request;
using Kairos.Api.Modules.Reservations.ReservationAttendees.Models;
using Kairos.Api.Modules.Reservations.ReservationAttendees.Services.Interfaces;
using Kairos.Api.Modules.Reservations.ReservationAttendees.Services.Rules;

namespace Kairos.Api.Modules.Reservations.ReservationAttendees.Services.Providers;

public sealed class FindReservationAttendeesUseCase(IUnitOfWork uow)
    : IFindReservationAttendeesUseCase
{
    private const string ViewAllPermission = "reservations.list";

    public async Task<Result<Page<ReservationAttendeeModel>>> ExecuteAsync(
        FindReservationAttendeesCommand command,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        // ---------------------------------------------------------------
        // 1. Validate command
        // ---------------------------------------------------------------

        if (command is null)
            return Result<Page<ReservationAttendeeModel>>.ValidationFailure(["Attendee search command is required."]);

        if (!ReservationAttendeeRules.TryValidate(command, "Invalid attendee search command.", out string[] errors))
            return Result<Page<ReservationAttendeeModel>>.ValidationFailure(errors);

        // ---------------------------------------------------------------
        // 2. Validate filter
        // ---------------------------------------------------------------

        FilterReservationAttendeeDto dto = command.Dto;

        if (!ReservationAttendeeRules.TryValidate(
                dto,
                "Invalid attendee filter.",
                out errors))
        {
            return Result<Page<ReservationAttendeeModel>>.ValidationFailure(errors);
        }

        // ---------------------------------------------------------------
        // 3. Validate requester
        // ---------------------------------------------------------------

        if (!ReservationAttendeeRules.TryParseUuid(
                command.RequesterUserId,
                out string requesterUserId))
        {
            return Result<Page<ReservationAttendeeModel>>.ValidationFailure(
                ["Requester user ID must be a valid UUID."]);
        }

        // ---------------------------------------------------------------
        // 4. Visibility scope. Allowed when the search is:
        //    - restricted to the requester's own invitations; or
        //    - restricted to a reservation the requester organizes
        //      or takes part in; or
        //    - made by someone with the "view all" permission.
        // ---------------------------------------------------------------

        bool allowed =
            ReservationAttendeeRules.TryParseUuid(dto.UserId, out string filterUserId) &&
            filterUserId == requesterUserId;

        if (!allowed &&
            ReservationAttendeeRules.TryParseUuid(
                dto.ReservationId,
                out string filterReservationId))
        {
            ReservationModel? reservation =
                await uow.ReservationRepository.GetByIdAsync(
                    filterReservationId,
                    ct);

            if (reservation is not null)
            {
                allowed =
                    reservation.UserId == requesterUserId ||
                    await uow.ReservationAttendeeRepository
                        .ExistsByReservationAndUserAsync(
                            filterReservationId,
                            requesterUserId,
                            ct);
            }
        }

        if (!allowed)
        {
            allowed = await uow.UserRepository.ExistsPermission(
                ViewAllPermission,
                requesterUserId);
        }

        if (!allowed)
        {
            return Result<Page<ReservationAttendeeModel>>.Forbidden(
                "Filter by your own user ID or by a reservation you take part in.");
        }

        // ---------------------------------------------------------------
        // 5. Find attendees
        // ---------------------------------------------------------------

        Page<ReservationAttendeeModel> attendees =
            await uow.ReservationAttendeeRepository.Find(dto, ct);

        return Result<Page<ReservationAttendeeModel>>.Success(attendees);
    }
}