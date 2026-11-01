using Kairos.Api.Common.Base.Model;

namespace Kairos.Api.Modules.Categories.Models;

public class CategoryModel : BaseModel
{
    public required string Code { get; set; }

    public required string Name { get; set; }

    public string? Description { get; set; }

    public string? Color { get; set; }

    public string? Icon { get; set; }

    public int DisplayOrder { get; set; } = 0;

    public bool IsActive { get; set; } = true;
}