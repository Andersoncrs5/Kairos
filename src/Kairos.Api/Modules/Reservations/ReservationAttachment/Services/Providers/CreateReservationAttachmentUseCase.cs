using System.ComponentModel.DataAnnotations;
using Kairos.Api.Common.Result;
using Kairos.Api.Config.Storage;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Reservations.ReservationAttachment.DTOs.Commands;
using Kairos.Api.Modules.Reservations.ReservationAttachment.DTOs.Request;
using Kairos.Api.Modules.Reservations.ReservationAttachment.Mapper;
using Kairos.Api.Modules.Reservations.ReservationAttachment.Models;
using Kairos.Api.Modules.Reservations.ReservationAttachment.Services.Interfaces;

namespace Kairos.Api.Modules.Reservations.ReservationAttachment.Services.Providers;

public sealed class CreateReservationAttachmentUseCase(
    IUnitOfWork uow,
    ILogger<CreateReservationAttachmentUseCase> logger)
    : ICreateReservationAttachmentUseCase
{
    public async Task<Result<ReservationAttachmentModel>> ExecuteAsync(
        CreateReservationAttachmentCommand command,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        // ---------------------------------------------------------------
        // 1. Validate command
        // ---------------------------------------------------------------

        if (command is null)
            return Result<ReservationAttachmentModel>.ValidationFailure(["Reservation attachment command is required."]);

        List<ValidationResult> validationResults = [];

        if (!Validator.TryValidateObject(command, new ValidationContext(command), validationResults, validateAllProperties: true))
        {
            return Result<ReservationAttachmentModel>.ValidationFailure(
                validationResults
                    .Select(result =>
                        result.ErrorMessage ?? "Invalid reservation attachment command.")
                    .Distinct()
                    .ToArray());
        }

        // ---------------------------------------------------------------
        // 2. Validate DTO
        // ---------------------------------------------------------------

        CreateReservationAttachmentDto dto = command.Dto;

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
                        result.ErrorMessage ?? "Invalid reservation attachment data.")
                    .Distinct()
                    .ToArray());
        }

        // ---------------------------------------------------------------
        // 3. Check reservation
        // ---------------------------------------------------------------

        bool reservationExists = await uow.ReservationRepository.ExistsAsync(command.ReservationId, ct);

        if (!reservationExists)
            return Result<ReservationAttachmentModel>.NotFound("The reservation was not found.");

        // ---------------------------------------------------------------
        // 4. Prepare file upload
        // ---------------------------------------------------------------

        string keyPrefix = $"reservations/{command.ReservationId}/attachments";

        using Stream stream = dto.File.OpenReadStream();

        UploadFileRequest uploadRequest = new(UploadKind.SupportMaterial, keyPrefix, stream);

        // ---------------------------------------------------------------
        // 5. Upload file
        // ---------------------------------------------------------------

        Result<StoredFile> uploadResult = await uow.FileStorageService.UploadAsync(uploadRequest, ct);

        if (uploadResult.IsFailure)
        {
            logger.LogWarning(
                "Failed to upload reservation attachment for reservation {ReservationId}. Errors: {Errors}",
                command.ReservationId,
                string.Join("; ", uploadResult.Errors));

            return Result<ReservationAttachmentModel>.Failure(uploadResult.Errors, uploadResult.StatusCode);
        }

        StoredFile? storedFile = uploadResult.Value;

        if (storedFile is null)
            return Result<ReservationAttachmentModel>.Failure("File storage returned an empty upload result.", 502);

        // ---------------------------------------------------------------
        // 6. Create attachment model
        // ---------------------------------------------------------------

        ReservationAttachmentModel attachment = ReservationAttachmentMapper.ToModel(
            dto,
            command.ReservationId,
            storedFile.Key,
            storedFile.Sha256,
            command.RequesterUserId);

        // ---------------------------------------------------------------
        // 7. Persist attachment
        // ---------------------------------------------------------------

        await uow.ReservationAttachmentRepository.AddAsync(attachment, ct);

        // ---------------------------------------------------------------
        // 8. Return result
        // ---------------------------------------------------------------

        logger.LogInformation(
            "Reservation attachment {AttachmentId} created for reservation {ReservationId}.",
            attachment.Id,
            attachment.ReservationId);

        return Result<ReservationAttachmentModel>.Created(attachment);
    }
}
