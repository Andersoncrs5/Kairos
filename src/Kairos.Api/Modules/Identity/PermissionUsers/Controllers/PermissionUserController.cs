using System.ComponentModel.DataAnnotations;
using Asp.Versioning;
using Kairos.Api.Common.Res;
using Kairos.Api.Common.Result;
using Kairos.Api.Config.Authentication;
using Kairos.Api.Config.DB.Transaction;
using Kairos.Api.Modules.Identity.PermissionUsers.DTOs.Commands;
using Kairos.Api.Modules.Identity.PermissionUsers.DTOs.Request;
using Kairos.Api.Modules.Identity.PermissionUsers.DTOs.Response;
using Kairos.Api.Modules.Identity.PermissionUsers.Mapper;
using Kairos.Api.Modules.Identity.PermissionUsers.Models;
using Kairos.Api.Modules.Identity.PermissionUsers.Services.Interface;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kairos.Api.Modules.Identity.PermissionUsers.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/permission-users")]
[ApiVersion("1.0")]
[Produces("application/json")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class PermissionUserController(
    IInboxTransactionUseCase<CreatePermissionUserDTO, PermissionUserModel, ICreatePermissionUserUseCase> createPermissionUserUseCase,
    IInboxTransactionUseCase<DeletePermissionUserCommand, object, IDeletePermissionUserByUserIdAndPermissionIdUseCase> deletePermissionUserUseCase,
    IExistsPermissionUserByUserIdAndPermissionIdUseCase existsPermissionUserByUserIdAndPermissionIdUseCase,
    IFindPermissionUserByIdUseCase findPermissionUserByIdUseCase,
    IInboxTransactionUseCase<UpdatePermissionUserCommand, PermissionUserModel, IUpdatePermissionUserUseCase> updatePermissionUserUseCase
) : ControllerBase
{
    /// <summary>
    /// Updates an existing user-permission association.
    /// </summary>
    /// <remarks>
    /// Updates the association using the supplied information.
    /// Requires a valid JWT, the identity.permission-users.update permission,
    /// and the X-Inbox-Id header.
    /// The operation is processed through the Inbox transaction service.
    /// </remarks>
    /// <param name="id">Identifier of the user-permission association.</param>
    /// <param name="dto">Properties of the association to update.</param>
    /// <param name="inboxId">Inbox identifier supplied through X-Inbox-Id.</param>
    /// <param name="ct">Cancellation token for the HTTP request.</param>
    /// <response code="200">The association was successfully updated.</response>
    /// <response code="400">The identifier or request data is invalid.</response>
    /// <response code="401">Authentication is required or the JWT is invalid.</response>
    /// <response code="403">The user lacks permission to update associations.</response>
    /// <response code="404">The association does not exist.</response>
    /// <response code="409">The Inbox operation cannot be processed.</response>
    /// <response code="500">An internal error prevented the update.</response>
    [HttpPut("{id}")]
    [RequirePermissions("identity.permission-users.update")]
    [ProducesResponseType(typeof(ResponseHttp<PermissionUserDTO>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseHttp<ValidationErrorResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseHttp<PermissionUserDTO>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseHttp<PermissionUserDTO>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseHttp<PermissionUserDTO>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseHttp<PermissionUserDTO>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ResponseHttp<PermissionUserDTO>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Update(
        [FromRoute] string id,
        [FromBody] UpdatePermissionUserDTO dto,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        string workerId = $"http-{Guid.NewGuid():N}";

        var command = new UpdatePermissionUserCommand(id, dto);

        Result<PermissionUserModel> result =
            await updatePermissionUserUseCase.ExecuteAsync(
                inboxId,
                workerId,
                command,
                ct);

        if (result.IsFailure)
        {
            return StatusCode(
                result.StatusCode,
                new ResponseHttp<PermissionUserDTO>(
                    Data: null,
                    Message: "Error updating user-permission association",
                    TraceId: inboxId,
                    Success: false,
                    Timestamp: DateTimeOffset.UtcNow,
                    Errors: result.Errors));
        }

        PermissionUserDTO permissionUser =
            PermissionUserMapper.ToDto(result.Value);

        return Ok(
            new ResponseHttp<PermissionUserDTO>(
                Data: permissionUser,
                Message: "User-permission association updated successfully",
                TraceId: inboxId,
                Success: true,
                Timestamp: DateTimeOffset.UtcNow));
    }
    
    /// <summary>
    /// Retrieves a user-permission association by its identifier.
    /// </summary>
    /// <remarks>
    /// Requires a valid JWT and the identity.permission-users.read permission.
    /// </remarks>
    /// <param name="id">Identifier of the user-permission association.</param>
    /// <param name="ct">Cancellation token for the HTTP request.</param>
    /// <response code="200">The association was found.</response>
    /// <response code="400">The identifier is invalid.</response>
    /// <response code="401">Authentication is required or the JWT is invalid.</response>
    /// <response code="403">The user lacks permission to read user permissions.</response>
    /// <response code="404">The association does not exist.</response>
    /// <response code="500">An internal error prevented the query.</response>
    [HttpGet("{id}")]
    [RequirePermissions("identity.permission-users.read")]
    [ProducesResponseType(typeof(ResponseHttp<PermissionUserDTO>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseHttp<ValidationErrorResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseHttp<PermissionUserDTO>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseHttp<PermissionUserDTO>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseHttp<PermissionUserDTO>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseHttp<PermissionUserDTO>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> FindById(
        [FromRoute] string id,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        Result<PermissionUserModel> result =
            await findPermissionUserByIdUseCase.ExecuteAsync(id, ct);

        if (result.IsFailure || result.Value == null)
        {
            return StatusCode(
                result.StatusCode,
                new ResponseHttp<PermissionUserDTO>(
                    Data: null,
                    Message: "Error finding user-permission association",
                    TraceId: inboxId,
                    Success: false,
                    Timestamp: DateTimeOffset.UtcNow,
                    Errors: result.Errors));
        }

        PermissionUserDTO permissionUser =
            PermissionUserMapper.ToDto(result.Value);

        return Ok(
            new ResponseHttp<PermissionUserDTO>(
                Data: permissionUser,
                Message: "User-permission association found",
                TraceId: inboxId,
                Success: true,
                Timestamp: DateTimeOffset.UtcNow));
    }
    
    /// <summary>
    /// Checks whether a permission is actively assigned to a user.
    /// </summary>
    /// <remarks>
    /// Requires a valid JWT and the identity.permission-users.read permission.
    /// Returns true if the user has the active permission association;
    /// otherwise, returns false.
    /// </remarks>
    /// <param name="userId">Identifier of the user.</param>
    /// <param name="permissionId">Identifier of the permission.</param>
    /// <param name="ct">Cancellation token for the HTTP request.</param>
    /// <response code="200">The existence check was completed successfully.</response>
    /// <response code="400">The identifiers are invalid.</response>
    /// <response code="401">Authentication is required or the JWT is invalid.</response>
    /// <response code="403">The user lacks permission to read user permissions.</response>
    /// <response code="500">An internal error prevented the check.</response>
    [HttpGet("exists/{userId}/{permissionId}")]
    [RequirePermissions("identity.permission-users.read")]
    [ProducesResponseType(typeof(ResponseHttp<bool?>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseHttp<ValidationErrorResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseHttp<bool?>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseHttp<bool?>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseHttp<bool?>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Exists(
        [FromRoute] string userId,
        [FromRoute] string permissionId,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        Result<bool> result =
            await existsPermissionUserByUserIdAndPermissionIdUseCase
                .ExecuteAsync(userId, permissionId, ct);

        if (result.IsFailure)
        {
            return StatusCode(
                result.StatusCode,
                new ResponseHttp<bool?>(
                    Data: null,
                    Message: "Error checking user permission",
                    TraceId: inboxId,
                    Success: false,
                    Timestamp: DateTimeOffset.UtcNow,
                    Errors: result.Errors));
        }

        return Ok(
            new ResponseHttp<bool?>(
                Data: result.Value,
                Message: result.Value
                    ? "Permission is actively assigned to user"
                    : "Permission is not actively assigned to user",
                TraceId: inboxId,
                Success: true,
                Timestamp: DateTimeOffset.UtcNow));
    }
    
    /// <summary>
    /// Revokes a permission from a user.
    /// </summary>
    /// <remarks>
    /// Removes the association between the specified user and permission.
    /// Requires a valid JWT, the identity.permission-users.delete permission,
    /// and the X-Inbox-Id header.
    /// The operation is processed through the Inbox transaction service.
    /// </remarks>
    /// <param name="userId">Identifier of the user.</param>
    /// <param name="permissionId">Identifier of the permission.</param>
    /// <param name="inboxId">Inbox identifier supplied through X-Inbox-Id.</param>
    /// <param name="ct">Cancellation token for the HTTP request.</param>
    /// <response code="204">The permission was successfully revoked.</response>
    /// <response code="400">The identifiers or request data are invalid.</response>
    /// <response code="401">Authentication is required or the JWT is invalid.</response>
    /// <response code="403">The user lacks permission to revoke permissions.</response>
    /// <response code="404">The user, permission, or association was not found.</response>
    /// <response code="409">The Inbox operation cannot be processed.</response>
    /// <response code="500">An internal error prevented the operation.</response>
    [HttpDelete("{userId}/{permissionId}")]
    [RequirePermissions("identity.permission-users.delete")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ResponseHttp<ValidationErrorResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Delete(
        [FromRoute] string userId,
        [FromRoute] string permissionId,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        string workerId = $"http-{Guid.NewGuid():N}";

        DeletePermissionUserCommand command = new DeletePermissionUserCommand(userId, permissionId);

        Result<object> result = await deletePermissionUserUseCase.ExecuteAsync(inboxId, workerId, command, ct);

        if (result.IsFailure)
        {
            return StatusCode(
                result.StatusCode,
                new ResponseHttp<object>(
                    Data: null,
                    Message: "Error revoking permission from user",
                    TraceId: HttpContext.TraceIdentifier,
                    Success: false,
                    Timestamp: DateTimeOffset.UtcNow,
                    Errors: result.Errors));
        }

        return Ok(new ResponseHttp<object>(
            Data: null,
            Message: "Revoking permission from user",
            TraceId: HttpContext.TraceIdentifier,
            Success: true,
            Timestamp: DateTimeOffset.UtcNow
            )
        );
    }
    
    /// <summary>
    /// Assigns a permission to a user.
    /// </summary>
    /// <remarks>
    /// The user and permission must exist.
    /// Inactive permissions cannot be assigned.
    /// A permission cannot be assigned more than once to the same user.
    /// Requires a valid JWT and the
    /// identity.permission-users.create permission.
    /// </remarks>
    /// <param name="dto">User and permission identifiers.</param>
    /// <param name="ct">Cancellation token for the HTTP request.</param>
    /// <response code="201">The permission was assigned successfully.</response>
    /// <response code="400">The request data or identifiers are invalid.</response>
    /// <response code="401">Authentication is required or the JWT is invalid.</response>
    /// <response code="403">The user lacks permission to assign permissions.</response>
    /// <response code="404">The user or permission does not exist.</response>
    /// <response code="409">The permission is inactive or already assigned.</response>
    /// <response code="500">An internal error prevented the operation.</response>
    [HttpPost]
    [RequirePermissions("identity.permission-users.create")]
    [ProducesResponseType(typeof(ResponseHttp<PermissionUserDTO>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ResponseHttp<ValidationErrorResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseHttp<PermissionUserModel>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseHttp<PermissionUserModel>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseHttp<PermissionUserModel>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseHttp<PermissionUserModel>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ResponseHttp<PermissionUserModel>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Create(
        [FromBody] CreatePermissionUserDTO dto,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        string workerId = $"http-{Guid.NewGuid():N}";
        
        Result<PermissionUserModel> result = await createPermissionUserUseCase.ExecuteAsync(inboxId, workerId, dto, ct);

        if (result.IsFailure || result.Value is null)
        {
            return StatusCode(
                result.StatusCode,
                new ResponseHttp<PermissionUserModel>(
                    Data: null,
                    Message: "Error assigning permission to user",
                    TraceId: HttpContext.TraceIdentifier,
                    Success: false,
                    Timestamp: DateTimeOffset.UtcNow,
                    Errors: result.Errors));
        }

        return StatusCode(
            StatusCodes.Status201Created,
            new ResponseHttp<PermissionUserDTO>(
                Data: PermissionUserMapper.ToDto(result.Value),
                Message: "Permission assigned to user successfully",
                TraceId: HttpContext.TraceIdentifier,
                Success: true,
                Timestamp: DateTimeOffset.UtcNow));
    }
    
}