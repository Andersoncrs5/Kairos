using System.ComponentModel.DataAnnotations;
using Kairos.Api.Common.Enums;
using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Rooms.Room.Models;
using Kairos.Api.Modules.Rooms.RoomBlockout.DTOs.Commands;
using Kairos.Api.Modules.Rooms.RoomBlockout.DTOs.Request;
using Kairos.Api.Modules.Rooms.RoomBlockout.Mapper;
using Kairos.Api.Modules.Rooms.RoomBlockout.Models;
using Kairos.Api.Modules.Rooms.RoomBlockout.Services.Interfaces;

namespace Kairos.Api.Modules.Rooms.RoomBlockout.Services.Providers;

public sealed class UpdateRoomBlockoutUseCase(IUnitOfWork uow)
    : IUpdateRoomBlockoutUseCase
{
    public async Task<Result<RoomBlockoutModel>> ExecuteAsync(
        UpdateRoomBlockoutCommand command,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (command is null)
            return Result<RoomBlockoutModel>.ValidationFailure(["Room blockout update command is required."]);

        List<ValidationResult> validationResults = [];

        if (!Validator.TryValidateObject(command, new ValidationContext(command), validationResults, validateAllProperties: true))
        {
            return Result<RoomBlockoutModel>.ValidationFailure(
                validationResults
                    .Select(result =>
                        result.ErrorMessage ??
                        "Invalid room blockout update command.")
                    .Distinct()
                    .ToArray());
        }

        UpdateRoomBlockoutDto dto = command.Dto;

        validationResults = [];

        if (!Validator.TryValidateObject(dto, new ValidationContext(dto), validationResults, validateAllProperties: true))
        {
            return Result<RoomBlockoutModel>.ValidationFailure(validationResults
                    .Select(result =>
                        result.ErrorMessage ??
                        "Invalid room blockout update data.")
                    .Distinct()
                    .ToArray());
        }

        if (!Guid.TryParse(command.RoomBlockoutId, out Guid blockoutGuid))
            return Result<RoomBlockoutModel>.ValidationFailure(["Room blockout ID must be a valid UUID."]);

        string blockoutId = blockoutGuid.ToString();

        // 4. Valida os campos enviados.
        List<string> errors = [];

        string? normalizedRoomId = null;

        if (dto.RoomId is not null)
        {
            string roomId = dto.RoomId.Trim();

            if (string.IsNullOrWhiteSpace(roomId))
            {
                errors.Add("Room ID cannot be empty.");
            }
            else if (!Guid.TryParse(roomId, out Guid roomGuid))
            {
                errors.Add("Room ID must be a valid UUID.");
            }
            else
            {
                normalizedRoomId = roomGuid.ToString();
            }
        }

        if (dto.Reason is not null && string.IsNullOrWhiteSpace(dto.Reason))
            errors.Add("Room blockout reason cannot be empty.");

        if (dto.Type.HasValue && !Enum.IsDefined(typeof(RoomBlockoutType), dto.Type.Value))
        {
            errors.Add("Room blockout type is invalid.");
        }

        if (dto.StartsAt.HasValue &&
            dto.StartsAt.Value == default)
        {
            errors.Add("Room blockout start date is invalid.");
        }

        if (dto.EndsAt.HasValue &&
            dto.EndsAt.Value == default)
        {
            errors.Add("Room blockout end date is invalid.");
        }

        if (errors.Count > 0)
        {
            return Result<RoomBlockoutModel>.ValidationFailure(errors.Distinct().ToArray());
        }

        RoomBlockoutModel? blockout = await uow.RoomBlockoutRepository.GetByIdAsync(blockoutId, ct);

        if (blockout is null)
            return Result<RoomBlockoutModel>.NotFound("Room blockout not found.");

        if (blockout.Status == RoomBlockoutStatus.Cancelled)
        {
            return Result<RoomBlockoutModel>.Conflict(
                "Cancelled room blockouts cannot be updated.");
        }

        string finalRoomId = normalizedRoomId ?? blockout.RoomId;

        DateTimeOffset finalStartsAt =
            dto.StartsAt ?? blockout.StartsAt;

        DateTimeOffset finalEndsAt =
            dto.EndsAt ?? blockout.EndsAt;

        if (finalStartsAt >= finalEndsAt)
        {
            return Result<RoomBlockoutModel>.ValidationFailure(
                ["Room blockout start date must be earlier than end date."]);
        }

        RoomModel? targetRoom = null;

        bool roomChanged = !string.Equals(
            finalRoomId,
            blockout.RoomId,
            StringComparison.OrdinalIgnoreCase);

        if (roomChanged)
        {
            targetRoom = await uow.RoomRepository.GetByIdAsync(
                finalRoomId,
                ct);

            if (targetRoom is null)
            {
                return Result<RoomBlockoutModel>.NotFound(
                    "Room doesn't exist.");
            }

            if (!targetRoom.IsActive)
            {
                return Result<RoomBlockoutModel>.BadRequest(
                    "Room is not active.");
            }
        }

        // 9. Verifica conflitos de horário, desconsiderando o próprio bloqueio.
        IReadOnlyList<RoomBlockoutModel> overlappingBlockouts =
            await uow.RoomBlockoutRepository.GetOverlappingAsync(
                finalRoomId,
                finalStartsAt,
                finalEndsAt,
                ct);

        bool hasOverlap = overlappingBlockouts.Any(existing =>
            !string.Equals(
                existing.Id,
                blockout.Id,
                StringComparison.OrdinalIgnoreCase)
            && existing.Status != RoomBlockoutStatus.Cancelled);

        if (hasOverlap)
        {
            return Result<RoomBlockoutModel>.Conflict(
                "Room blockout overlaps an existing blockout.");
        }

        // 10. Aplica somente os campos enviados.
        RoomBlockoutMapper.ApplyUpdate(dto, blockout);

        // Normaliza valores que precisam de tratamento específico.
        blockout.RoomId = finalRoomId;
        blockout.StartsAt = finalStartsAt;
        blockout.EndsAt = finalEndsAt;

        if (roomChanged)
        {
            blockout.Room = targetRoom;
        }

        if (dto.Type.HasValue)
        {
            blockout.Type = dto.Type.Value;
        }

        if (dto.Reason is not null)
        {
            blockout.Reason = dto.Reason.Trim();
        }

        // Texto em branco limpa o campo opcional.
        if (dto.Notes is not null)
        {
            blockout.Notes = string.IsNullOrWhiteSpace(dto.Notes)
                ? null
                : dto.Notes.Trim();
        }

        // 11. Persiste a atualização.
        uow.RoomBlockoutRepository.Update(blockout);

        return Result<RoomBlockoutModel>.Success(blockout);
    }
}