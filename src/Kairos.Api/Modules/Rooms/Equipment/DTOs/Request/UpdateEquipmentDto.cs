using System.ComponentModel.DataAnnotations;

namespace Kairos.Api.Modules.Rooms.Equipment.DTOs.Request;

public sealed class UpdateEquipmentDto
{
    [StringLength(
        50,
        ErrorMessage = "Equipment code cannot exceed 50 characters.")]
    public string? Code { get; init; }

    [StringLength(
        100,
        ErrorMessage = "Equipment name cannot exceed 100 characters.")]
    public string? Name { get; init; }

    [StringLength(
        255,
        ErrorMessage = "Equipment description cannot exceed 255 characters.")]
    public string? Description { get; init; }

    public bool? IsActive { get; init; }
}