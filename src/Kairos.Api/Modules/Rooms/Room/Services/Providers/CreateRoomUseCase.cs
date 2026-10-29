using System.ComponentModel.DataAnnotations;
using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Rooms.Room.DTOs.Request;
using Kairos.Api.Modules.Rooms.Room.Mapper;
using Kairos.Api.Modules.Rooms.Room.Models;
using Kairos.Api.Modules.Rooms.Room.Services.Interfaces;

namespace Kairos.Api.Modules.Rooms.Room.Services.Providers;

public sealed class CreateRoomUseCase(IUnitOfWork uow)
    : ICreateRoomUseCase
{
    public async Task<Result<RoomModel>> ExecuteAsync(
        CreateRoomDto dto,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        // -----------------------------------------------------------
        // Validação do DTO
        // -----------------------------------------------------------

        if (dto is null)
            return Result<RoomModel>.Failure(["Room data is required."], 400);

        var validationResults = new List<ValidationResult>();

        if (!Validator.TryValidateObject(dto, new ValidationContext(dto), validationResults, validateAllProperties: true))
            return Result<RoomModel>.Failure(validationResults.Select(result => result.ErrorMessage ?? "Invalid room data.").Distinct().ToArray(), 400);

        // -----------------------------------------------------------
        // Normalização dos dados
        // -----------------------------------------------------------

        var roomTypeId = dto.RoomTypeId.Trim();
        var code = dto.Code.Trim();
        var name = dto.Name.Trim();
        var location = dto.Location.Trim();

        if (string.IsNullOrWhiteSpace(roomTypeId))
            return Result<RoomModel>.Failure(["Room type ID is required."], 400);
        
        if (!Guid.TryParse(roomTypeId, out var parsedRoomTypeId))
            return Result<RoomModel>.Failure(["Room type ID must be a valid UUID."], 400);

        roomTypeId = parsedRoomTypeId.ToString();

        if (string.IsNullOrWhiteSpace(code))
            return Result<RoomModel>.Failure(["Room code is required."], 400);
        
        if (string.IsNullOrWhiteSpace(name))
            return Result<RoomModel>.Failure(["Room name is required."], 400);
        
        if (string.IsNullOrWhiteSpace(location))
            return Result<RoomModel>.Failure(["Room location is required."], 400);

        // -----------------------------------------------------------
        // Verificação do tipo de sala
        // -----------------------------------------------------------

        var roomType = await uow.RoomTypeRepository.GetByIdAsync(roomTypeId, ct);
        if (roomType == null) return Result<RoomModel>.NotFound("Room type doesn't exist.");
        
        if (roomType.IsActive == false) return Result<RoomModel>.BadRequest("Room type is not active.");

        // -----------------------------------------------------------
        // Unicidade do código
        // -----------------------------------------------------------

        bool codeExists = await uow.RoomRepository.ExistsByCodeAsync(code, ct);

        if (codeExists)
        {
            return Result<RoomModel>.Failure(
                [$"Room with the code '{code}' already exists."],
                409);
        }

        // -----------------------------------------------------------
        // Criação da entidade
        // -----------------------------------------------------------

        DateTimeOffset now = DateTimeOffset.UtcNow;

        RoomModel room = RoomMapper.ToModel(
            dto,
            Guid.NewGuid().ToString(),
            now);

        room.RoomTypeId = roomTypeId;
        room.Code = code;
        room.Name = name;
        room.Location = location;

        room.Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim();

        room.Floor = string.IsNullOrWhiteSpace(dto.Floor)? null : dto.Floor.Trim();

        room.Version = 1;
        room.CreatedAt = now;
        room.UpdatedAt = null;
        room.IsActive = true;

        // -----------------------------------------------------------
        // Persistência
        // -----------------------------------------------------------

        await uow.RoomRepository.AddAsync(room, ct);

        return Result<RoomModel>.Success(room);
    }
}