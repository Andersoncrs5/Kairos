using System.ComponentModel.DataAnnotations;
using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Rooms.Equipment.DTOs.Commands;
using Kairos.Api.Modules.Rooms.Equipment.Mapper;
using Kairos.Api.Modules.Rooms.Equipment.Model;
using Kairos.Api.Modules.Rooms.Equipment.Services.Interfaces;

namespace Kairos.Api.Modules.Rooms.Equipment.Services.Providers;

public sealed class CreateEquipmentUseCase(IUnitOfWork uow) : ICreateEquipmentUseCase
{
    public async Task<Result<EquipmentModel>> ExecuteAsync(
        CreateEquipmentCommand command,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (command is null)
        {
            return Result<EquipmentModel>.Failure(
                ["Equipment command is required."],
                400);
        }

        if (command.Dto is null)
        {
            return Result<EquipmentModel>.Failure(
                ["Equipment data is required."],
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
                        result.ErrorMessage ?? "Invalid equipment data.")
                    .ToArray(),
                400);
        }

        string code = command.Dto.Code.Trim();
        string name = command.Dto.Name.Trim();

        bool codeExists = await uow.EquipmentRepository.ExistsByCodeAsync(code, ct);
        if (codeExists)
        {
            return Result<EquipmentModel>.Failure(
                [$"Equipment with the code '{code}' already exists."],
                409);
        }

        bool nameExists = await uow.EquipmentRepository.ExistsByNameAsync(name, ct);
        if (nameExists)
        {
            return Result<EquipmentModel>.Failure(
                [$"Equipment with the name '{name}' already exists."],
                409);
        }

        DateTimeOffset createdAt = DateTimeOffset.UtcNow;

        EquipmentModel equipment = EquipmentMapper.ToModel(
            command.Dto,
            Guid.NewGuid().ToString(),
            createdAt);

        equipment.Code = code;
        equipment.Name = name;
        equipment.Version = 1;
        equipment.IsActive = true;

        await uow.EquipmentRepository.AddAsync(equipment);

        return Result<EquipmentModel>.Success(equipment);
    }
}