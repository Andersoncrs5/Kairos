using System.ComponentModel.DataAnnotations;
using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Identity.Permissions.DTOs.Commands;
using Kairos.Api.Modules.Identity.Permissions.DTOs.Request;
using Kairos.Api.Modules.Identity.Permissions.Mapper;
using Kairos.Api.Modules.Identity.Permissions.Models;
using Kairos.Api.Modules.Identity.Permissions.Services.Interface;

namespace Kairos.Api.Modules.Identity.Permissions.Services.Provider;

public sealed class UpdatePermissionUseCase(IUnitOfWork uow)
    : IUpdatePermissionUseCase
{
    public async Task<Result<PermissionsModel>> ExecuteAsync(
        UpdatePermissionCommand command,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        List<ValidationResult> validationResults = [];

        if (!Validator.TryValidateObject(
                command,
                new ValidationContext(command),
                validationResults,
                validateAllProperties: true))
        {
            return Result<PermissionsModel>.Failure(
                validationResults
                    .Select(result =>
                        result.ErrorMessage ?? "Invalid update command.")
                    .ToArray(),
                400);
        }

        UpdatePermissionDto dto = command.Dto;

        validationResults = [];

        if (!Validator.TryValidateObject(
                dto,
                new ValidationContext(dto),
                validationResults,
                validateAllProperties: true))
        {
            return Result<PermissionsModel>.Failure(
                validationResults
                    .Select(result =>
                        result.ErrorMessage ?? "Invalid permission data.")
                    .ToArray(),
                400);
        }

        if (!Guid.TryParse(command.PermissionId, out var permissionId))
        {
            return Result<PermissionsModel>.Failure(
                ["Id must be a valid UUID."],
                400);
        }

        PermissionsModel? permission =
            await uow.PermissionRepository.GetByIdAsync(
                permissionId.ToString(),
                ct);

        if (permission is null)
        {
            return Result<PermissionsModel>.NotFound(
                "Permission not found.");
        }

        if (permission.IsSystem)
        {
            return Result<PermissionsModel>.Forbidden(
                "System permissions cannot be modified.");
        }

        PermissionMapper.ApplyUpdate(dto, permission);

        uow.PermissionRepository.Update(permission);

        return Result<PermissionsModel>.Success(permission);
    }
}