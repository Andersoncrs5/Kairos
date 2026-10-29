using System.ComponentModel.DataAnnotations;
using Kairos.Api.Common.Base.Model;

namespace Kairos.Api.Modules.Categories.DTOs.Request;

public sealed class CategoryFilterDto : BaseFilter
{
    [StringLength(100)]
    public string? Name { get; init; }

    public bool? NameIgnoreCase { get; init; }

    [StringLength(255)]
    public string? Description { get; init; }

    public bool? DescriptionIgnoreCase { get; init; }

    public bool? IsActive { get; init; }
}