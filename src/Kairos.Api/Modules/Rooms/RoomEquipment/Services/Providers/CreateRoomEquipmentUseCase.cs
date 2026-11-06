using System.ComponentModel.DataAnnotations;
using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Rooms.RoomEquipment.DTOs.Commands;
using Kairos.Api.Modules.Rooms.RoomEquipment.Mapper;
using Kairos.Api.Modules.Rooms.RoomEquipment.Models;
using Kairos.Api.Modules.Rooms.RoomEquipment.Services.Interfaces;

namespace Kairos.Api.Modules.Rooms.RoomEquipment.Services.Providers;

public sealed class CreateRoomEquipmentUseCase(IUnitOfWork uow) : ICreateRoomEquipmentUseCase
{
    public async Task<Result<RoomEquipmentModel>> ExecuteAsync(AddEquipmentToRoomCommand command, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (command is null)
        {
            return Result<RoomEquipmentModel>.Failure(
                ["Room equipment command is required."],
                400);
        }

        if (string.IsNullOrWhiteSpace(command.RoomId))
        {
            return Result<RoomEquipmentModel>.Failure(
                ["Room ID is required."],
                400);
        }

        if (!Guid.TryParse(command.RoomId, out _))
        {
            return Result<RoomEquipmentModel>.Failure(
                ["Room ID must be a valid UUID."],
                400);
        }

        if (command.Dto is null)
        {
            return Result<RoomEquipmentModel>.Failure(
                ["Room equipment data is required."],
                400);
        }

        List<ValidationResult> validationResults = [];

        if (!Validator.TryValidateObject(
                command.Dto,
                new ValidationContext(command.Dto),
                validationResults,
                validateAllProperties: true))
        {
            return Result<RoomEquipmentModel>.Failure(
                validationResults
                    .Select(result =>
                        result.ErrorMessage ?? "Invalid room equipment data.")
                    .ToArray(),
                400);
        }

        string roomId = command.RoomId.Trim();
        string equipmentId = command.Dto.EquipmentId.Trim();

        bool roomExists = await uow.RoomRepository.ExistsAsync(roomId, ct);
        if (!roomExists)
            return Result<RoomEquipmentModel>.Failure(["Room doesn't exist."], 404);

        bool equipmentExists = await uow.EquipmentRepository.ExistsAsync(equipmentId, ct);
        if (!equipmentExists)
        {
            return Result<RoomEquipmentModel>.Failure(
                ["Equipment doesn't exist."],
                404);
        }

        bool associationExists = await uow.RoomEquipmentRepository
            .ExistsByRoomAndEquipmentAsync(roomId, equipmentId, ct);

        if (associationExists)
            return Result<RoomEquipmentModel>.Failure(["Equipment is already associated with this room."], 409);
        

        DateTimeOffset createdAt = DateTimeOffset.UtcNow;

        RoomEquipmentModel roomEquipment = RoomEquipmentMapper.ToModel(command.Dto, Guid.NewGuid().ToString(), createdAt);

        roomEquipment.RoomId = roomId;
        roomEquipment.EquipmentId = equipmentId;
        roomEquipment.Version = 1;

        await uow.RoomEquipmentRepository.AddAsync(roomEquipment);

        return Result<RoomEquipmentModel>.Success(roomEquipment);
    }
}