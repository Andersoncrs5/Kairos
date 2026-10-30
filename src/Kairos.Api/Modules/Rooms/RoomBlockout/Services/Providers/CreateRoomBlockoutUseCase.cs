using System.ComponentModel.DataAnnotations;
using Kairos.Api.Common.Enums;
using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Rooms.RoomBlockout.DTOs.Commands;
using Kairos.Api.Modules.Rooms.RoomBlockout.DTOs.Request;
using Kairos.Api.Modules.Rooms.RoomBlockout.Mapper;
using Kairos.Api.Modules.Rooms.RoomBlockout.Models;
using Kairos.Api.Modules.Rooms.RoomBlockout.Services.Interfaces;

namespace Kairos.Api.Modules.Rooms.RoomBlockout.Services.Providers;

public sealed class CreateRoomBlockoutUseCase(IUnitOfWork uow) : ICreateRoomBlockoutUseCase
{
    public async Task<Result<RoomBlockoutModel>> ExecuteAsync(CreateRoomBlockoutCommand command, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (command is null)
            return Result<RoomBlockoutModel>.ValidationFailure(["Room blockout command is required."]);
        
        List<ValidationResult> validationResults = new List<ValidationResult>();

        if (!Validator.TryValidateObject(command, new ValidationContext(command), validationResults, validateAllProperties: true))
        {
            return Result<RoomBlockoutModel>.ValidationFailure(
                validationResults
                    .Select(result =>
                        result.ErrorMessage ?? "Invalid room blockout command.")
                    .Distinct()
                    .ToArray());
        }
        
        CreateRoomBlockoutDto dto = command.Dto;

        validationResults = [];

        if (!Validator.TryValidateObject(dto, new ValidationContext(dto), validationResults, validateAllProperties: true))
        {
            return Result<RoomBlockoutModel>.ValidationFailure(
                validationResults
                    .Select(result =>
                        result.ErrorMessage ?? "Invalid room blockout data.")
                    .Distinct()
                    .ToArray());
        }

        if (string.IsNullOrWhiteSpace(command.CreatedBy))
            return Result<RoomBlockoutModel>.ValidationFailure(["Room blockout creator ID is required."]);

        if (!Guid.TryParse(command.CreatedBy, out var createdById))
            return Result<RoomBlockoutModel>.ValidationFailure(["Created by must be a valid UUID."]);

        string roomId = dto.RoomId.Trim();
        string reason = dto.Reason.Trim();

        string? notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim();

        if (string.IsNullOrWhiteSpace(roomId))
            return Result<RoomBlockoutModel>.ValidationFailure(["Room ID is required."]);

        if (!Guid.TryParse(roomId, out var parsedRoomId))
        {
            return Result<RoomBlockoutModel>.ValidationFailure(
                ["Room ID must be a valid UUID."]);
        }

        roomId = parsedRoomId.ToString();

        if (string.IsNullOrWhiteSpace(reason))
        {
            return Result<RoomBlockoutModel>.ValidationFailure(
                ["Room blockout reason is required."]);
        }

        if (dto.StartsAt == default || dto.EndsAt == default)
        {
            return Result<RoomBlockoutModel>.ValidationFailure(
                ["Room blockout start and end dates are required."]);
        }

        if (dto.StartsAt >= dto.EndsAt)
        {
            return Result<RoomBlockoutModel>.ValidationFailure(
                ["Room blockout start date must be earlier than end date."]);
        }

        var room = await uow.RoomRepository.GetByIdAsync(roomId, ct);

        if (room is null)
            return Result<RoomBlockoutModel>.NotFound("Room doesn't exist.");

        if (!room.IsActive)
            return Result<RoomBlockoutModel>.BadRequest("Room is not active.");
        
        bool hasOverlap = await uow.RoomBlockoutRepository.ExistsOverlappingAsync(roomId, dto.StartsAt, dto.EndsAt, ct);

        if (hasOverlap)
            return Result<RoomBlockoutModel>.Conflict("Room blockout overlaps an existing blockout.");
        
        DateTimeOffset now = DateTimeOffset.UtcNow;

        var blockout = RoomBlockoutMapper.ToModel(
            dto,
            Guid.NewGuid().ToString(),
            now,
            createdById.ToString());

        blockout.RoomId = roomId;
        blockout.Reason = reason;
        blockout.Notes = notes;
        blockout.Status = RoomBlockoutStatus.Scheduled;
        blockout.Version = 1;
        blockout.CreatedAt = now;
        blockout.UpdatedAt = null;

        // -----------------------------------------------------------
        // Persistência
        // -----------------------------------------------------------

        await uow.RoomBlockoutRepository.AddAsync(
            blockout,
            ct);

        return Result<RoomBlockoutModel>.Success(blockout);
    }
}