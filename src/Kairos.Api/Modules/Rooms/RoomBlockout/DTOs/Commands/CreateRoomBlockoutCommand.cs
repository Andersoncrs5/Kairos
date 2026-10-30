using System.ComponentModel.DataAnnotations;
using Kairos.Api.Modules.Rooms.RoomBlockout.DTOs.Request;

namespace Kairos.Api.Modules.Rooms.RoomBlockout.DTOs.Commands;

public sealed class CreateRoomBlockoutCommand
{
    [Required(ErrorMessage = "Room blockout data is required.")]
    public required CreateRoomBlockoutDto Dto { get; init; }

    public string? CreatedBy { get; init; }
}