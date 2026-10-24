using Kairos.Api.Common.Base.Model;
using Kairos.Api.Common.Enums;
using System;

namespace Kairos.Api.Modules.Reliability.Inbox.DTOs.Response;

public class InboxDto : BaseDTO
{
    public required string MessageId { get; set; }

    public required string MessageType { get; set; }

    public required string Payload { get; set; }

    public string? Source { get; set; }

    public string? Headers { get; set; }

    public InboxStatusEnum Status { get; set; }

    public DateTimeOffset ReceivedAt { get; set; }

    public DateTimeOffset? ProcessedAt { get; set; }

    public int RetryCount { get; set; }

    public DateTimeOffset? NextAttemptAt { get; set; }

    public string? LastError { get; set; }

    public string? LockedBy { get; set; }

    public string? CorrelationId { get; set; }
}