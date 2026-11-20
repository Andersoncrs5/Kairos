using System.Text.Json;
using Fido2NetLib;
using Fido2NetLib.Objects;
using Kairos.Api.Common.Result;
using Kairos.Api.Modules.Identity.Passkeys.DTOs.Response;
using Kairos.Api.Modules.Identity.Passkeys.Services.Interfaces;

namespace Kairos.Api.Modules.Identity.Passkeys.Services.Providers;

public sealed class CreatePasskeyLoginOptionsUseCase(
    IFido2 fido2,
    IPasskeyFlowStore flowStore)
    : ICreatePasskeyLoginOptionsUseCase
{
    public async Task<Result<PasskeyOptionsResponse>> ExecuteAsync(
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        AssertionOptions options = fido2.GetAssertionOptions(
            new GetAssertionOptionsParams
            {
                AllowedCredentials = [],
                UserVerification = UserVerificationRequirement.Required
            });

        string optionsJson = options.ToJson();

        string flowId = await flowStore.SaveAsync(
            PasskeyFlowKind.Login,
            optionsJson,
            null,
            ct);

        return Result<PasskeyOptionsResponse>.Success(new PasskeyOptionsResponse
        {
            FlowId = flowId,
            Options = JsonSerializer.Deserialize<JsonElement>(optionsJson)
        });
    }
}