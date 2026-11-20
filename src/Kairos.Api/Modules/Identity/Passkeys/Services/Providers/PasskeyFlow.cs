namespace Kairos.Api.Modules.Identity.Passkeys.Services.Providers;

public enum PasskeyFlowKind
{
    Registration,
    Login
}

public sealed record PasskeyFlow(
    PasskeyFlowKind Kind,
    string OptionsJson,
    string? UserId);