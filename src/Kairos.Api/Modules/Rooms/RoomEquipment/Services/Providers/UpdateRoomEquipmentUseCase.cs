using System.ComponentModel.DataAnnotations;
using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Rooms.RoomEquipment.DTOs.Commands;
using Kairos.Api.Modules.Rooms.RoomEquipment.Mapper;
using Kairos.Api.Modules.Rooms.RoomEquipment.Models;
using Kairos.Api.Modules.Rooms.RoomEquipment.Services.Interfaces;

namespace Kairos.Api.Modules.Rooms.RoomEquipment.Services.Providers;

public sealed class UpdateRoomEquipmentUseCase(IUnitOfWork uow) : IUpdateRoomEquipmentUseCase
{
    public async Task<Result<RoomEquipmentModel>> ExecuteAsync(
        UpdateRoomEquipmentCommand command,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (command is null)
        {
            return Result<RoomEquipmentModel>.Failure(
                ["Room equipment update command is required."],
                400);
        }

        if (string.IsNullOrWhiteSpace(command.RoomEquipmentId))
        {
            return Result<RoomEquipmentModel>.Failure(
                ["Room equipment association ID is required."],
                400);
        }

        if (!Guid.TryParse(command.RoomEquipmentId, out _))
        {
            return Result<RoomEquipmentModel>.Failure(
                ["Room equipment association ID must be a valid UUID."],
                400);
        }

        if (command.Dto is null)
        {
            return Result<RoomEquipmentModel>.Failure(
                ["Room equipment update data is required."],
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
                        result.ErrorMessage ?? "Invalid room equipment update data.")
                    .ToArray(),
                400);
        }

        string id = command.RoomEquipmentId.Trim();

        RoomEquipmentModel? roomEquipment = await uow.RoomEquipmentRepository.FindByIdWithForUpdateAsync(id, null, ct);
        if (roomEquipment is null)
            return Result<RoomEquipmentModel>.Failure(["Room equipment association doesn't exist."], 404);

        RoomEquipmentMapper.ApplyUpdate(command.Dto, roomEquipment);

        uow.RoomEquipmentRepository.Update(roomEquipment);

        return Result<RoomEquipmentModel>.Success(roomEquipment);
    }
}