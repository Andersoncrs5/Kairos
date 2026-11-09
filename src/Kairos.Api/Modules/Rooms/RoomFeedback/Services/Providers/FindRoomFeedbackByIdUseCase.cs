using Kairos.Api.Common.Result;
using Kairos.Api.Common.Validation;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Rooms.RoomFeedback.DTOs.Commands;
using Kairos.Api.Modules.Rooms.RoomFeedback.Models;
using Kairos.Api.Modules.Rooms.RoomFeedback.Services.Interfaces;
using Kairos.Api.Modules.Rooms.RoomFeedback.Services.Rules;

namespace Kairos.Api.Modules.Rooms.RoomFeedback.Services.Providers;

public sealed class FindRoomFeedbackByIdUseCase(IUnitOfWork uow)
    : IFindRoomFeedbackByIdUseCase
{
    public async Task<Result<RoomFeedbackModel>> ExecuteAsync(
        FindRoomFeedbackByIdCommand command,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        // ---------------------------------------------------------------
        // 1. Validate command
        // ---------------------------------------------------------------

        if (command is null)
        {
            return Result<RoomFeedbackModel>.ValidationFailure(
                ["Room feedback search command is required."]);
        }

        if (!UseCaseValidation.TryValidate(
                command,
                "Invalid room feedback search command.",
                out string[] errors))
        {
            return Result<RoomFeedbackModel>.ValidationFailure(errors);
        }

        // ---------------------------------------------------------------
        // 2. Validate identifiers
        // ---------------------------------------------------------------

        if (!UseCaseValidation.TryParseUuid(
                command.FeedbackId,
                out string feedbackId))
        {
            return Result<RoomFeedbackModel>.ValidationFailure(
                ["Room feedback ID must be a valid UUID."]);
        }

        if (!UseCaseValidation.TryParseUuid(
                command.RequesterUserId,
                out string requesterUserId))
        {
            return Result<RoomFeedbackModel>.ValidationFailure(
                ["Requester user ID must be a valid UUID."]);
        }

        // ---------------------------------------------------------------
        // 3. Find feedback (Reservation, Room and User loaded)
        // ---------------------------------------------------------------

        RoomFeedbackModel? feedback =
            await uow.RoomFeedbackRepository
                .GetByIdWithDetailsAsync(feedbackId, ct);

        if (feedback is null)
        {
            return Result<RoomFeedbackModel>.NotFound(
                "Room feedback not found.");
        }

        // ---------------------------------------------------------------
        // 4. Visibility: the author or a moderator.
        //    Anyone else gets NotFound, so existence is not revealed.
        // ---------------------------------------------------------------

        bool allowed =
            feedback.UserId == requesterUserId ||
            await RoomFeedbackRules.CanManageAsync(uow, requesterUserId);

        if (!allowed)
        {
            return Result<RoomFeedbackModel>.NotFound(
                "Room feedback not found.");
        }

        return Result<RoomFeedbackModel>.Success(feedback);
    }
}