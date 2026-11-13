using System.ComponentModel.DataAnnotations;
using Asp.Versioning;
using Kairos.Api.Common.Base.Controller;
using Kairos.Api.Common.Page;
using Kairos.Api.Common.Res;
using Kairos.Api.Common.Result;
using Kairos.Api.Config.Authentication;
using Kairos.Api.Config.DB.Transaction;
using Kairos.Api.Modules.Categories.DTOs.Commands;
using Kairos.Api.Modules.Categories.DTOs.Request;
using Kairos.Api.Modules.Categories.DTOs.Response;
using Kairos.Api.Modules.Categories.Mappings;
using Kairos.Api.Modules.Categories.Models;
using Kairos.Api.Modules.Categories.Services.Interface;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kairos.Api.Modules.Categories.Controllers;

[Route("api/v{version:apiVersion}/categories")]
[ApiVersion("1.0")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public sealed class CategoryController(
    IInboxTransactionUseCase<
        CreateCategoryDto,
        CategoryModel,
        ICreateCategoryUseCase> createCategoryUseCase,

    IInboxTransactionUseCase<
        string,
        object,
        IDeleteCategoryByIdUseCase> deleteCategoryUseCase,

    IExistsCategoryByNameUseCase existsCategoryByNameUseCase,

    IInboxTransactionUseCase<
        string,
        CategoryModel,
        IFindCategoryByIdUseCase> findCategoryByIdUseCase,

    IInboxTransactionUseCase<
        UpdateCategoryCommand,
        CategoryModel,
        IUpdateCategoryUseCase> updateCategoryUseCase,

    IFilterCategoriesUseCase filterCategoriesUseCase,

    IExistsCategoryByCodeUseCase existsCategoryByCodeUseCase
) : ApiControllerBase
{
    // ---------------------------------------------------------------
    // Filter
    // ---------------------------------------------------------------

    /// <summary>
    /// Retrieves a paginated list of categories.
    /// </summary>
    /// <remarks>
    /// Supports filtering by identifier, version, creation date,
    /// update date, name, description and active status.
    /// Supports pagination and sort direction.
    /// </remarks>
    [HttpGet]
    [RequirePermissions("categories.read")]
    [ProducesResponseType(
        typeof(ResponseHttp<Page<CategoryDto>>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        typeof(ResponseHttp<ValidationErrorResponse>),
        StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Filter(
        [FromQuery] CategoryFilterDto dto,
        CancellationToken ct)
    {
        Result<Page<CategoryModel>> result =
            await filterCategoriesUseCase.ExecuteAsync(dto, ct);

        return ToResponse(
            result,
            ToPageDto,
            "Categories retrieved successfully",
            "Error filtering categories",
            HttpContext.TraceIdentifier);
    }

    // ---------------------------------------------------------------
    // Update
    // ---------------------------------------------------------------

    /// <summary>
    /// Updates an existing category.
    /// </summary>
    /// <remarks>
    /// Category names must be unique.
    /// The operation is processed through the Inbox transaction service.
    /// </remarks>
    [HttpPut("{id}")]
    [RequirePermissions("categories.update")]
    [ProducesResponseType(
        typeof(ResponseHttp<CategoryDto>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        typeof(ResponseHttp<ValidationErrorResponse>),
        StatusCodes.Status400BadRequest)]
    [ProducesResponseType(
        typeof(ResponseHttp<CategoryDto>),
        StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        [FromRoute, Required] string id,
        [FromBody] UpdateCategoryDto dto,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        UpdateCategoryCommand command = new(id, dto);

        Result<CategoryModel> result =
            await updateCategoryUseCase.ExecuteAsync(
                inboxId,
                NewWorkerId(),
                command,
                ct);

        return ToResponse(
            result,
            CategoryMapper.ToDto,
            "Category updated successfully",
            "Error updating category",
            inboxId);
    }

    // ---------------------------------------------------------------
    // Find by ID
    // ---------------------------------------------------------------

    /// <summary>
    /// Retrieves a category by its identifier.
    /// </summary>
    /// <remarks>
    /// The operation is processed through the Inbox transaction service.
    /// </remarks>
    [HttpGet("{id}")]
    [RequirePermissions("categories.read")]
    [ProducesResponseType(
        typeof(ResponseHttp<CategoryDto>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        typeof(ResponseHttp<CategoryDto>),
        StatusCodes.Status404NotFound)]
    public async Task<IActionResult> FindById(
        [FromRoute, Required] string id,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        Result<CategoryModel> result =
            await findCategoryByIdUseCase.ExecuteAsync(
                inboxId,
                NewWorkerId(),
                id,
                ct);

        return ToResponse(
            result,
            CategoryMapper.ToDto,
            "Category found",
            "Error finding category",
            inboxId);
    }

    // ---------------------------------------------------------------
    // Exists by name
    // ---------------------------------------------------------------

    /// <summary>
    /// Checks whether a category exists by its name.
    /// </summary>
    [HttpGet("Exists/name/{name}")]
    [RequirePermissions("categories.read")]
    [ProducesResponseType(
        typeof(ResponseHttp<bool?>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        typeof(ResponseHttp<ValidationErrorResponse>),
        StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ExistsByName(
        [FromRoute, Required] string name,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        Result<bool> result =
            await existsCategoryByNameUseCase.ExecuteAsync(name, ct);

        return ToExistsResponse(
            result,
            "Category exists",
            "Category does not exist",
            "Error checking category name",
            inboxId);
    }

    // ---------------------------------------------------------------
    // Delete
    // ---------------------------------------------------------------

    /// <summary>
    /// Deletes a category by its identifier.
    /// </summary>
    /// <remarks>
    /// The operation is processed through the Inbox transaction service.
    /// </remarks>
    [HttpDelete("{id}")]
    [RequirePermissions("categories.delete")]
    [ProducesResponseType(
        typeof(ResponseHttp<object>),
        StatusCodes.Status200OK)]
    public async Task<IActionResult> Delete(
        [FromRoute, Required] string id,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        Result<object> result =
            await deleteCategoryUseCase.ExecuteAsync(
                inboxId,
                NewWorkerId(),
                id,
                ct);

        if (result.IsFailure)
        {
            return FailureResponse<object>(
                result.StatusCode,
                result.Errors,
                "Error deleting category",
                inboxId);
        }

        return SuccessResponse(
            result.Value,
            "Category deleted successfully",
            inboxId);
    }

    // ---------------------------------------------------------------
    // Create
    // ---------------------------------------------------------------

    /// <summary>
    /// Creates a new category.
    /// </summary>
    /// <remarks>
    /// Category names must be unique.
    /// The operation is processed through the Inbox transaction service.
    /// </remarks>
    [HttpPost]
    [RequirePermissions("categories.create")]
    [ProducesResponseType(
        typeof(ResponseHttp<CategoryDto>),
        StatusCodes.Status201Created)]
    [ProducesResponseType(
        typeof(ResponseHttp<ValidationErrorResponse>),
        StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateCategoryDto dto,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        Result<CategoryModel> result =
            await createCategoryUseCase.ExecuteAsync(
                inboxId,
                NewWorkerId(),
                dto,
                ct);

        return ToResponse(
            result,
            CategoryMapper.ToDto,
            "Category created successfully",
            "Error creating category",
            inboxId,
            StatusCodes.Status201Created);
    }

    // ---------------------------------------------------------------
    // Exists by code
    // ---------------------------------------------------------------

    /// <summary>
    /// Checks whether a category exists by its code.
    /// </summary>
    [HttpGet("Exists/code/{code}")]
    [RequirePermissions("categories.read")]
    [ProducesResponseType(
        typeof(ResponseHttp<bool?>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        typeof(ResponseHttp<ValidationErrorResponse>),
        StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ExistsByCode(
        [FromRoute, Required] string code,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        Result<bool> result =
            await existsCategoryByCodeUseCase.ExecuteAsync(code, ct);

        return ToExistsResponse(
            result,
            "Category exists",
            "Category does not exist",
            "Error checking category code",
            inboxId);
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private static Page<CategoryDto> ToPageDto(
        Page<CategoryModel> page)
    {
        return Page<CategoryDto>.Create(
            page.Data
                .Select(CategoryMapper.ToDto)
                .ToArray(),
            page.TotalCount,
            page.PageNumber,
            page.PageSize);
    }

    private ObjectResult ToExistsResponse(
        Result<bool> result,
        string existsMessage,
        string notExistsMessage,
        string errorMessage,
        string inboxId)
    {
        if (result.IsFailure)
        {
            return FailureResponse<bool?>(
                result.StatusCode,
                result.Errors,
                errorMessage,
                inboxId);
        }

        return SuccessResponse(
            (bool?)result.Value,
            result.Value ? existsMessage : notExistsMessage,
            inboxId);
    }
}
