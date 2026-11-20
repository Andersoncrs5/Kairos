using Kairos.Api.Common.Base.Services;
using Kairos.Api.Common.Result;
using Kairos.Api.Modules.Identity.Auth.DTOs.Response;
using Kairos.Api.Modules.Identity.Passkeys.DTOs.Commands;
using Kairos.Api.Modules.Identity.Passkeys.DTOs.Response;

namespace Kairos.Api.Modules.Identity.Passkeys.Services.Interfaces;

/// <summary>Input: the authenticated user id.</summary>
public interface ICreatePasskeyRegistrationOptionsUseCase
    : IUseCase<string, Result<PasskeyOptionsResponse>> { }

public interface ICompletePasskeyRegistrationUseCase
    : IUseCase<CompletePasskeyRegistrationCommand, Result<PasskeyDTO>> { }

public interface ICreatePasskeyLoginOptionsUseCase
{
    Task<Result<PasskeyOptionsResponse>> ExecuteAsync(
        CancellationToken ct = default);
}

public interface ICompletePasskeyLoginUseCase
    : IUseCase<CompletePasskeyLoginCommand, Result<Tokens>> { }

/// <summary>Input: the authenticated user id.</summary>
public interface IListPasskeysUseCase
    : IUseCase<string, Result<IReadOnlyList<PasskeyDTO>>> { }

public interface IDeletePasskeyUseCase
    : IUseCase<DeletePasskeyCommand, Result<bool>> { }