using System.ComponentModel.DataAnnotations;

namespace Kairos.Api.Modules.Reliability.Inbox.DTOs.Request;

public class CreateInboxDto
{
    [Required]
    [StringLength(256)]
    public required string MessageId { get; set; }

    [Required]
    [StringLength(256)]
    public required string MessageType { get; set; }

    [Required]
    public required string Payload { get; set; }

    [StringLength(256)]
    public string? Source { get; set; }

    public string? Headers { get; set; }

    [StringLength(256)]
    public string? CorrelationId { get; set; }
}