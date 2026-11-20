using Kairos.Api.Common.Result;
using Kairos.Api.Common.Validation;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Identity.Passkeys.DTOs.Response;
using Kairos.Api.Modules.Identity.Passkeys.Mapper;
using Kairos.Api.Modules.Identity.Passkeys.Models;
using Kairos.Api.Modules.Identity.Passkeys.Services.Interfaces;

namespace Kairos.Api.Modules.Identity.Passkeys.Services.Providers;

public sealed class ListPasskeysUseCase(IUnitOfWork uow)
    : IListPasskeysUseCase
{
    public async Task<Result<IReadOnlyList<PasskeyDTO>>> ExecuteAsync(
        string userId,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (!UseCaseValidation.TryParseUuid(userId, out string id))
        {
            return Result<IReadOnlyList<PasskeyDTO>>.ValidationFailure(
                ["User ID must be a valid UUID."]);
        }

        IReadOnlyList<UserPasskeyModel> passkeys =
            await uow.UserPasskeyRepository.GetByUserIdAsync(id, ct);

        IReadOnlyList<PasskeyDTO> items =
            passkeys.Select(PasskeyMapper.ToDto).ToList();

        return Result<IReadOnlyList<PasskeyDTO>>.Success(items);
    }
}