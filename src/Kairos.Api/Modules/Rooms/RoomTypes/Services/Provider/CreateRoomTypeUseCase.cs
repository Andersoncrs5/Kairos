using System.ComponentModel.DataAnnotations;
using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Rooms.RoomTypes.DTOs.Request;
using Kairos.Api.Modules.Rooms.RoomTypes.Mapper;
using Kairos.Api.Modules.Rooms.RoomTypes.Models;
using Kairos.Api.Modules.Rooms.RoomTypes.Services.Interface;

namespace Kairos.Api.Modules.Rooms.RoomTypes.Services.Provider;

public sealed class CreateRoomTypeUseCase(IUnitOfWork uow): ICreateRoomTypeUseCase
{
    public async Task<Result<RoomTypeModel>> ExecuteAsync(CreateRoomTypeDto dto, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (dto is null)
        {
            return Result<RoomTypeModel>.Failure(
                ["Room type data is required."],
                400);
        }

        List<ValidationResult> validationResults = [];

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

        string name = dto.Name.Trim();

        bool exists = await uow.RoomTypeRepository.ExistsByNameAsync(name, ct);

        if (exists)
        {
            return Result<RoomTypeModel>.Failure(
                [$"Room type with the name '{name}' already exists."],
                409);
        }

        DateTimeOffset createdAt = DateTimeOffset.UtcNow;

        RoomTypeModel roomType = RoomTypeMapper.ToModel(dto, Guid.NewGuid().ToString(), createdAt);

        roomType.Name = name;
        roomType.Version = 1;
        roomType.IsActive = true;

        await uow.RoomTypeRepository.AddAsync(roomType);

        return Result<RoomTypeModel>.Success(roomType);
    }
}