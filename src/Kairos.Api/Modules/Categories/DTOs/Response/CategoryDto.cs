using Kairos.Api.Common.Base.Model;

namespace Kairos.Api.Modules.Categories.DTOs.Response;

public sealed class CategoryDto: BaseDTO
{
    public required string Name { get; init; }

    public string? Description { get; init; }

    public bool IsActive { get; init; }
}