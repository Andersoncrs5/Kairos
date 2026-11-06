using System.ComponentModel.DataAnnotations;
using Kairos.Api.Modules.Rooms.Equipment.DTOs.Request;

namespace Kairos.Api.Modules.Rooms.Equipment.DTOs.Commands;

public sealed record CreateEquipmentCommand(
    [property: Required(ErrorMessage = "Equipment data is required.")]
    CreateEquipmentDto Dto,

    [property: Required(ErrorMessage = "Creator ID is required.")]
    string CreatedBy
);