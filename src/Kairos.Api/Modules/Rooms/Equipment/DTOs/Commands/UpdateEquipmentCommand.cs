using System.ComponentModel.DataAnnotations;
using Kairos.Api.Modules.Rooms.Equipment.DTOs.Request;

namespace Kairos.Api.Modules.Rooms.Equipment.DTOs.Commands;

public sealed record UpdateEquipmentCommand(
    [property: Required(ErrorMessage = "Equipment ID is required.")]
    string EquipmentId,

    [property: Required(ErrorMessage = "Equipment update data is required.")]
    UpdateEquipmentDto Dto
);