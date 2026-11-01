using System.ComponentModel.DataAnnotations;

namespace Kairos.Api.Modules.Categories.DTOs.Request;

public sealed class CreateCategoryDto
{
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public required string Code { get; init; }

    [Required]
    [StringLength(100, MinimumLength = 1)]
    public required string Name { get; init; }

    [StringLength(255)]
    public string? Description { get; init; }

    [RegularExpression(
        "^#[0-9a-fA-F]{6}$",
        ErrorMessage = "Color must be a valid hexadecimal color (e.g. #2563EB).")]
    public string? Color { get; init; }

    [StringLength(50)]
    public string? Icon { get; init; }

    [Range(0, int.MaxValue)]
    public int DisplayOrder { get; init; } = 0;
}