using System.Text.Json;
using System.Text.Json.Serialization;

namespace Kairos.Api.Modules.Identity.Passkeys.DTOs.Response;

public sealed class PasskeyOptionsResponse
{
    [JsonPropertyName("flowId")]
    public required string FlowId { get; init; }

    /// <summary>
    /// WebAuthn options, to pass to navigator.credentials.create/get.
    /// </summary>
    [JsonPropertyName("options")]
    public required JsonElement Options { get; init; }
}

public sealed class PasskeyDTO
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? LastUsedAt { get; init; }

    public bool IsBackupEligible { get; init; }

    public bool IsBackedUp { get; init; }
}