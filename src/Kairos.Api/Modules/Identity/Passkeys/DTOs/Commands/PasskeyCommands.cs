using Fido2NetLib;

namespace Kairos.Api.Modules.Identity.Passkeys.DTOs.Commands;

public sealed record CompletePasskeyRegistrationCommand(
    string UserId,
    string FlowId,
    string? Name,
    AuthenticatorAttestationRawResponse Credential);

public sealed record CompletePasskeyLoginCommand(
    string FlowId,
    AuthenticatorAssertionRawResponse Credential);

public sealed record DeletePasskeyCommand(
    string UserId,
    string PasskeyId);