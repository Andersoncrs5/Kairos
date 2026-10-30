using System.ComponentModel.DataAnnotations;
using Kairos.Api.Common.Enums;
using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Rooms.RoomBlockout.DTOs.Commands;
using Kairos.Api.Modules.Rooms.RoomBlockout.Models;
using Kairos.Api.Modules.Rooms.RoomBlockout.Services.Interfaces;

namespace Kairos.Api.Modules.Rooms.RoomBlockout.Services.Providers;

public sealed class CancelRoomBlockoutUseCase(IUnitOfWork uow)
    : ICancelRoomBlockoutUseCase
{
    public async Task<Result<RoomBlockoutModel>> ExecuteAsync(CancelRoomBlockoutCommand command, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (command is null)
            return Result<RoomBlockoutModel>.ValidationFailure(["Room blockout cancellation command is required."]);

        List<ValidationResult> validationResults = [];

        if (!Validator.TryValidateObject(command, new ValidationContext(command), validationResults, validateAllProperties: true))
        {
            return Result<RoomBlockoutModel>.ValidationFailure(
                validationResults
                    .Select(result =>
                        result.ErrorMessage ??
                        "Invalid room blockout cancellation command.")
                    .Distinct()
                    .ToArray());
        }

        if (!Guid.TryParse(command.RoomBlockoutId, out Guid blockoutGuid))
            return Result<RoomBlockoutModel>.ValidationFailure(["Room blockout ID must be a valid UUID."]);
        
        string blockoutId = blockoutGuid.ToString();

        string cancellationReason = command.CancellationReason.Trim();

        if (string.IsNullOrWhiteSpace(cancellationReason))
            return Result<RoomBlockoutModel>.ValidationFailure(["Room blockout cancellation reason is required."]);

        if (!Guid.TryParse(
                command.CancelledBy,
                out Guid cancelledByGuid))
        {
            return Result<RoomBlockoutModel>.ValidationFailure(
                ["Cancelled by must be a valid UUID."]);
        }

        string cancelledBy = cancelledByGuid.ToString();

        // 5. Busca o bloqueio.
        RoomBlockoutModel? blockout =
            await uow.RoomBlockoutRepository.GetByIdAsync(
                blockoutId,
                ct);

        if (blockout is null)
        {
            return Result<RoomBlockoutModel>.NotFound(
                "Room blockout not found.");
        }

        // 6. Verifica se já foi cancelado.
        if (blockout.Status == RoomBlockoutStatus.Cancelled)
        {
            return Result<RoomBlockoutModel>.Conflict(
                "Room blockout is already cancelled.");
        }

        // 7. Aplica o cancelamento.
        blockout.Status = RoomBlockoutStatus.Cancelled;
        blockout.CancelledAt = DateTimeOffset.UtcNow;
        blockout.CancellationReason = cancellationReason;
        blockout.CancelledBy = cancelledBy;

        // 8. Persiste a alteração.
        uow.RoomBlockoutRepository.Update(blockout);

        return Result<RoomBlockoutModel>.Success(blockout);
    }
}