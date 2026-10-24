using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Identity.Permissions.Services.Interface;

namespace Kairos.Api.Modules.Identity.Permissions.Services.Provider;

public sealed class ExistsPermissionByCodeUseCase(IUnitOfWork uow)
    : IExistsPermissionByCodeUseCase
{
    private const int MaxCodeLength = 100;

    public async Task<Result<bool>> ExecuteAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return Result<bool>.ValidationFailure(
                ["Permission code is required."]);
        }

        var normalizedCode = code.Trim();

        if (normalizedCode.Length > MaxCodeLength)
        {
            return Result<bool>.ValidationFailure(
                [$"Permission code cannot exceed {MaxCodeLength} characters."]);
        }

        var exists = await uow.PermissionRepository.ExistsByCodeAsync(
            normalizedCode,
            cancellationToken);

        return Result<bool>.Success(exists);
    }
}