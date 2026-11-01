using System.ComponentModel.DataAnnotations;
using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Rooms.RoomOperatingHour.DTOs.Commands;
using Kairos.Api.Modules.Rooms.RoomOperatingHour.Mapper;
using Kairos.Api.Modules.Rooms.RoomOperatingHour.Model;
using Kairos.Api.Modules.Rooms.RoomOperatingHour.Services.Interfaces;

namespace Kairos.Api.Modules.Rooms.RoomOperatingHour.Services.Providers;

public sealed class UpdateRoomOperatingHourUseCase(
    IUnitOfWork unitOfWork)
    : IUpdateRoomOperatingHourUseCase
{
    public async Task<Result<RoomOperatingHourModel>> ExecuteAsync(
        UpdateRoomOperatingHourCommand command,
        CancellationToken ct = default)
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

        if (string.IsNullOrWhiteSpace(command.RoomOperatingHourId))
        {
            return Result<RoomOperatingHourModel>.ValidationFailure(
                ["Room operating hour ID is required."]);
        }

        if (command.Dto is null)
        {
            return Result<RoomOperatingHourModel>.ValidationFailure(
                ["Room operating hour update data is required."]);
        }

        string id = command.RoomOperatingHourId.Trim();

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
                    .Select(x =>
                        x.ErrorMessage ?? "Invalid room operating hour data.")
                    .ToArray());
        }

        // ---------------------------------------------------------------
        // Find operating hour
        // ---------------------------------------------------------------

        RoomOperatingHourModel? model =
            await unitOfWork.RoomOperatingHourRepository
                .GetByIdAsync(id, ct);

        if (model is null)
            return Result<RoomOperatingHourModel>.NotFound("Room operating hour not found.");

        // ---------------------------------------------------------------
        // Effective values
        // ---------------------------------------------------------------

        DayOfWeek dayOfWeek =
            command.Dto.DayOfWeek ?? model.DayOfWeek;

        TimeOnly opensAt =
            command.Dto.OpensAt ?? model.OpensAt;

        TimeOnly closesAt =
            command.Dto.ClosesAt ?? model.ClosesAt;

        bool isClosed =
            command.Dto.IsClosed ?? model.IsClosed;

        bool isActive =
            command.Dto.IsActive ?? model.IsActive;

        // ---------------------------------------------------------------
        // Day of week
        // ---------------------------------------------------------------

        if (!Enum.IsDefined(typeof(DayOfWeek), dayOfWeek))
        {
            return Result<RoomOperatingHourModel>.ValidationFailure(
                ["Invalid day of week."]);
        }

        // ---------------------------------------------------------------
        // Opening and closing time
        // ---------------------------------------------------------------

        if (!isClosed && opensAt >= closesAt)
        {
            return Result<RoomOperatingHourModel>.ValidationFailure(
                ["Opening time must be earlier than closing time."]);
        }

        // ---------------------------------------------------------------
        // Existing operating hours
        // ---------------------------------------------------------------

        IReadOnlyList<RoomOperatingHourModel> existingHours =
            await unitOfWork.RoomOperatingHourRepository
                .GetByRoomIdAndDayAsync(
                    model.RoomId,
                    dayOfWeek,
                    ct);

        var otherActiveHours = existingHours
            .Where(x =>
                x.Id != model.Id &&
                x.IsActive)
            .ToList();

        // ---------------------------------------------------------------
        // Closed day validation
        // ---------------------------------------------------------------

        if (isActive && isClosed && otherActiveHours.Count > 0)
        {
            return Result<RoomOperatingHourModel>.Conflict(
                "Room already has operating hours configured for this day.");
        }

        if (isActive && !isClosed &&
            otherActiveHours.Any(x => x.IsClosed))
        {
            return Result<RoomOperatingHourModel>.Conflict(
                "Room is marked as closed for this day.");
        }

        // ---------------------------------------------------------------
        // Overlapping periods
        // ---------------------------------------------------------------

        if (isActive && !isClosed)
        {
            bool hasOverlap = otherActiveHours.Any(existing =>
                !existing.IsClosed &&
                opensAt < existing.ClosesAt &&
                closesAt > existing.OpensAt);

            if (hasOverlap)
            {
                return Result<RoomOperatingHourModel>.Conflict(
                    "Room operating hours overlap with an existing period.");
            }
        }

        // ---------------------------------------------------------------
        // Mapping
        // ---------------------------------------------------------------

        RoomOperatingHourMapper.ApplyUpdate(command.Dto, model);

        // Explicitly apply nullable properties using effective values.
        model.DayOfWeek = dayOfWeek;
        model.OpensAt = opensAt;
        model.ClosesAt = closesAt;
        model.IsClosed = isClosed;
        model.IsActive = isActive;

        model.UpdatedAt = DateTimeOffset.UtcNow;
        model.Version++;

        // ---------------------------------------------------------------
        // Persistence
        // ---------------------------------------------------------------

        unitOfWork.RoomOperatingHourRepository.Update(model);

        return Result<RoomOperatingHourModel>.Success(model);
    }
}