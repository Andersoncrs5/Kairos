using System.ComponentModel.DataAnnotations;
using Kairos.Api.Common.Base.Model;
using Kairos.Api.Common.Enums;

namespace Kairos.Api.Modules.Reliability.Inbox.DTOs.Request;

public class FilterInboxDto : BaseFilter
{
    [StringLength(256)]
    public string? MessageId { get; set; }

    [StringLength(256)]
    public string? MessageType { get; set; }
    public bool? IgnoreCaseMessageType { get; set; }

    [StringLength(256)]
    public string? Source { get; set; }
    public bool? IgnoreCaseSource { get; set; }

    [EnumDataType(typeof(InboxStatusEnum))]
    public InboxStatusEnum? Status { get; set; }

    [StringLength(256)]
    public string? CorrelationId { get; set; }

    public DateTimeOffset? ReceivedFrom { get; set; }

    public DateTimeOffset? ReceivedTo { get; set; }
}