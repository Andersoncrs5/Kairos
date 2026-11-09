using Kairos.Api.Common.Page;
using Kairos.Api.Common.Result;
using Kairos.Api.Common.Validation;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Rooms.RoomFeedback.DTOs.Commands;
using Kairos.Api.Modules.Rooms.RoomFeedback.DTOs.Request;
using Kairos.Api.Modules.Rooms.RoomFeedback.Models;
using Kairos.Api.Modules.Rooms.RoomFeedback.Services.Interfaces;
using Kairos.Api.Modules.Rooms.RoomFeedback.Services.Rules;

namespace Kairos.Api.Modules.Rooms.RoomFeedback.Services.Providers;

public sealed class FindRoomFeedbacksUseCase(IUnitOfWork uow)
    : IFindRoomFeedbacksUseCase
{
    public async Task<Result<Page<RoomFeedbackModel>>> ExecuteAsync(
        FindRoomFeedbacksCommand command,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        // ---------------------------------------------------------------
        // 1. Validate command
        // ---------------------------------------------------------------

        if (command is null)
        {
            return Result<Page<RoomFeedbackModel>>.ValidationFailure(
                ["Room feedback search command is required."]);
        }

        if (!UseCaseValidation.TryValidate(
                command,
                "Invalid room feedback search command.",
                out string[] errors))
        {
            return Result<Page<RoomFeedbackModel>>.ValidationFailure(errors);
        }

        // ---------------------------------------------------------------
        // 2. Validate filter (BaseFilter ranges + rating range)
        // ---------------------------------------------------------------

        FilterRoomFeedbackDto dto = command.Dto;

        if (!UseCaseValidation.TryValidate(
                dto,
                "Invalid room feedback filter.",
                out errors))
        {
            return Result<Page<RoomFeedbackModel>>.ValidationFailure(errors);
        }

        // ---------------------------------------------------------------
        // 3. Validate requester
        // ---------------------------------------------------------------

        if (!UseCaseValidation.TryParseUuid(
                command.RequesterUserId,
                out string requesterUserId))
        {
            return Result<Page<RoomFeedbackModel>>.ValidationFailure(
                ["Requester user ID must be a valid UUID."]);
        }

        // ---------------------------------------------------------------
        // 4. Visibility scope
        //    - moderators see everything;
        //    - everyone else only sees their own reviews
        //      (no UserId in the filter means "mine").
        // ---------------------------------------------------------------

        bool canManage =
            await RoomFeedbackRules.CanManageAsync(uow, requesterUserId);

        if (!canManage)
        {
            if (string.IsNullOrWhiteSpace(dto.UserId))
            {
                dto.UserId = requesterUserId;
            }
            else
            {
                bool isOwnFilter =
                    UseCaseValidation.TryParseUuid(
                        dto.UserId,
                        out string filterUserId) &&
                    filterUserId == requesterUserId;

                if (!isOwnFilter)
                {
                    return Result<Page<RoomFeedbackModel>>.Forbidden(
                        "You can only list your own room feedback.");
                }

                dto.UserId = filterUserId;
            }
        }

        // ---------------------------------------------------------------
        // 5. Find feedbacks
        // ---------------------------------------------------------------

        Page<RoomFeedbackModel> feedbacks =
            await uow.RoomFeedbackRepository.Find(dto, ct);

        return Result<Page<RoomFeedbackModel>>.Success(feedbacks);
    }
}