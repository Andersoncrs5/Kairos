using Kairos.Api.Modules.Identity.Passkeys.Services.Providers;

namespace Kairos.Api.Modules.Identity.Passkeys.Services.Interfaces;

public interface IPasskeyFlowStore
{
    Task<string> SaveAsync(
        PasskeyFlowKind kind,
        string optionsJson,
        string? userId,
        CancellationToken ct = default);

    /// <summary>
    /// Returns and deletes the flow in one atomic step, so a ceremony can be
    /// completed only once.
    /// </summary>
    Task<PasskeyFlow?> TakeAsync(
        string flowId,
        PasskeyFlowKind kind,
        CancellationToken ct = default);
}