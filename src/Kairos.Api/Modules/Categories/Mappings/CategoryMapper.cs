using Kairos.Api.Modules.Categories.DTOs.Request;
using Kairos.Api.Modules.Categories.Models;
using Riok.Mapperly.Abstractions;

namespace Kairos.Api.Modules.Categories.Mappings;

[Mapper(
    RequiredMappingStrategy = RequiredMappingStrategy.Source,
    AllowNullPropertyAssignment = false)]
public static partial class CategoryMapper
{
    public static partial CategoryModel ToModel(
        CreateCategoryDto source,
        string id,
        DateTimeOffset createdAt);

    public static partial void ApplyUpdate(
        UpdateCategoryDto source,
        [MappingTarget] CategoryModel target);
}