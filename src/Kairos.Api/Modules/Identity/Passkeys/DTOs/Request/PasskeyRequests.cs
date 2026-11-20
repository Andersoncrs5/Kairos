using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Fido2NetLib;

namespace Kairos.Api.Modules.Identity.Passkeys.DTOs.Request;

public sealed class CompletePasskeyRegistrationDto
{
    [JsonPropertyName("flowId")]
    [Required(ErrorMessage = "Flow id is required.")]
    [StringLength(128, ErrorMessage = "Flow id is invalid.")]
    public required string FlowId { get; init; }

    [JsonPropertyName("name")]
    [StringLength(100, ErrorMessage = "Name cannot exceed 100 characters.")]
    public string? Name { get; init; }

    [JsonPropertyName("credential")]
    [Required(ErrorMessage = "Credential is required.")]
    public required AuthenticatorAttestationRawResponse Credential { get; init; }
}

public sealed class CompletePasskeyLoginDto
{
    [JsonPropertyName("flowId")]
    [Required(ErrorMessage = "Flow id is required.")]
    [StringLength(128, ErrorMessage = "Flow id is invalid.")]
    public required string FlowId { get; init; }

    [JsonPropertyName("credential")]
    [Required(ErrorMessage = "Credential is required.")]
    public required AuthenticatorAssertionRawResponse Credential { get; init; }
}