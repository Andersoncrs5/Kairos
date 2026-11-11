using System.ComponentModel.DataAnnotations;

using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Rooms.RoomAttachment.DTOs.Commands;
using Kairos.Api.Modules.Rooms.RoomAttachment.DTOs.Request;
using Kairos.Api.Modules.Rooms.RoomAttachment.Mappers;
using Kairos.Api.Modules.Rooms.RoomAttachment.Models;
using Kairos.Api.Modules.Rooms.RoomAttachment.Services.Interfaces;

using Microsoft.Extensions.Logging;

namespace Kairos.Api.Modules.Rooms.RoomAttachment.Services.Providers;

public sealed class UpdateRoomAttachmentUseCase(
    IUnitOfWork uow,
    ILogger<UpdateRoomAttachmentUseCase> logger)
    : IUpdateRoomAttachmentUseCase
{
    public async Task<Result<RoomAttachmentModel>> ExecuteAsync(
        UpdateRoomAttachmentCommand command,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        // ---------------------------------------------------------------
        // 1. Validate command
        // ---------------------------------------------------------------

        if (command is null)
        {
            return Result<RoomAttachmentModel>.ValidationFailure(
                ["Room attachment command is required."]);
        }

        List<ValidationResult> validationResults = [];

        if (!Validator.TryValidateObject(
                command,
                new ValidationContext(command),
                validationResults,
                validateAllProperties: true))
        {
            return Result<RoomAttachmentModel>.ValidationFailure(
                validationResults
                    .Select(result =>
                        result.ErrorMessage ??
                        "Invalid room attachment command.")
                    .Distinct()
                    .ToArray());
        }

        // ---------------------------------------------------------------
        // 2. Validate DTO
        // ---------------------------------------------------------------

        if (command.Dto is null)
        {
            return Result<RoomAttachmentModel>.ValidationFailure(
                ["Room attachment data is required."]);
        }

        UpdateRoomAttachmentDto dto = command.Dto;

        validationResults = [];

        if (!Validator.TryValidateObject(
                dto,
                new ValidationContext(dto),
                validationResults,
                validateAllProperties: true))
        {
            return Result<RoomAttachmentModel>.ValidationFailure(
                validationResults
                    .Select(result =>
                        result.ErrorMessage ??
                        "Invalid room attachment data.")
                    .Distinct()
                    .ToArray());
        }

        // ---------------------------------------------------------------
        // 3. Validate and normalize attachment ID
        // ---------------------------------------------------------------

        if (!Guid.TryParse(command.AttachmentId, out Guid attachmentGuid))
        {
            return Result<RoomAttachmentModel>.ValidationFailure(
                ["Room attachment ID must be a valid UUID."]);
        }

        string attachmentId = attachmentGuid.ToString();

        // ---------------------------------------------------------------
        // 4. Get current attachment with row lock
        // ---------------------------------------------------------------

        RoomAttachmentModel? attachment = await uow
            .RoomAttachmentRepository
            .FindByIdWithForUpdateAsync(
                attachmentId,
                cancellationToken: ct);

        if (attachment is null)
        {
            return Result<RoomAttachmentModel>.NotFound(
                "Room attachment not found.");
        }

        // ---------------------------------------------------------------
        // 5. Validate business rules
        // ---------------------------------------------------------------

        if (dto.IsCover &&
            attachment.Type != Common.Enums.RoomAttachmentTypeEnum.Photo)
        {
            return Result<RoomAttachmentModel>.ValidationFailure(
                ["Only photo attachments can be marked as cover."]);
        }

        if (dto.SortOrder < 0)
        {
            return Result<RoomAttachmentModel>.ValidationFailure(
                ["Room attachment sort order cannot be negative."]);
        }

        // ---------------------------------------------------------------
        // 6. Apply updates
        // ---------------------------------------------------------------

        RoomAttachmentMapper.ApplyUpdate(dto, attachment);

        // ---------------------------------------------------------------
        // 7. Update audit fields
        // ---------------------------------------------------------------

        attachment.UpdatedAt = DateTimeOffset.UtcNow;

        // ---------------------------------------------------------------
        // 8. Log update
        // ---------------------------------------------------------------

        logger.LogInformation(
            "Room attachment {AttachmentId} updated by user {UserId}.",
            attachment.Id,
            command.RequesterUserId);

        // ---------------------------------------------------------------
        // 9. Return result
        // ---------------------------------------------------------------

        return Result<RoomAttachmentModel>.Success(attachment);
    }
}