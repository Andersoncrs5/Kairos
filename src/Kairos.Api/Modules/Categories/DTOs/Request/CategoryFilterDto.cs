using System.ComponentModel.DataAnnotations;
using Kairos.Api.Common.Base.Model;

namespace Kairos.Api.Modules.Categories.DTOs.Request;

public sealed class CategoryFilterDto : BaseFilter
{
    [StringLength(150)]
    public string? Code { get; init; }

    public bool? CodeIgnoreCase { get; init; }

    [StringLength(100)]
    public string? Name { get; init; }

    public bool? NameIgnoreCase { get; init; }

    [StringLength(255)]
    public string? Description { get; init; }

    public bool? DescriptionIgnoreCase { get; init; }

    [RegularExpression("^#[0-9a-fA-F]{6}$")]
    public string? Color { get; init; }

    [StringLength(50)]
    public string? Icon { get; init; }

    public bool? IconIgnoreCase { get; init; }

    [Range(0, int.MaxValue)]
    public int? DisplayOrder { get; init; }

    public bool? IsActive { get; init; }
}