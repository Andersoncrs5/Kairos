using System.ComponentModel.DataAnnotations;
using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Rooms.RoomTypes.DTOs.Commands;
using Kairos.Api.Modules.Rooms.RoomTypes.DTOs.Request;
using Kairos.Api.Modules.Rooms.RoomTypes.Mapper;
using Kairos.Api.Modules.Rooms.RoomTypes.Models;
using Kairos.Api.Modules.Rooms.RoomTypes.Services.Interface;

namespace Kairos.Api.Modules.Rooms.RoomTypes.Services.Provider;

public sealed class UpdateRoomTypeUseCase(IUnitOfWork uow)
    : IUpdateRoomTypeUseCase
{
    public async Task<Result<RoomTypeModel>> ExecuteAsync(
        UpdateRoomTypeCommand command,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (command is null)
        {
            return Result<RoomTypeModel>.Failure(
                ["Room type update command is required."],
                400);
        }

        List<ValidationResult> validationResults = [];

        if (!Validator.TryValidateObject(
                command,
                new ValidationContext(command),
                validationResults,
                validateAllProperties: true))
        {
            return Result<RoomTypeModel>.Failure(
                validationResults
                    .Select(result =>
                        result.ErrorMessage ?? "Invalid update command.")
                    .ToArray(),
                400);
        }

        UpdateRoomTypeDto dto = command.Dto;

        validationResults = [];

        if (!Validator.TryValidateObject(
                dto,
                new ValidationContext(dto),
                validationResults,
                validateAllProperties: true))
        {
            return Result<RoomTypeModel>.Failure(
                validationResults
                    .Select(result =>
                        result.ErrorMessage ?? "Invalid room type data.")
                    .ToArray(),
                400);
        }

        if (dto.Name is not null && string.IsNullOrWhiteSpace(dto.Name))
            return Result<RoomTypeModel>.Failure(["Room type name cannot be empty."], 400);

        if (!Guid.TryParse(command.RoomTypeId, out var roomTypeId))
            return Result<RoomTypeModel>.Failure(["Id must be a valid UUID."], 400);

        RoomTypeModel? roomType = await uow.RoomTypeRepository.GetByIdAsync(roomTypeId.ToString(), ct);

        if (roomType is null)
            return Result<RoomTypeModel>.NotFound("Room type not found.");

        if (dto.Name is not null)
        {
            string name = dto.Name.Trim();

            if (!string.Equals(name, roomType.Name, StringComparison.OrdinalIgnoreCase))
            {
                bool exists = await uow.RoomTypeRepository.ExistsByNameAsync(name, ct);

                if (exists)
                    return Result<RoomTypeModel>.Failure([$"Room type with the name '{name}' already exists."], 409);
            }
        }

        RoomTypeMapper.ApplyUpdate(dto, roomType);

        if (dto.Name is not null)
            roomType.Name = dto.Name.Trim();

        if (dto.Description is not null)
            roomType.Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim();

        uow.RoomTypeRepository.Update(roomType);

        return Result<RoomTypeModel>.Success(roomType);
    }
}