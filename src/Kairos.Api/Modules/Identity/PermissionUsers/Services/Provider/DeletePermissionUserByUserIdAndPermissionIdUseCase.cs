using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Identity.PermissionUsers.DTOs.Commands;
using Kairos.Api.Modules.Identity.PermissionUsers.Services.Interface;
using Kairos.Api.Modules.Identity.Users.Models;
using Kairos.Api.Modules.Identity.Users.Services.Interface;

namespace Kairos.Api.Modules.Identity.PermissionUsers.Services.Provider;

public sealed class DeletePermissionUserByUserIdAndPermissionIdUseCase(
    IUnitOfWork uow,
    IFindUserByIdUseCase findUserByIdUseCase)
    : IDeletePermissionUserByUserIdAndPermissionIdUseCase
{
    public async Task<Result<object>> ExecuteAsync(
        DeletePermissionUserCommand command,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (command is null)
        {
            return Result<object>.ValidationFailure(
                ["Command is required."]);
        }

        if (string.IsNullOrWhiteSpace(command.UserId))
        {
            return Result<object>.ValidationFailure(
                ["UserId is required."]);
        }

        if (string.IsNullOrWhiteSpace(command.PermissionId))
        {
            return Result<object>.ValidationFailure(
                ["PermissionId is required."]);
        }

        var normalizedUserId = command.UserId.Trim();

        if (!Guid.TryParse(
                command.PermissionId.Trim(),
                out var permissionGuid))
        {
            return Result<object>.ValidationFailure(
                ["PermissionId must be a valid UUID."]);
        }

        var normalizedPermissionId = permissionGuid.ToString();

        var permission = await uow.PermissionRepository.GetByIdAsync(
            normalizedPermissionId,
            cancellationToken);

        if (permission is null)
        {
            return Result<object>.NotFound(
                "Permission not found.");
        }

        if (string.IsNullOrWhiteSpace(permission.Code))
        {
            return Result<object>.Failure(
                "The permission has an invalid code.",
                500);
        }

        var userResult = await findUserByIdUseCase.ExecuteAsync(
            normalizedUserId,
            cancellationToken);

        if (userResult.IsFailure)
        {
            return Result<object>.Failure(
                userResult.Errors,
                userResult.StatusCode);
        }

        UserModel? user = userResult.Value;

        if (user is null)
        {
            return Result<object>.NotFound(
                "User not found.");
        }

        var association =
            await uow.PermissionUserRepository
                .GetByUserIdAndPermissionIdAsync(
                    user.Id,
                    permission.Id,
                    cancellationToken);

        if (association is null)
        {
            return Result<object>.NotFound(
                "Permission-user association not found.");
        }

        await uow.PermissionUserRepository
            .DeleteByUserIdAndPermissionIdAsync(
                user.Id,
                permission.Id,
                cancellationToken);

        user.RemovePermission(permission.Code.Trim());

        return Result<object>.Ok();
    }
}