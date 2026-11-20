using System.Text;
using System.Text.Json;
using Fido2NetLib;
using Fido2NetLib.Objects;
using Kairos.Api.Common.Result;
using Kairos.Api.Common.Validation;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Identity.Passkeys.DTOs.Response;
using Kairos.Api.Modules.Identity.Passkeys.Models;
using Kairos.Api.Modules.Identity.Passkeys.Services.Interfaces;
using Kairos.Api.Modules.Identity.Users.Models;

namespace Kairos.Api.Modules.Identity.Passkeys.Services.Providers;

public sealed class CreatePasskeyRegistrationOptionsUseCase(
    IUnitOfWork uow,
    IFido2 fido2,
    IPasskeyFlowStore flowStore)
    : ICreatePasskeyRegistrationOptionsUseCase
{
    private const int MaxPasskeysPerUser = 10;

    public async Task<Result<PasskeyOptionsResponse>> ExecuteAsync(
        string userId,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (!UseCaseValidation.TryParseUuid(userId, out string id))
        {
            return Result<PasskeyOptionsResponse>.ValidationFailure(
                ["User ID must be a valid UUID."]);
        }

        UserModel? user = await uow.UserRepository.GetByIdAsync(id);

        if (user is null)
            return Result<PasskeyOptionsResponse>.NotFound("User not found.");

        if (!user.IsActive)
        {
            return Result<PasskeyOptionsResponse>.Unauthorized(
                "User is not active");
        }

        IReadOnlyList<UserPasskeyModel> existing =
            await uow.UserPasskeyRepository.GetByUserIdAsync(id, ct);

        if (existing.Count >= MaxPasskeysPerUser)
        {
            return Result<PasskeyOptionsResponse>.Conflict(
                $"You can register at most {MaxPasskeysPerUser} passkeys.");
        }

        Fido2User fidoUser = new()
        {
            // The user handle: lets the authenticator return it at login.
            Id = Encoding.UTF8.GetBytes(user.Id),
            Name = user.Email ?? user.UserName ?? user.Id,
            DisplayName = user.FullName ?? user.UserName ?? user.Email ?? user.Id
        };

        // Prevents registering the same authenticator twice.
        List<PublicKeyCredentialDescriptor> exclude = existing
            .Select(passkey => new PublicKeyCredentialDescriptor(
                passkey.CredentialId))
            .ToList();

        CredentialCreateOptions options = fido2.RequestNewCredential(
            new RequestNewCredentialParams
            {
                User = fidoUser,
                ExcludeCredentials = exclude,
                AuthenticatorSelection = new AuthenticatorSelection
                {
                    // Discoverable credential = a real passkey.
                    ResidentKey = ResidentKeyRequirement.Required,
                    // Biometric/PIN every time: this is what makes it MFA.
                    UserVerification = UserVerificationRequirement.Required
                },
                AttestationPreference = AttestationConveyancePreference.None,
                Extensions = new AuthenticationExtensionsClientInputs
                {
                    CredProps = true
                }
            });

        string optionsJson = options.ToJson();

        string flowId = await flowStore.SaveAsync(
            PasskeyFlowKind.Registration,
            optionsJson,
            id,
            ct);

        return Result<PasskeyOptionsResponse>.Success(new PasskeyOptionsResponse
        {
            FlowId = flowId,
            Options = JsonSerializer.Deserialize<JsonElement>(optionsJson)
        });
    }
}