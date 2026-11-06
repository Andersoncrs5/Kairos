using System.ComponentModel.DataAnnotations;

namespace Kairos.Api.Modules.Rooms.Equipment.DTOs.Request;

public sealed class CreateEquipmentDto
{
    [Required(ErrorMessage = "Equipment code is required.")]
    [StringLength(
        50,
        ErrorMessage = "Equipment code cannot exceed 50 characters.")]
    public required string Code { get; init; }

    [Required(ErrorMessage = "Equipment name is required.")]
    [StringLength(
        100,
        ErrorMessage = "Equipment name cannot exceed 100 characters.")]
    public required string Name { get; init; }

    [StringLength(
        255,
        ErrorMessage = "Equipment description cannot exceed 255 characters.")]
    public string? Description { get; init; }
}