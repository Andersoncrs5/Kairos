using System.ComponentModel.DataAnnotations;
using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Rooms.Room.DTOs.Commands;
using Kairos.Api.Modules.Rooms.Room.DTOs.Request;
using Kairos.Api.Modules.Rooms.Room.Mapper;
using Kairos.Api.Modules.Rooms.Room.Models;
using Kairos.Api.Modules.Rooms.Room.Services.Interfaces;
using Kairos.Api.Modules.Rooms.RoomTypes.Models;

namespace Kairos.Api.Modules.Rooms.Room.Services.Providers;

public sealed class UpdateRoomUseCase(IUnitOfWork uow)
    : IUpdateRoomUseCase
{
    public async Task<Result<RoomModel>> ExecuteAsync(
        UpdateRoomCommand command,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (command is null)
        {
            return Result<RoomModel>.Failure(
                ["Room update command is required."],
                400);
        }

        // 1) Validação do command (RoomId e Dto obrigatórios)
        List<ValidationResult> validationResults = [];

        if (!Validator.TryValidateObject(
                command,
                new ValidationContext(command),
                validationResults,
                validateAllProperties: true))
        {
            return Result<RoomModel>.Failure(
                validationResults
                    .Select(result => result.ErrorMessage ?? "Invalid update command.")
                    .ToArray(),
                400);
        }

        // 2) Validação do DTO (tamanhos e faixas)
        UpdateRoomDto dto = command.Dto;

        validationResults = [];

        if (!Validator.TryValidateObject(
                dto,
                new ValidationContext(dto),
                validationResults,
                validateAllProperties: true))
        {
            return Result<RoomModel>.Failure(
                validationResults
                    .Select(result => result.ErrorMessage ?? "Invalid room data.")
                    .ToArray(),
                400);
        }

        // 3) Campos enviados não podem ser vazios (null = "não alterar")
        List<string> errors = [];

        if (dto.RoomTypeId is not null && string.IsNullOrWhiteSpace(dto.RoomTypeId))
            errors.Add("Room type ID cannot be empty.");

        if (dto.Code is not null && string.IsNullOrWhiteSpace(dto.Code))
            errors.Add("Room code cannot be empty.");

        if (dto.Name is not null && string.IsNullOrWhiteSpace(dto.Name))
            errors.Add("Room name cannot be empty.");

        if (dto.Location is not null && string.IsNullOrWhiteSpace(dto.Location))
            errors.Add("Room location cannot be empty.");

        if (errors.Count > 0)
            return Result<RoomModel>.Failure(errors, 400);

        // 4) IDs precisam ser UUIDs válidos
        if (!Guid.TryParse(command.RoomId, out Guid roomId))
            return Result<RoomModel>.Failure(["Id must be a valid UUID."], 400);

        Guid? newRoomTypeId = null;

        if (dto.RoomTypeId is not null)
        {
            if (!Guid.TryParse(dto.RoomTypeId.Trim(), out Guid parsedRoomTypeId))
            {
                return Result<RoomModel>.Failure(
                    ["Room type ID must be a valid UUID."],
                    400);
            }

            newRoomTypeId = parsedRoomTypeId;
        }

        // 5) A sala precisa existir
        RoomModel? room = await uow.RoomRepository.GetByIdAsync(roomId.ToString(), ct);

        if (room is null)
            return Result<RoomModel>.NotFound("Room not found.");

        // 6) Troca de tipo: o novo tipo precisa existir
        if (newRoomTypeId is not null &&
            !string.Equals(
                newRoomTypeId.Value.ToString(),
                room.RoomTypeId,
                StringComparison.OrdinalIgnoreCase))
        {
            RoomTypeModel? roomType = await uow.RoomTypeRepository
                .GetByIdAsync(newRoomTypeId.Value.ToString(), ct);

            if (roomType is null)
                return Result<RoomModel>.NotFound("Room type not found.");
        }

        // 7) Troca de código: não pode colidir com outra sala
        if (dto.Code is not null)
        {
            string code = dto.Code.Trim();

            if (!string.Equals(code, room.Code, StringComparison.OrdinalIgnoreCase))
            {
                bool exists = await uow.RoomRepository.ExistsByCodeAsync(code, ct);

                if (exists)
                {
                    return Result<RoomModel>.Failure(
                        [$"Room with the code '{code}' already exists."],
                        409);
                }
            }
        }

        // 8) Aplica só o que foi enviado (o Mapperly ignora nulls)...
        RoomMapper.ApplyUpdate(dto, room);

        // ...e normaliza o que o mapper copia sem tratar
        if (newRoomTypeId is not null)
            room.RoomTypeId = newRoomTypeId.Value.ToString();

        if (dto.Code is not null)
            room.Code = dto.Code.Trim();

        if (dto.Name is not null)
            room.Name = dto.Name.Trim();

        if (dto.Location is not null)
            room.Location = dto.Location.Trim();

        // Texto em branco limpa o campo opcional
        if (dto.Description is not null)
            room.Description = NullIfBlank(dto.Description);

        if (dto.Floor is not null)
            room.Floor = NullIfBlank(dto.Floor);

        uow.RoomRepository.Update(room);

        return Result<RoomModel>.Success(room);
    }

    private static string? NullIfBlank(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}