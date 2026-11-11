using System;
using System.Linq;

using Kairos.Api.Common.Enums;
using Kairos.Api.Modules.Identity.Users.DTOs.Response;
using Kairos.Api.Modules.Reservations.Reservation.DTOs.Response;
using Kairos.Api.Modules.Reservations.ReservationAttachment.DTOs.Request;
using Kairos.Api.Modules.Reservations.ReservationAttachment.DTOs.Response;
using Kairos.Api.Modules.Reservations.ReservationAttachment.Models;

namespace Kairos.Api.Modules.Reservations.ReservationAttachment.Mapper;

public static class ReservationAttachmentMapper
{
    
    public static ReservationAttachmentModel ToModel(
        CreateReservationAttachmentDto dto,
        string reservationId,
        string storageKey,
        string sha256,
        string uploadedBy)
    {
        ArgumentNullException.ThrowIfNull(dto);
        ArgumentException.ThrowIfNullOrWhiteSpace(reservationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(storageKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(sha256);
        ArgumentException.ThrowIfNullOrWhiteSpace(uploadedBy);

        return new ReservationAttachmentModel
        {
            Id = Guid.NewGuid().ToString(),

            ReservationId = reservationId,

            StorageKey = storageKey,

            OriginalFileName = NormalizeFileName(dto.File.FileName),

            ContentType = string.IsNullOrWhiteSpace(dto.File.ContentType)
                ? "application/octet-stream"
                : dto.File.ContentType,

            SizeBytes = dto.File.Length,

            Sha256 = sha256,

            DisplayName = dto.DisplayName.Trim(),

            Description = NormalizeDescription(dto.Description),

            Visibility = dto.Visibility,

            ScanStatus = FileScanStatusEnum.NotScanned,

            ScannedAt = null,

            UploadedBy = uploadedBy,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    public static void ApplyUpdate(
        ReservationAttachmentModel model,
        UpdateReservationAttachmentDto dto)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(dto);

        model.DisplayName = dto.DisplayName.Trim();
        model.Description = NormalizeDescription(dto.Description);
        model.Visibility = dto.Visibility;
    }

    public static ReservationAttachmentDto ToDto(
        ReservationAttachmentModel model,
        string? downloadUrl = null,
        ReservationDto? reservation = null,
        UserDTO? uploader = null)
    {
        ArgumentNullException.ThrowIfNull(model);

        return new ReservationAttachmentDto
        {
            Id = model.Id,
            Version = model.Version,
            CreatedAt = model.CreatedAt,
            UpdatedAt = model.UpdatedAt,

            ReservationId = model.ReservationId,
            Reservation = reservation,

            OriginalFileName = model.OriginalFileName,
            ContentType = model.ContentType,
            SizeBytes = model.SizeBytes,
            Sha256 = model.Sha256,

            DisplayName = model.DisplayName,
            Description = model.Description,

            Visibility = model.Visibility,

            ScanStatus = model.ScanStatus,
            ScannedAt = model.ScannedAt,

            UploadedBy = model.UploadedBy,
            Uploader = uploader,

            DownloadUrl = downloadUrl
        };
    }

    public static IReadOnlyCollection<ReservationAttachmentDto> ToDto(
        IEnumerable<ReservationAttachmentModel> models)
    {
        ArgumentNullException.ThrowIfNull(models);

        return models
            .Select(model => ToDto(model))
            .ToArray();
    }

    private static string NormalizeFileName(string fileName)
    {
        string normalized = fileName.Replace('\\', '/');

        int lastSeparator = normalized.LastIndexOf('/');

        string result = lastSeparator >= 0
            ? normalized[(lastSeparator + 1)..]
            : normalized;

        return result.Trim();
    }

    private static string? NormalizeDescription(string? description)
    {
        return string.IsNullOrWhiteSpace(description)
            ? null
            : description.Trim();
    }
}