using System.Text;
using Fido2NetLib;
using Fido2NetLib.Objects;
using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Identity.Auth.DTOs.Response;
using Kairos.Api.Modules.Identity.Auth.Services.Interfaces;
using Kairos.Api.Modules.Identity.Passkeys.DTOs.Commands;
using Kairos.Api.Modules.Identity.Passkeys.Models;
using Kairos.Api.Modules.Identity.Passkeys.Services.Interfaces;
using Kairos.Api.Modules.Identity.Users.Models;

namespace Kairos.Api.Modules.Identity.Passkeys.Services.Providers;

public sealed class CompletePasskeyLoginUseCase(
    IUnitOfWork uow,
    IFido2 fido2,
    IPasskeyFlowStore flowStore,
    ICreateSessionUseCase createSessionUseCase)
    : ICompletePasskeyLoginUseCase
{
    public async Task<Result<Tokens>> ExecuteAsync(
        CompletePasskeyLoginCommand command,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        // One-shot: the challenge cannot be replayed.
        PasskeyFlow? flow = await flowStore.TakeAsync(
            command.FlowId,
            PasskeyFlowKind.Login,
            ct);

        if (flow is null)
        {
            return Result<Tokens>.Unauthorized(
                "Passkey login expired or invalid. Start again.");
        }

        AssertionOptions options = AssertionOptions.FromJson(flow.OptionsJson);

        UserPasskeyModel? passkey =
            await uow.UserPasskeyRepository.GetByCredentialIdAsync(
                command.Credential.RawId,
                ct);

        if (passkey is null)
            return Result<Tokens>.Unauthorized("Passkey not recognized.");

        // The user comes from our database row, never from client input.
        UserModel? user = await uow.UserRepository.GetByIdAsync(passkey.UserId);

        if (user is null || !user.IsActive)
            return Result<Tokens>.Unauthorized("Login invalid");

        byte[] expectedUserHandle = Encoding.UTF8.GetBytes(passkey.UserId);

        VerifyAssertionResult assertion;

        try
        {
            assertion = await fido2.MakeAssertionAsync(
                new MakeAssertionParams
                {
                    AssertionResponse = command.Credential,
                    OriginalOptions = options,
                    StoredPublicKey = passkey.PublicKey,
                    StoredSignatureCounter = (uint)passkey.SignCount,
                    IsUserHandleOwnerOfCredentialIdCallback = (args, token) =>
                        Task.FromResult(
                            args.UserHandle.AsSpan()
                                .SequenceEqual(expectedUserHandle) &&
                            args.CredentialId.AsSpan()
                                .SequenceEqual(passkey.CredentialId))
                },
                cancellationToken: ct);
        }
        catch (Fido2VerificationException)
        {
            return Result<Tokens>.Unauthorized("Passkey verification failed.");
        }

        passkey.SignCount = assertion.SignCount;
        passkey.IsBackedUp = assertion.IsBackedUp;
        passkey.LastUsedAt = DateTimeOffset.UtcNow;

        uow.UserPasskeyRepository.Update(passkey);

        return await createSessionUseCase.ExecuteAsync(user, ct);
    }
}