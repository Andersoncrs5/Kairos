using System.ComponentModel.DataAnnotations;
using Kairos.Api.Modules.Categories.DTOs.Request;

namespace Kairos.Api.Modules.Categories.DTOs.Commands;

public sealed record UpdateCategoryCommand(
    [property: Required(
        ErrorMessage = "Category ID is required.")]
    string CategoryId,

    [property: Required(
        ErrorMessage = "Category update data is required.")]
    UpdateCategoryDto Dto
);