using System.ComponentModel.DataAnnotations;
using Asp.Versioning;
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

[ApiController]
[Route("api/v{version:apiVersion}/categories")]
[ApiVersion("1.0")]
[Produces("application/json")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public sealed class CategoryController(
    IInboxTransactionUseCase<CreateCategoryDto, CategoryModel, ICreateCategoryUseCase> createCategoryUseCase,
    IInboxTransactionUseCase<string, object, IDeleteCategoryByIdUseCase> deleteCategoryUseCase,
    IExistsCategoryByNameUseCase existsCategoryByNameUseCase,
    IInboxTransactionUseCase<string, CategoryModel, IFindCategoryByIdUseCase> findCategoryByIdUseCase,
    IInboxTransactionUseCase<UpdateCategoryCommand, CategoryModel, IUpdateCategoryUseCase> updateCategoryUseCase,
    IFilterCategoriesUseCase filterCategoriesUseCase
) : ControllerBase
{
    /// <summary>
    /// Retrieves a paginated list of categories.
    /// </summary>
    /// <remarks>
    /// Supports filtering by identifier, version, creation date,
    /// update date, name, description and active status.
    /// Supports pagination and sort direction.
    /// Requires a valid JWT and the categories.read permission.
    /// </remarks>
    /// <param name="dto">Filters and pagination parameters.</param>
    /// <param name="ct">Cancellation token for the HTTP request.</param>
    /// <response code="200">The categories were retrieved successfully.</response>
    /// <response code="400">The filter parameters are invalid.</response>
    /// <response code="401">Authentication is required or the JWT is invalid.</response>
    /// <response code="403">The user lacks permission to read categories.</response>
    /// <response code="500">An internal error prevented the query.</response>
    [HttpGet]
    [RequirePermissions("categories.read")]
    [ProducesResponseType(
        typeof(ResponseHttp<Page<CategoryDto>>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        typeof(ResponseHttp<object>),
        StatusCodes.Status400BadRequest)]
    [ProducesResponseType(
        typeof(ResponseHttp<object>),
        StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(
        typeof(ResponseHttp<object>),
        StatusCodes.Status403Forbidden)]
    [ProducesResponseType(
        typeof(ResponseHttp<object>),
        StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Filter(
        [FromQuery] CategoryFilterDto dto,
        CancellationToken ct)
    {
        Result<Page<CategoryModel>> result =
            await filterCategoriesUseCase.ExecuteAsync(dto, ct);

        if (result.IsFailure || result.Value is null)
        {
            return StatusCode(
                result.StatusCode,
                new ResponseHttp<Page<CategoryDto>>(
                    Data: null,
                    Message: "Error filtering categories",
                    TraceId: HttpContext.TraceIdentifier,
                    Success: false,
                    Timestamp: DateTimeOffset.UtcNow,
                    Errors: result.Errors));
        }

        Page<CategoryModel> categories = result.Value;

        Page<CategoryDto> data = Page<CategoryDto>.Create(
            categories.Data
                .Select(CategoryMapper.ToDto)
                .ToArray(),
            categories.TotalCount,
            categories.PageNumber,
            categories.PageSize);

        return Ok(
            new ResponseHttp<Page<CategoryDto>>(
                Data: data,
                Message: "Categories retrieved successfully",
                TraceId: HttpContext.TraceIdentifier,
                Success: true,
                Timestamp: DateTimeOffset.UtcNow));
    }
    
    /// <summary>
    /// Updates an existing category.
    /// </summary>
    /// <remarks>
    /// Updates a category using the supplied information.
    /// Category names must be unique.
    /// The request requires a valid JWT and the X-Inbox-Id header.
    /// The operation is processed through the Inbox transaction service.
    /// </remarks>
    /// <param name="id">Identifier of the category to update.</param>
    /// <param name="dto">Category properties to update.</param>
    /// <param name="inboxId">Inbox identifier supplied through X-Inbox-Id.</param>
    /// <param name="ct">Cancellation token for the HTTP request.</param>
    /// <response code="200">The category was successfully updated.</response>
    /// <response code="400">The identifier or request data is invalid.</response>
    /// <response code="401">Authentication is required or the JWT is invalid.</response>
    /// <response code="403">The user lacks permission to update categories.</response>
    /// <response code="404">The category does not exist.</response>
    /// <response code="409">The category name already exists, or the Inbox operation cannot be processed.</response>
    /// <response code="500">An internal error prevented the update.</response>
    [HttpPut("{id}")]
    [RequirePermissions("categories.update")]
    [ProducesResponseType(typeof(ResponseHttp<CategoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseHttp<CategoryDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseHttp<ValidationErrorResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseHttp<CategoryDto>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseHttp<CategoryDto>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseHttp<CategoryDto>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseHttp<CategoryDto>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ResponseHttp<CategoryDto>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Update(
        [FromRoute] string id,
        [FromBody] UpdateCategoryDto dto,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        string workerId = $"http-{Guid.NewGuid():N}";

        var command = new UpdateCategoryCommand(id, dto);

        Result<CategoryModel> result =
            await updateCategoryUseCase.ExecuteAsync(
                inboxId,
                workerId,
                command,
                ct);

        if (result.IsFailure)
        {
            return StatusCode(
                result.StatusCode,
                new ResponseHttp<CategoryDto>(
                    Data: null,
                    Message: "Error updating category",
                    TraceId: inboxId,
                    Success: false,
                    Timestamp: DateTimeOffset.UtcNow,
                    Errors: result.Errors));
        }

        CategoryDto category = CategoryMapper.ToDto(result.Value);

        return Ok(
            new ResponseHttp<CategoryDto>(
                Data: category,
                Message: "Category updated successfully",
                TraceId: inboxId,
                Success: true,
                Timestamp: DateTimeOffset.UtcNow));
    }
    
    /// <summary>
    /// Retrieves a category by its identifier.
    /// </summary>
    /// <remarks>
    /// Requires a valid JWT and the X-Inbox-Id header.
    /// The operation is processed through the Inbox transaction service.
    /// </remarks>
    /// <param name="id">Identifier of the category.</param>
    /// <param name="inboxId">Inbox identifier supplied through X-Inbox-Id.</param>
    /// <param name="ct">Cancellation token for the HTTP request.</param>
    /// <response code="200">The category was found.</response>
    /// <response code="400">The identifier is invalid.</response>
    /// <response code="401">Authentication is required or the JWT is invalid.</response>
    /// <response code="403">The user lacks permission to read categories.</response>
    /// <response code="404">The category does not exist.</response>
    /// <response code="409">The Inbox operation cannot be processed.</response>
    /// <response code="500">An internal error prevented the query.</response>
    [HttpGet("{id}")]
    [RequirePermissions("categories.read")]
    [ProducesResponseType(typeof(ResponseHttp<CategoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseHttp<CategoryDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseHttp<CategoryDto>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseHttp<CategoryDto>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseHttp<CategoryDto>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseHttp<CategoryDto>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ResponseHttp<CategoryDto>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> FindById(
        [FromRoute] string id,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        string workerId = $"http-{Guid.NewGuid():N}";

        Result<CategoryModel> result =
            await findCategoryByIdUseCase.ExecuteAsync(
                inboxId,
                workerId,
                id,
                ct);

        if (result.IsFailure)
        {
            return StatusCode(
                result.StatusCode,
                new ResponseHttp<CategoryDto>(
                    Data: null,
                    Message: "Error finding category",
                    TraceId: HttpContext.TraceIdentifier,
                    Success: false,
                    Timestamp: DateTimeOffset.UtcNow,
                    Errors: result.Errors));
        }

        CategoryDto category = CategoryMapper.ToDto(result.Value);

        return Ok(
            new ResponseHttp<CategoryDto>(
                Data: category,
                Message: "Category found",
                TraceId: HttpContext.TraceIdentifier,
                Success: true,
                Timestamp: DateTimeOffset.UtcNow));
    }
    
    /// <summary>
    /// Checks whether a category exists by its name.
    /// </summary>
    /// <remarks>
    /// Requires a valid JWT and the categories.read permission.
    /// </remarks>
    /// <param name="name">Name of the category to check.</param>
    /// <param name="ct">Cancellation token for the HTTP request.</param>
    /// <response code="200">The check succeeded.</response>
    /// <response code="400">The category name is invalid.</response>
    /// <response code="401">Authentication is required or the JWT is invalid.</response>
    /// <response code="403">The user lacks permission to read categories.</response>
    /// <response code="500">An internal error prevented the check.</response>
    [HttpGet("Exists/name/{name}")]
    [RequirePermissions("categories.read")]
    [ProducesResponseType(typeof(ResponseHttp<bool?>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseHttp<bool?>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseHttp<ValidationErrorResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseHttp<bool?>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseHttp<bool?>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseHttp<bool?>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ExistsByName(
        [FromRoute, Required] string name,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        Result<bool> result =
            await existsCategoryByNameUseCase.ExecuteAsync(name, ct);

        if (result.IsFailure)
        {
            return StatusCode(
                result.StatusCode,
                new ResponseHttp<bool?>(
                    Data: null,
                    Message: "Error checking category name",
                    TraceId: inboxId,
                    Success: false,
                    Timestamp: DateTimeOffset.UtcNow,
                    Errors: result.Errors));
        }

        return Ok(
            new ResponseHttp<bool?>(
                Data: result.Value,
                Message: result.Value
                    ? "Category exists"
                    : "Category does not exist",
                TraceId: inboxId,
                Success: true,
                Timestamp: DateTimeOffset.UtcNow));
    }
    
    /// <summary>
    /// Deletes a category by its identifier.
    /// </summary>
    /// <remarks>
    /// Deletes an existing category.
    /// The request requires a valid JWT and the X-Inbox-Id header.
    /// The operation is processed through the Inbox transaction service.
    /// </remarks>
    /// <param name="id">Identifier of the category to delete.</param>
    /// <param name="inboxId">Inbox identifier supplied through X-Inbox-Id.</param>
    /// <param name="ct">Cancellation token for the HTTP request.</param>
    /// <response code="200">The category was successfully deleted.</response>
    /// <response code="400">The identifier is invalid or the category does not exist.</response>
    /// <response code="401">Authentication is required or the JWT is invalid.</response>
    /// <response code="403">The user lacks permission to delete categories.</response>
    /// <response code="409">The Inbox operation cannot be processed.</response>
    /// <response code="500">An internal error prevented deletion.</response>
    [HttpDelete("{id}")]
    [RequirePermissions("categories.delete")]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Delete(
        [FromRoute] string id,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        string workerId = $"http-{Guid.NewGuid():N}";

        Result<object> result =
            await deleteCategoryUseCase.ExecuteAsync(
                inboxId,
                workerId,
                id,
                ct);

        if (result.IsFailure)
        {
            return StatusCode(
                result.StatusCode,
                new ResponseHttp<object>(
                    Data: null,
                    Message: "Error deleting category",
                    TraceId: HttpContext.TraceIdentifier,
                    Success: false,
                    Timestamp: DateTimeOffset.UtcNow,
                    Errors: result.Errors));
        }

        return Ok(
            new ResponseHttp<object>(
                Data: result.Value,
                Message: "Category deleted",
                TraceId: HttpContext.TraceIdentifier,
                Success: true,
                Timestamp: DateTimeOffset.UtcNow));
    }
    
    /// <summary>
    /// Creates a new category.
    /// </summary>
    /// <remarks>
    /// Creates a category using the supplied information.
    /// Category names must be unique.
    /// The request requires a valid JWT and the X-Inbox-Id header.
    /// The operation is processed through the Inbox transaction service.
    /// </remarks>
    /// <param name="dto">Information required to create the category.</param>
    /// <param name="inboxId">
    /// Identifier of the Inbox message supplied through X-Inbox-Id.
    /// </param>
    /// <param name="ct">Cancellation token for the HTTP request.</param>
    /// <response code="201">The category was successfully created.</response>
    /// <response code="400">The request contains invalid data.</response>
    /// <response code="401">Authentication is required or the JWT is invalid.</response>
    /// <response code="403">The authenticated user lacks permission to create categories.</response>
    /// <response code="409">A category with the supplied name already exists, or the Inbox operation cannot be processed.</response>
    /// <response code="500">An internal error prevented category creation.</response>
    [HttpPost]
    [RequirePermissions("categories.create")]
    [ProducesResponseType(typeof(ResponseHttp<CategoryDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseHttp<ValidationErrorResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseHttp<CategoryDto>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseHttp<CategoryDto>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseHttp<CategoryDto>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ResponseHttp<CategoryDto>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Create(
        [FromBody] CreateCategoryDto dto,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        string workerId = $"http-{Guid.NewGuid():N}";

        Result<CategoryModel> result =
            await createCategoryUseCase.ExecuteAsync(
                inboxId,
                workerId,
                dto,
                ct);

        if (result.IsFailure || result.Value is null)
        {
            return StatusCode(
                result.StatusCode,
                new ResponseHttp<CategoryDto>(
                    Data: null,
                    Message: "Error creating category",
                    TraceId: HttpContext.TraceIdentifier,
                    Success: false,
                    Timestamp: DateTimeOffset.UtcNow,
                    Errors: result.Errors));
        }

        return StatusCode(
            StatusCodes.Status201Created,
            new ResponseHttp<CategoryDto>(
                Data: CategoryMapper.ToDto(result.Value),
                Message: "Category created",
                TraceId: HttpContext.TraceIdentifier,
                Success: true,
                Timestamp: DateTimeOffset.UtcNow));
    }
}