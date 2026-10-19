using Kairos.Api.Common.Base.Model;

namespace Kairos.Api.Modules.Categories.Models;

public class CategoryModel : BaseModel
{
    public required string Name { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}