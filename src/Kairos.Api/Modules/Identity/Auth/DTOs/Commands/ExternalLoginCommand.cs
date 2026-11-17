namespace Kairos.Api.Modules.Identity.Auth.DTOs.Commands;

public sealed record ExternalLoginCommand(
    string Provider,
    string ProviderKey,
    string? Email,
    bool EmailVerified,
    string? FullName);