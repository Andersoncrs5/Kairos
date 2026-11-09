using Kairos.Api.Common.Result;
using Kairos.Api.Common.Validation;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Rooms.RoomFeedback.DTOs.Commands;
using Kairos.Api.Modules.Rooms.RoomFeedback.DTOs.Request;
using Kairos.Api.Modules.Rooms.RoomFeedback.Mapper;
using Kairos.Api.Modules.Rooms.RoomFeedback.Models;
using Kairos.Api.Modules.Rooms.RoomFeedback.Services.Interfaces;

namespace Kairos.Api.Modules.Rooms.RoomFeedback.Services.Providers;

public sealed class UpdateRoomFeedbackUseCase(IUnitOfWork uow)
    : IUpdateRoomFeedbackUseCase
{
    public async Task<Result<RoomFeedbackModel>> ExecuteAsync(
        UpdateRoomFeedbackCommand command,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        
        if (command is null)
        {
            return Result<RoomFeedbackModel>.ValidationFailure(
                ["Room feedback update command is required."]);
        }

        if (!UseCaseValidation.TryValidate(
                command,
                "Invalid room feedback update command.",
                out string[] errors))
        {
            return Result<RoomFeedbackModel>.ValidationFailure(errors);
        }
        
        UpdateRoomFeedbackDto dto = command.Dto;

        if (!UseCaseValidation.TryValidate(
                dto,
                "Invalid room feedback update data.",
                out errors))
        {
            return Result<RoomFeedbackModel>.ValidationFailure(errors);
        }

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

        RoomFeedbackModel? feedback =
            await uow.RoomFeedbackRepository.GetByIdAsync(feedbackId, ct);

        if (feedback is null)
        {
            return Result<RoomFeedbackModel>.NotFound(
                "Room feedback not found.");
        }

        if (feedback.UserId != requesterUserId)
        {
            return Result<RoomFeedbackModel>.Forbidden(
                "Only the author can update this room feedback.");
        }
        
        string? comment = RoomFeedbackMapper.NormalizeComment(dto.Comment);

        if (feedback.Rating == dto.Rating && feedback.Comment == comment && feedback.IssueType == dto.IssueType)
        {
            return Result<RoomFeedbackModel>.Success(feedback);
        }
        
        RoomFeedbackMapper.ApplyUpdate(dto, feedback);
        
        feedback.UpdatedAt = DateTimeOffset.UtcNow;
        
        return Result<RoomFeedbackModel>.Success(feedback);
    }
}