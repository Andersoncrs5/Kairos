using Kairos.Api.Common.Base.Model;

namespace Kairos.Api.Modules.Categories.DTOs.Response;

public sealed class CategoryDto : BaseDTO
{
    public required string Code { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    public string? Color { get; init; }

    public string? Icon { get; init; }

    public int DisplayOrder { get; init; }

    public bool IsActive { get; init; }
}