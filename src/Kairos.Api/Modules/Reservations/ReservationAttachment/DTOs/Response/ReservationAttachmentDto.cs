using Kairos.Api.Common.Base.Model;
using Kairos.Api.Common.Enums;
using Kairos.Api.Modules.Identity.Users.DTOs.Response;
using Kairos.Api.Modules.Reservations.Reservation.DTOs.Response;

namespace Kairos.Api.Modules.Reservations.ReservationAttachment.DTOs.Response;

public sealed class ReservationAttachmentDto : BaseDTO
{
    public required string ReservationId { get; init; }

    public ReservationDto? Reservation { get; init; }

    public required string OriginalFileName { get; init; }

    public required string ContentType { get; init; }

    public long SizeBytes { get; init; }

    public required string Sha256 { get; init; }

    public required string DisplayName { get; init; }

    public string? Description { get; init; }

    public ReservationAttachmentVisibilityEnum Visibility { get; init; }

    public FileScanStatusEnum ScanStatus { get; init; }

    public DateTimeOffset? ScannedAt { get; init; }

    public string? UploadedBy { get; init; }

    public UserDTO? Uploader { get; init; }
    
    public string? DownloadUrl { get; init; }
}