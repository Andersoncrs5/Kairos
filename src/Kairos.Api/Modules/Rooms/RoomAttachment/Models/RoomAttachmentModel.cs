using Kairos.Api.Common.Base.Model;
using Kairos.Api.Common.Enums;
using Kairos.Api.Modules.Identity.Users.Models;
using Kairos.Api.Modules.Rooms.Room.Models;

namespace Kairos.Api.Modules.Rooms.RoomAttachment.Models;

public class RoomAttachmentModel : BaseModel
{
    public required string RoomId { get; set; }
    public RoomModel? Room { get; set; }

    public RoomAttachmentTypeEnum Type { get; set; } = RoomAttachmentTypeEnum.Photo;

    public required string StorageKey { get; set; }
    public required string OriginalFileName { get; set; }
    public required string ContentType { get; set; }
    public long SizeBytes { get; set; }
    public required string Sha256 { get; set; }

    public string? AltText { get; set; }
    public int SortOrder { get; set; }
    public bool IsCover { get; set; }

    public string? CreatedBy { get; set; }
    public UserModel? Creator { get; set; }
}