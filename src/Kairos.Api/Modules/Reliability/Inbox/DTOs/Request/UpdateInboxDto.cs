using System.ComponentModel.DataAnnotations;
using Kairos.Api.Common.Enums;

namespace Kairos.Api.Modules.Reliability.Inbox.DTOs.Request;

public class UpdateInboxDto
{
    [EnumDataType(typeof(InboxStatusEnum))]
    public required InboxStatusEnum Status { get; set; }

    public DateTimeOffset? ProcessedAt { get; set; }

    [Range(0, int.MaxValue)]
    public int RetryCount { get; set; }

    public DateTimeOffset? NextAttemptAt { get; set; }

    public string? LastError { get; set; }

    [StringLength(256)]
    public string? LockedBy { get; set; }
}