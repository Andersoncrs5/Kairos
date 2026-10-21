using System.ComponentModel.DataAnnotations;

namespace Kairos.Api.Modules.Categories.DTOs.Request;

public sealed class UpdateCategoryDto
{
    [StringLength(100, MinimumLength = 1)]
    public string? Name { get; init; }

    [StringLength(255)]
    public string? Description { get; init; }

    public bool? IsActive { get; init; }
}