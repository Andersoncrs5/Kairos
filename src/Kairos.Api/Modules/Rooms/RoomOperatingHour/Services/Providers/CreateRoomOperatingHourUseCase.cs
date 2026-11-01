using System.ComponentModel.DataAnnotations;
using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Rooms.RoomOperatingHour.DTOs.Commands;
using Kairos.Api.Modules.Rooms.RoomOperatingHour.Mapper;
using Kairos.Api.Modules.Rooms.RoomOperatingHour.Model;
using Kairos.Api.Modules.Rooms.RoomOperatingHour.Services.Interfaces;

namespace Kairos.Api.Modules.Rooms.RoomOperatingHour.Services.Providers;

public sealed class CreateRoomOperatingHourUseCase(
    IUnitOfWork unitOfWork)
    : ICreateRoomOperatingHourUseCase
{
    public async Task<Result<RoomOperatingHourModel>> ExecuteAsync(CreateRoomOperatingHourCommand command, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        // ---------------------------------------------------------------
        // Command validation
        // ---------------------------------------------------------------

        if (command is null)
        {
            return Result<RoomOperatingHourModel>.ValidationFailure(
                ["Room operating hour command is required."]);
        }

        if (string.IsNullOrWhiteSpace(command.RoomId))
        {
            return Result<RoomOperatingHourModel>.ValidationFailure(
                ["Room ID is required."]);
        }

        if (command.Dto is null)
        {
            return Result<RoomOperatingHourModel>.ValidationFailure(
                ["Room operating hour data is required."]);
        }

        string roomId = command.RoomId.Trim();

        // ---------------------------------------------------------------
        // DTO validation
        // ---------------------------------------------------------------

        var validationResults = new List<ValidationResult>();

        bool isValid = Validator.TryValidateObject(
            command.Dto,
            new ValidationContext(command.Dto),
            validationResults,
            validateAllProperties: true);

        if (!isValid)
        {
            return Result<RoomOperatingHourModel>.ValidationFailure(
                validationResults
                    .Select(x => x.ErrorMessage ?? "Invalid room operating hour data.")
                    .ToArray());
        }

        // ---------------------------------------------------------------
        // Day of week
        // ---------------------------------------------------------------

        if (!Enum.IsDefined(typeof(DayOfWeek), command.Dto.DayOfWeek))
        {
            return Result<RoomOperatingHourModel>.ValidationFailure(
                ["Invalid day of week."]);
        }

        // ---------------------------------------------------------------
        // Opening and closing time
        // ---------------------------------------------------------------

        if (!command.Dto.IsClosed &&
            command.Dto.OpensAt >= command.Dto.ClosesAt)
        {
            return Result<RoomOperatingHourModel>.ValidationFailure(
                ["Opening time must be earlier than closing time."]);
        }

        // ---------------------------------------------------------------
        // Room existence
        // ---------------------------------------------------------------

        bool roomExists = await unitOfWork.RoomRepository
            .ExistsAsync(roomId, ct);

        if (!roomExists)
        {
            return Result<RoomOperatingHourModel>.NotFound(
                "Room doesn't exist.");
        }

        // ---------------------------------------------------------------
        // Existing operating hours
        // ---------------------------------------------------------------

        IReadOnlyList<RoomOperatingHourModel> existingHours =
            await unitOfWork.RoomOperatingHourRepository
                .GetByRoomIdAndDayAsync(
                    roomId,
                    command.Dto.DayOfWeek,
                    ct);

        var activeHours = existingHours
            .Where(x => x.IsActive)
            .ToList();

        // ---------------------------------------------------------------
        // Closed day validation
        // ---------------------------------------------------------------

        if (command.Dto.IsClosed && activeHours.Count > 0)
        {
            return Result<RoomOperatingHourModel>.Conflict(
                "Room already has operating hours configured for this day.");
        }

        if (!command.Dto.IsClosed &&
            activeHours.Any(x => x.IsClosed))
        {
            return Result<RoomOperatingHourModel>.Conflict(
                "Room is marked as closed for this day.");
        }

        // ---------------------------------------------------------------
        // Overlapping periods
        // ---------------------------------------------------------------

        if (!command.Dto.IsClosed)
        {
            bool hasOverlap = activeHours.Any(existing =>
                !existing.IsClosed &&
                command.Dto.OpensAt < existing.ClosesAt &&
                command.Dto.ClosesAt > existing.OpensAt);

            if (hasOverlap)
            {
                return Result<RoomOperatingHourModel>.Conflict(
                    "Room operating hours overlap with an existing period.");
            }
        }

        // ---------------------------------------------------------------
        // Mapping
        // ---------------------------------------------------------------

        DateTimeOffset now = DateTimeOffset.UtcNow;

        RoomOperatingHourModel model = RoomOperatingHourMapper.ToModel(
            command.Dto,
            Guid.NewGuid().ToString(),
            roomId,
            now);

        model.Version = 1;

        // ---------------------------------------------------------------
        // Persistence
        // ---------------------------------------------------------------

        await unitOfWork.RoomOperatingHourRepository.AddAsync(model, ct);
        
        return Result<RoomOperatingHourModel>.Created(model);
    }
}