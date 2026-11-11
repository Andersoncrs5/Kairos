using Kairos.Api.Common.Base.Model;
using Kairos.Api.Common.Enums;
using Kairos.Api.Modules.Identity.Users.Models;
using Kairos.Api.Modules.Reservations.Reservation.Models;

namespace Kairos.Api.Modules.Reservations.ReservationAttachment.Models;

public class ReservationAttachmentModel : BaseModel
{
    public required string ReservationId { get; set; }
    public ReservationModel? Reservation { get; set; }

    public required string StorageKey { get; set; }
    public required string OriginalFileName { get; set; }
    public required string ContentType { get; set; }
    public long SizeBytes { get; set; }
    public required string Sha256 { get; set; }

    public required string DisplayName { get; set; }
    public string? Description { get; set; }

    public ReservationAttachmentVisibilityEnum Visibility { get; set; }
        = ReservationAttachmentVisibilityEnum.Participants;

    public FileScanStatusEnum ScanStatus { get; set; } = FileScanStatusEnum.NotScanned;
    public DateTimeOffset? ScannedAt { get; set; }

    public string? UploadedBy { get; set; }
    public UserModel? Uploader { get; set; }
}