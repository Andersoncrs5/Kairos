using Kairos.Api.Common.Result;
using Kairos.Api.Common.Validation;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Rooms.Room.Models;
using Kairos.Api.Modules.Rooms.RoomFeedback.DTOs.Commands;
using Kairos.Api.Modules.Rooms.RoomFeedback.DTOs.Response;
using Kairos.Api.Modules.Rooms.RoomFeedback.Mapper;
using Kairos.Api.Modules.Rooms.RoomFeedback.Repositories;
using Kairos.Api.Modules.Rooms.RoomFeedback.Services.Interfaces;
using Kairos.Api.Modules.Rooms.RoomFeedback.Summary;

namespace Kairos.Api.Modules.Rooms.RoomFeedback.Services.Providers;

public sealed class GetRoomFeedbackSummaryUseCase(IUnitOfWork uow): IGetRoomFeedbackSummaryUseCase
{
    public async Task<Result<RoomFeedbackSummaryDto>> ExecuteAsync(
        GetRoomFeedbackSummaryCommand command,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        // ---------------------------------------------------------------
        // 1. Validate command
        // ---------------------------------------------------------------

        if (command is null)
        {
            return Result<RoomFeedbackSummaryDto>.ValidationFailure(
                ["Room feedback summary command is required."]);
        }

        if (!UseCaseValidation.TryValidate(command, "Invalid room feedback summary command.", out string[] errors))
        {
            return Result<RoomFeedbackSummaryDto>.ValidationFailure(errors);
        }

        // ---------------------------------------------------------------
        // 2. Validate identifier
        // ---------------------------------------------------------------

        if (!UseCaseValidation.TryParseUuid(command.RoomId, out string roomId))
        {
            return Result<RoomFeedbackSummaryDto>.ValidationFailure(
                ["Room ID must be a valid UUID."]);
        }

        // ---------------------------------------------------------------
        // 3. Find room
        // ---------------------------------------------------------------

        RoomModel? room = await uow.RoomRepository.GetByIdAsync(roomId, ct);

        if (room is null)
        {
            return Result<RoomFeedbackSummaryDto>.NotFound(
                "Room doesn't exist.");
        }

        // ---------------------------------------------------------------
        // 4. Aggregate ratings (average + distribution from 1 to 5)
        // ---------------------------------------------------------------

        RoomFeedbackRatingSummary summary =
            await uow.RoomFeedbackRepository
                .GetRatingSummaryByRoomIdAsync(roomId, ct);

        return Result<RoomFeedbackSummaryDto>.Success(
            RoomFeedbackMapper.ToSummaryDto(roomId, summary));
    }
}