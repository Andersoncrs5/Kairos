using System.ComponentModel.DataAnnotations;
using Asp.Versioning;
using Kairos.Api.Common.Page;
using Kairos.Api.Common.Res;
using Kairos.Api.Common.Result;
using Kairos.Api.Config.Authentication;
using Kairos.Api.Config.DB.Transaction;
using Kairos.Api.Modules.Identity.Permissions.DTOs.Commands;
using Kairos.Api.Modules.Identity.Permissions.DTOs.Request;
using Kairos.Api.Modules.Identity.Permissions.DTOs.Response;
using Kairos.Api.Modules.Identity.Permissions.Mapper;
using Kairos.Api.Modules.Identity.Permissions.Models;
using Kairos.Api.Modules.Identity.Permissions.Services.Interface;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kairos.Api.Modules.Identity.Permissions.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/permissions")]
[ApiVersion("1.0")]
[Produces("application/json")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class PermissionController(
    IInboxTransactionUseCase<CreatePermissionDto, PermissionsModel, ICreatePermissionUseCase> createPermissionUseCase,
    IInboxTransactionUseCase<string, object, IDeletePermissionByIdUseCase> deletePermissionUseCase,
    IInboxTransactionUseCase<string, bool, IExistsPermissionByCodeUseCase> existPermissionUseCase,
    IInboxTransactionUseCase<string, PermissionsModel, IFindPermissionByIdUseCase> findPermissionByIdUseCase,
    IInboxTransactionUseCase<string, PermissionsModel, IFindPermissionByCodeUseCase> findPermissionByCodeUseCase,
    IInboxTransactionUseCase<UpdatePermissionCommand, PermissionsModel, IUpdatePermissionUseCase> updatePermissionUseCase,
    IFindAllPermissionUseCase findAllPermissions
    ) : ControllerBase
{
    
    /// <summary>
    /// Retrieves a paginated list of permissions.
    /// </summary>
    /// <remarks>
    /// Supports filtering by code, name, module, system status and active status.
    /// Requires a valid JWT and the identity.permissions.list permission.
    /// </remarks>
    /// <param name="dto">Filters and pagination parameters.</param>
    /// <param name="ct">Cancellation token for the HTTP request.</param>
    /// <response code="200">The permissions were retrieved successfully.</response>
    /// <response code="400">The query parameters are invalid.</response>
    /// <response code="401">Authentication is required or the JWT is invalid.</response>
    /// <response code="403">The user lacks permission to list permissions.</response>
    /// <response code="500">An internal error prevented the query.</response>
    [HttpGet]
    [RequirePermissions("identity.permissions.list")]
    [ProducesResponseType(typeof(ResponseHttp<Page<PermissionDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Find(
        [FromQuery] PermissionFilterDto dto,
        CancellationToken ct)
    {
        Page<PermissionsModel> result =
            await findAllPermissions.ExecuteAsync(dto, ct);

        Page<PermissionDto> data = Page<PermissionDto>.Create(
            result.Data
                .Select(PermissionMapper.ToDto)
                .ToArray(),
            result.TotalCount,
            result.PageNumber,
            result.PageSize);

        return Ok(
            new ResponseHttp<Page<PermissionDto>>(
                Data: data,
                Message: "Permissions retrieved successfully",
                TraceId: HttpContext.TraceIdentifier,
                Success: true,
                Timestamp: DateTimeOffset.UtcNow));
    }
    
    /// <summary>
    /// Updates an existing permission.
    /// </summary>
    /// <remarks>
    /// Updates a permission using the supplied information.
    /// System permissions cannot be modified.
    /// The request requires a valid JWT and the X-Inbox-Id header.
    /// The operation is processed through the Inbox transaction service.
    /// </remarks>
    /// <param name="id">Identifier of the permission to update.</param>
    /// <param name="dto">Permission properties to update.</param>
    /// <param name="inboxId">Inbox identifier supplied through X-Inbox-Id.</param>
    /// <param name="ct">Cancellation token for the HTTP request.</param>
    /// <response code="200">The permission was successfully updated.</response>
    /// <response code="400">The identifier or request data is invalid.</response>
    /// <response code="401">Authentication is required or the JWT is invalid.</response>
    /// <response code="403">The user lacks permission, or the permission is a system permission.</response>
    /// <response code="404">The permission does not exist.</response>
    /// <response code="409">The Inbox operation cannot be processed.</response>
    /// <response code="500">An internal error prevented the update.</response>
    [HttpPut("{id}")]
    [RequirePermissions("identity.permissions.update")]
    [ProducesResponseType(typeof(ResponseHttp<PermissionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseHttp<PermissionDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseHttp<PermissionDto>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseHttp<PermissionDto>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseHttp<PermissionDto>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseHttp<PermissionDto>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ResponseHttp<PermissionDto>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Update(
        [FromRoute] string id,
        [FromBody] UpdatePermissionDto dto,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        string workerId = $"http-{Guid.NewGuid():N}";

        var command = new UpdatePermissionCommand(id, dto);

        Result<PermissionsModel> result =
            await updatePermissionUseCase.ExecuteAsync(
                inboxId,
                workerId,
                command,
                ct);

        if (result.IsFailure)
        {
            return StatusCode(
                result.StatusCode,
                new ResponseHttp<PermissionDto>(
                    Data: null,
                    Message: "Error updating permission",
                    TraceId: HttpContext.TraceIdentifier,
                    Success: false,
                    Timestamp: DateTimeOffset.UtcNow,
                    Errors: result.Errors));
        }

        PermissionDto permission = PermissionMapper.ToDto(result.Value);

        return Ok(
            new ResponseHttp<PermissionDto>(
                Data: permission,
                Message: "Permission updated successfully",
                TraceId: HttpContext.TraceIdentifier,
                Success: true,
                Timestamp: DateTimeOffset.UtcNow));
    }
    
    /// <summary>
    /// Retrieves a permission by its code.
    /// </summary>
    /// <remarks>
    /// Requires a valid JWT and the X-Inbox-Id header.
    /// The operation is processed through the Inbox transaction service.
    /// </remarks>
    /// <param name="code">Code of the permission to retrieve.</param>
    /// <param name="inboxId">Inbox identifier supplied through X-Inbox-Id.</param>
    /// <param name="ct">Cancellation token for the HTTP request.</param>
    /// <response code="200">The permission was found.</response>
    /// <response code="400">The permission code is required.</response>
    /// <response code="401">Authentication is required or the JWT is invalid.</response>
    /// <response code="403">The user lacks permission to read permissions.</response>
    /// <response code="404">The permission does not exist.</response>
    /// <response code="409">The Inbox operation cannot be processed.</response>
    /// <response code="500">An internal error prevented the query.</response>
    [HttpGet("code/{code}")]
    [RequirePermissions("identity.permissions.read")]
    [ProducesResponseType(typeof(ResponseHttp<PermissionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseHttp<PermissionDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseHttp<PermissionDto>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseHttp<PermissionDto>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseHttp<PermissionDto>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseHttp<PermissionDto>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ResponseHttp<PermissionDto>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> FindByCode(
        [FromRoute] string code,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        string workerId = $"http-{Guid.NewGuid():N}";

        Result<PermissionsModel> result =
            await findPermissionByCodeUseCase.ExecuteAsync(
                inboxId,
                workerId,
                code,
                ct);

        if (result.IsFailure)
        {
            return StatusCode(
                result.StatusCode,
                new ResponseHttp<PermissionDto>(
                    Data: null,
                    Message: "Error finding permission by code",
                    TraceId: HttpContext.TraceIdentifier,
                    Success: false,
                    Timestamp: DateTimeOffset.UtcNow,
                    Errors: result.Errors));
        }

        PermissionDto permission = PermissionMapper.ToDto(result.Value);

        return Ok(
            new ResponseHttp<PermissionDto>(
                Data: permission,
                Message: "Permission found",
                TraceId: HttpContext.TraceIdentifier,
                Success: true,
                Timestamp: DateTimeOffset.UtcNow));
    }
    
    /// <summary>
    /// Creates a new permission.
    /// </summary>
    /// <remarks>
    /// Creates a permission using the supplied information.
    /// The request must include a valid JWT and the
    /// X-Inbox-Id header to identify the incoming operation.
    ///
    /// Permission codes must be unique.
    /// The response includes a trace identifier for request tracking.
    /// </remarks>
    /// <param name="dto">Information required to create the permission.</param>
    /// <param name="inboxId">
    /// Identifier of the Inbox message supplied through X-Inbox-Id.
    /// </param>
    /// <param name="ct">Cancellation token for the HTTP request.</param>
    /// <response code="201">
    /// The permission was successfully created.
    /// </response>
    /// <response code="400">
    /// The request contains invalid data.
    /// </response>
    /// <response code="401">
    /// Authentication is required or the JWT is invalid.
    /// </response>
    /// <response code="403">
    /// The authenticated user does not have permission to create permissions.
    /// </response>
    /// <response code="409">
    /// A permission with the supplied code already exists, or the Inbox
    /// operation cannot be processed.
    /// </response>
    /// <response code="500">
    /// An internal error prevented permission creation.
    /// </response>
    [HttpPost]
    [RequirePermissions("identity.permissions.create")]
    [ProducesResponseType(typeof(ResponseHttp<PermissionDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ResponseHttp<PermissionDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseHttp<PermissionDto>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseHttp<PermissionDto>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseHttp<PermissionDto>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ResponseHttp<PermissionDto>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Create(
        [FromBody] CreatePermissionDto dto,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        string workerId = $"http-{Guid.NewGuid():N}";

        Result<PermissionsModel> result = await createPermissionUseCase.ExecuteAsync(inboxId, workerId, dto, ct);

        if (result.IsFailure || result.Value == null)
        {
            return StatusCode(
                result.StatusCode,
                new ResponseHttp<PermissionDto>(
                    Data: null,
                    Message: "Error creating permission",
                    TraceId: HttpContext.TraceIdentifier,
                    Success: false,
                    Timestamp: DateTimeOffset.UtcNow,
                    Errors: result.Errors));
        }

        PermissionDto permission = PermissionMapper.ToDto(result.Value);

        return StatusCode(
            StatusCodes.Status201Created,
            new ResponseHttp<PermissionDto>(
                Data: permission,
                Message: "Permission created",
                TraceId: HttpContext.TraceIdentifier,
                Success: true,
                Timestamp: DateTimeOffset.UtcNow
                )
            );
    }
    
    /// <summary>
    /// Deletes a permission by its identifier.
    /// </summary>
    /// <remarks>
    /// Deletes an existing permission unless it is a system permission.
    /// The request requires a valid JWT and the X-Inbox-Id header.
    /// The operation is processed through the Inbox transaction service.
    /// </remarks>
    /// <param name="id">Identifier of the permission to delete.</param>
    /// <param name="inboxId">Inbox identifier supplied through X-Inbox-Id.</param>
    /// <param name="ct">Cancellation token for the HTTP request.</param>
    /// <response code="200">The permission was successfully deleted.</response>
    /// <response code="400">The identifier is invalid or the request is invalid.</response>
    /// <response code="401">Authentication is required or the JWT is invalid.</response>
    /// <response code="403">The user lacks permission, or the permission is a system permission.</response>
    /// <response code="404">The permission does not exist.</response>
    /// <response code="409">The Inbox operation cannot be processed.</response>
    /// <response code="500">An internal error prevented deletion.</response>
    [HttpDelete("{id}")]
    [RequirePermissions("identity.permissions.delete")]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Delete(
        [FromRoute] string id,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        string workerId = $"http-{Guid.NewGuid():N}";

        Result<object> result = await deletePermissionUseCase.ExecuteAsync(
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
                    Message: "Error deleting permission",
                    TraceId: HttpContext.TraceIdentifier,
                    Success: false,
                    Timestamp: DateTimeOffset.UtcNow,
                    Errors: result.Errors));
        }

        return StatusCode(
            StatusCodes.Status200OK,
            new ResponseHttp<object>(
                Data: result.Value,
                Message: "Permission deleted",
                TraceId: HttpContext.TraceIdentifier,
                Success: true,
                Timestamp: DateTimeOffset.UtcNow));
    }
    
    /// <summary>
    /// Checks whether a permission exists by its code.
    /// </summary>
    /// <remarks>
    /// The request requires a valid JWT and the X-Inbox-Id header.
    /// The operation is processed through the Inbox transaction service.
    /// Code comparison follows the repository's case-insensitive behavior.
    /// </remarks>
    /// <param name="code">Code of the permission to check.</param>
    /// <param name="inboxId">Inbox identifier supplied through X-Inbox-Id.</param>
    /// <param name="ct">Cancellation token for the HTTP request.</param>
    /// <response code="200">
    /// The check succeeded. Data is true if the permission exists;
    /// otherwise, it is false.
    /// </response>
    /// <response code="400">
    /// The permission code is missing or exceeds the maximum length.
    /// </response>
    /// <response code="401">
    /// Authentication is required or the JWT is invalid.
    /// </response>
    /// <response code="403">
    /// The authenticated user lacks permission to read permissions.
    /// </response>
    /// <response code="409">
    /// The Inbox operation cannot be processed.
    /// </response>
    /// <response code="500">
    /// An internal error prevented the check.
    /// </response>
    [HttpGet("Exists/code/{code}")]
    [RequirePermissions("identity.permissions.read")]
    [ProducesResponseType(typeof(ResponseHttp<bool?>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseHttp<bool?>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseHttp<bool?>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseHttp<bool?>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseHttp<bool?>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ResponseHttp<bool?>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ExistsByCode(
        [FromRoute] string code,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        string workerId = $"http-{Guid.NewGuid():N}";

        Result<bool> result = await existPermissionUseCase.ExecuteAsync(
            inboxId,
            workerId,
            code,
            ct);

        if (result.IsFailure)
        {
            return StatusCode(
                result.StatusCode,
                new ResponseHttp<bool?>(
                    Data: null,
                    Message: "Error checking permission code",
                    TraceId: HttpContext.TraceIdentifier,
                    Success: false,
                    Timestamp: DateTimeOffset.UtcNow,
                    Errors: result.Errors));
        }

        return Ok(
            new ResponseHttp<bool?>(
                Data: result.Value,
                Message: result.Value
                    ? "Permission exists"
                    : "Permission does not exist",
                TraceId: HttpContext.TraceIdentifier,
                Success: true,
                Timestamp: DateTimeOffset.UtcNow));
    }
    
    /// <summary>
    /// Retrieves a permission by its identifier.
    /// </summary>
    /// <remarks>
    /// Requires a valid JWT and the X-Inbox-Id header.
    /// The operation is processed through the Inbox transaction service.
    /// </remarks>
    /// <param name="id">Identifier of the permission.</param>
    /// <param name="inboxId">Inbox identifier supplied through X-Inbox-Id.</param>
    /// <param name="ct">Cancellation token for the HTTP request.</param>
    /// <response code="200">The permission was found.</response>
    /// <response code="400">The identifier is invalid.</response>
    /// <response code="401">Authentication is required or the JWT is invalid.</response>
    /// <response code="403">The user lacks permission to read permissions.</response>
    /// <response code="404">The permission does not exist.</response>
    /// <response code="409">The Inbox operation cannot be processed.</response>
    /// <response code="500">An internal error prevented the query.</response>
    [HttpGet("{id}")]
    [RequirePermissions("identity.permissions.read")]
    [ProducesResponseType(typeof(ResponseHttp<PermissionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseHttp<PermissionDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseHttp<PermissionDto>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseHttp<PermissionDto>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseHttp<PermissionDto>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseHttp<PermissionDto>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ResponseHttp<PermissionDto>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> FindById(
        [FromRoute] string id,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        string workerId = $"http-{Guid.NewGuid():N}";

        Result<PermissionsModel> result =
            await findPermissionByIdUseCase.ExecuteAsync(
                inboxId,
                workerId,
                id,
                ct);

        if (result.IsFailure)
        {
            return StatusCode(
                result.StatusCode,
                new ResponseHttp<PermissionDto>(
                    Data: null,
                    Message: "Error finding permission",
                    TraceId: HttpContext.TraceIdentifier,
                    Success: false,
                    Timestamp: DateTimeOffset.UtcNow,
                    Errors: result.Errors));
        }

        PermissionDto permission = PermissionMapper.ToDto(result.Value);

        return Ok(
            new ResponseHttp<PermissionDto>(
                Data: permission,
                Message: "Permission found",
                TraceId: HttpContext.TraceIdentifier,
                Success: true,
                Timestamp: DateTimeOffset.UtcNow));
    }
}