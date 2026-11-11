using Kairos.Api.Common.Base.Model;
using Kairos.Api.Common.Enums;
using Kairos.Api.Modules.Identity.Users.DTOs.Response;
using Kairos.Api.Modules.Rooms.Room.DTOs.Response;

namespace Kairos.Api.Modules.Rooms.RoomAttachment.DTOs.Response;

public sealed class RoomAttachmentDto : BaseDTO
{
    public required string RoomId { get; init; }

    public RoomDto? Room { get; init; }

    public RoomAttachmentTypeEnum Type { get; init; }

    public required string OriginalFileName { get; init; }

    public required string ContentType { get; init; }

    public long SizeBytes { get; init; }

    public required string Sha256 { get; init; }

    public string? AltText { get; init; }

    public int SortOrder { get; init; }

    public bool IsCover { get; init; }

    public string? CreatedBy { get; init; }

    public UserDTO? Creator { get; init; }

    public string? DownloadUrl { get; init; }
}