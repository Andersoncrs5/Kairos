using Kairos.Api.Common.Result;
using Kairos.Api.Common.Validation;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Rooms.RoomFeedback.DTOs.Commands;
using Kairos.Api.Modules.Rooms.RoomFeedback.Models;
using Kairos.Api.Modules.Rooms.RoomFeedback.Services.Interfaces;
using Kairos.Api.Modules.Rooms.RoomFeedback.Services.Rules;

namespace Kairos.Api.Modules.Rooms.RoomFeedback.Services.Providers;

public sealed class DeleteRoomFeedbackUseCase(IUnitOfWork uow)
    : IDeleteRoomFeedbackUseCase
{
    public async Task<Result<bool>> ExecuteAsync(
        DeleteRoomFeedbackCommand command,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        // ---------------------------------------------------------------
        // 1. Validate command
        // ---------------------------------------------------------------

        if (command is null)
        {
            return Result<bool>.ValidationFailure(
                ["Room feedback delete command is required."]);
        }

        if (!UseCaseValidation.TryValidate(
                command,
                "Invalid room feedback delete command.",
                out string[] errors))
        {
            return Result<bool>.ValidationFailure(errors);
        }

        // ---------------------------------------------------------------
        // 2. Validate identifiers
        // ---------------------------------------------------------------

        if (!UseCaseValidation.TryParseUuid(
                command.FeedbackId,
                out string feedbackId))
        {
            return Result<bool>.ValidationFailure(
                ["Room feedback ID must be a valid UUID."]);
        }

        if (!UseCaseValidation.TryParseUuid(
                command.RequesterUserId,
                out string requesterUserId))
        {
            return Result<bool>.ValidationFailure(
                ["Requester user ID must be a valid UUID."]);
        }

        // ---------------------------------------------------------------
        // 3. Find feedback (tracked, so it can be removed)
        // ---------------------------------------------------------------

        RoomFeedbackModel? feedback =
            await uow.RoomFeedbackRepository.GetByIdAsync(feedbackId, ct);

        if (feedback is null)
        {
            return Result<bool>.NotFound("Room feedback not found.");
        }

        // ---------------------------------------------------------------
        // 4. Authorization: the author or a moderator
        // ---------------------------------------------------------------

        bool allowed = feedback.UserId == requesterUserId || await RoomFeedbackRules.CanManageAsync(uow, requesterUserId);

        if (!allowed)
            return Result<bool>.Forbidden("Only the author or a moderator can delete this room feedback.");
        
        uow.RoomFeedbackRepository.Delete(feedback);

        return Result<bool>.Success(true);
    }
}