using System.ComponentModel.DataAnnotations;

namespace Kairos.Api.Modules.Categories.DTOs.Request;

public sealed class CreateCategoryDto
{
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public required string Name { get; init; }

    [StringLength(255)]
    public string? Description { get; init; }
}