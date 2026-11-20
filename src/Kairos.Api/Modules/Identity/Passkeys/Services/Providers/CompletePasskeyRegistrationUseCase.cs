using Fido2NetLib;
using Fido2NetLib.Objects;
using Kairos.Api.Common.Result;
using Kairos.Api.Common.Validation;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Identity.Passkeys.DTOs.Commands;
using Kairos.Api.Modules.Identity.Passkeys.DTOs.Response;
using Kairos.Api.Modules.Identity.Passkeys.Mapper;
using Kairos.Api.Modules.Identity.Passkeys.Models;
using Kairos.Api.Modules.Identity.Passkeys.Services.Interfaces;

namespace Kairos.Api.Modules.Identity.Passkeys.Services.Providers;

public sealed class CompletePasskeyRegistrationUseCase(
    IUnitOfWork uow,
    IFido2 fido2,
    IPasskeyFlowStore flowStore)
    : ICompletePasskeyRegistrationUseCase
{
    public async Task<Result<PasskeyDTO>> ExecuteAsync(
        CompletePasskeyRegistrationCommand command,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (!UseCaseValidation.TryParseUuid(command.UserId, out string userId))
        {
            return Result<PasskeyDTO>.ValidationFailure(
                ["User ID must be a valid UUID."]);
        }

        // One-shot: the flow is deleted as soon as it is read.
        PasskeyFlow? flow = await flowStore.TakeAsync(
            command.FlowId,
            PasskeyFlowKind.Registration,
            ct);

        if (flow is null || flow.UserId != userId)
        {
            return Result<PasskeyDTO>.ValidationFailure(
                ["The passkey registration expired or is invalid. Start again."]);
        }

        CredentialCreateOptions options =
            CredentialCreateOptions.FromJson(flow.OptionsJson);

        RegisteredPublicKeyCredential credential;

        try
        {
            credential = await fido2.MakeNewCredentialAsync(
                new MakeNewCredentialParams
                {
                    AttestationResponse = command.Credential,
                    OriginalOptions = options,
                    IsCredentialIdUniqueToUserCallback = async (args, token) =>
                        !await uow.UserPasskeyRepository
                            .ExistsByCredentialIdAsync(args.CredentialId, token)
                },
                cancellationToken: ct);
        }
        catch (Fido2VerificationException)
        {
            return Result<PasskeyDTO>.ValidationFailure(
                ["The passkey could not be verified."]);
        }

        string name = string.IsNullOrWhiteSpace(command.Name)
            ? $"Passkey {DateTimeOffset.UtcNow:yyyy-MM-dd}"
            : command.Name.Trim();

        if (name.Length > 100)
            name = name[..100];

        UserPasskeyModel passkey = new()
        {
            Id = Guid.NewGuid().ToString(),
            UserId = userId,
            CredentialId = credential.Id,
            PublicKey = credential.PublicKey,
            SignCount = credential.SignCount,
            Name = name,
            IsBackupEligible = credential.IsBackupEligible,
            IsBackedUp = credential.IsBackedUp,
            Version = 1,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await uow.UserPasskeyRepository.AddAsync(passkey, ct);

        return Result<PasskeyDTO>.Created(PasskeyMapper.ToDto(passkey));
    }
}