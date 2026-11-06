using System.ComponentModel.DataAnnotations;
using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Rooms.Equipment.DTOs.Commands;
using Kairos.Api.Modules.Rooms.Equipment.Mapper;
using Kairos.Api.Modules.Rooms.Equipment.Model;
using Kairos.Api.Modules.Rooms.Equipment.Services.Interfaces;

namespace Kairos.Api.Modules.Rooms.Equipment.Services.Providers;

public sealed class UpdateEquipmentUseCase(IUnitOfWork uow) : IUpdateEquipmentUseCase
{
    public async Task<Result<EquipmentModel>> ExecuteAsync(
        UpdateEquipmentCommand command,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (command is null)
        {
            return Result<EquipmentModel>.Failure(
                ["Equipment update command is required."],
                400);
        }

        if (string.IsNullOrWhiteSpace(command.EquipmentId))
        {
            return Result<EquipmentModel>.Failure(
                ["Equipment ID is required."],
                400);
        }

        if (!Guid.TryParse(command.EquipmentId, out _))
        {
            return Result<EquipmentModel>.Failure(
                ["Id must be a valid UUID."],
                400);
        }

        if (command.Dto is null)
        {
            return Result<EquipmentModel>.Failure(
                ["Equipment update data is required."],
                400);
        }

        List<ValidationResult> validationResults = [];

        if (!Validator.TryValidateObject(
                command.Dto,
                new ValidationContext(command.Dto),
                validationResults,
                validateAllProperties: true))
        {
            return Result<EquipmentModel>.Failure(
                validationResults
                    .Select(result =>
                        result.ErrorMessage ?? "Invalid equipment update data.")
                    .ToArray(),
                400);
        }

        EquipmentModel? equipment = await uow.EquipmentRepository.FindByIdWithForUpdateAsync(command.EquipmentId, null, ct);
        if (equipment is null)
        {
            return Result<EquipmentModel>.NotFound("Equipment doesn't exist.");
        }

        if (!string.IsNullOrWhiteSpace(command.Dto.Code))
        {
            string newCode = command.Dto.Code.Trim();
            if (equipment.Code != newCode)
            {
                bool codeExists = await uow.EquipmentRepository.ExistsByCodeAsync(newCode, ct);
                if (codeExists)
                {
                    return Result<EquipmentModel>.Failure(
                        [$"Equipment with the code '{newCode}' already exists."],
                        409);
                }
                equipment.Code = newCode;
            }
        }

        if (!string.IsNullOrWhiteSpace(command.Dto.Name))
        {
            string newName = command.Dto.Name.Trim();
            if (equipment.Name != newName)
            {
                bool nameExists = await uow.EquipmentRepository.ExistsByNameAsync(newName, ct);
                if (nameExists)
                {
                    return Result<EquipmentModel>.Failure(
                        [$"Equipment with the name '{newName}' already exists."],
                        409);
                }
                equipment.Name = newName;
            }
        }

        EquipmentMapper.ApplyUpdate(command.Dto, equipment);

        equipment.Version++;
        equipment.UpdatedAt = DateTimeOffset.UtcNow;

        uow.EquipmentRepository.Update(equipment);

        return Result<EquipmentModel>.Success(equipment);
    }
}