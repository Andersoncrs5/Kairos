using System.ComponentModel.DataAnnotations;
using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Reservations.ReservationAttachment.DTOs.Commands;
using Kairos.Api.Modules.Reservations.ReservationAttachment.DTOs.Request;
using Kairos.Api.Modules.Reservations.ReservationAttachment.Mapper;
using Kairos.Api.Modules.Reservations.ReservationAttachment.Models;
using Kairos.Api.Modules.Reservations.ReservationAttachment.Services.Interfaces;

namespace Kairos.Api.Modules.Reservations.ReservationAttachment.Services.Providers;

public sealed class UpdateReservationAttachmentUseCase(
    IUnitOfWork uow,
    ILogger<UpdateReservationAttachmentUseCase> logger)
    : IUpdateReservationAttachmentUseCase
{
    public async Task<Result<ReservationAttachmentModel>> ExecuteAsync(
        UpdateReservationAttachmentCommand command,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        // ---------------------------------------------------------------
        // 1. Validate command
        // ---------------------------------------------------------------

        if (command is null)
        {
            return Result<ReservationAttachmentModel>.ValidationFailure(
                ["Reservation attachment command is required."]);
        }

        List<ValidationResult> validationResults = [];

        if (!Validator.TryValidateObject(
                command,
                new ValidationContext(command),
                validationResults,
                validateAllProperties: true))
        {
            return Result<ReservationAttachmentModel>.ValidationFailure(
                validationResults
                    .Select(result =>
                        result.ErrorMessage ??
                        "Invalid reservation attachment command.")
                    .Distinct()
                    .ToArray());
        }

        // ---------------------------------------------------------------
        // 2. Validate DTO
        // ---------------------------------------------------------------

        if (command.Dto is null)
        {
            return Result<ReservationAttachmentModel>.ValidationFailure(
                ["Reservation attachment data is required."]);
        }

        UpdateReservationAttachmentDto dto = command.Dto;

        validationResults = [];

        if (!Validator.TryValidateObject(
                dto,
                new ValidationContext(dto),
                validationResults,
                validateAllProperties: true))
        {
            return Result<ReservationAttachmentModel>.ValidationFailure(
                validationResults
                    .Select(result =>
                        result.ErrorMessage ??
                        "Invalid reservation attachment data.")
                    .Distinct()
                    .ToArray());
        }

        // ---------------------------------------------------------------
        // 3. Validate and normalize attachment ID
        // ---------------------------------------------------------------

        if (!Guid.TryParse(command.AttachmentId, out Guid attachmentGuid))
        {
            return Result<ReservationAttachmentModel>.ValidationFailure(
                ["Reservation attachment ID must be a valid UUID."]);
        }

        string attachmentId = attachmentGuid.ToString();

        // ---------------------------------------------------------------
        // 4. Get current attachment with row lock
        // ---------------------------------------------------------------

        ReservationAttachmentModel? attachment =
            await uow.ReservationAttachmentRepository
                .FindByIdWithForUpdateAsync(
                    attachmentId,
                    cancellationToken: ct);

        if (attachment is null)
        {
            return Result<ReservationAttachmentModel>.NotFound(
                "Reservation attachment not found.");
        }

        // ---------------------------------------------------------------
        // 5. Apply updates
        // ---------------------------------------------------------------

        ReservationAttachmentMapper.ApplyUpdate(
            attachment,
            dto);

        // ---------------------------------------------------------------
        // 6. Update audit fields
        // ---------------------------------------------------------------

        attachment.UpdatedAt = DateTimeOffset.UtcNow;
        
        // ---------------------------------------------------------------
        // 7. Log update
        // ---------------------------------------------------------------

        logger.LogInformation(
            "Reservation attachment {AttachmentId} updated by user {UserId}.",
            attachment.Id,
            command.RequesterUserId);

        // ---------------------------------------------------------------
        // 8. Return result
        // ---------------------------------------------------------------

        return Result<ReservationAttachmentModel>.Success(attachment);
    }
}
