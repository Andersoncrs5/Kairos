using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using Asp.Versioning;
using Kairos.Api.Common.Exceptions;
using Kairos.Api.Common.Page;
using Kairos.Api.Common.Res;
using Kairos.Api.Common.Result;
using Kairos.Api.Config.Authentication;
using Kairos.Api.Config.DB.Transaction;
using Kairos.Api.Modules.Identity.Users.DTOs.Commands;
using Kairos.Api.Modules.Identity.Users.DTOs.Request;
using Kairos.Api.Modules.Identity.Users.DTOs.Response;
using Kairos.Api.Modules.Identity.Users.Mapper;
using Kairos.Api.Modules.Identity.Users.Models;
using Kairos.Api.Modules.Identity.Users.Services.Interface;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kairos.Api.Modules.Identity.Users.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/users")]
[ApiVersion("1.0")]
[Produces("application/json")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class UserController(
    IInboxTransactionUseCase<UpdateUserCommand, bool, IUpdateUserUseCase> updateUser,
    IInboxTransactionUseCase<string, UserModel, IFindUserByIdUseCase> findUserById,
    IInboxTransactionUseCase<string, object, IDeleteUserByIdUseCase> deleteUserById,
    IFindUsersUseCase findUsers
    ) : ControllerBase
{
    [HttpPut("Me")]
    [ProducesResponseType(typeof(ResponseHttp<bool>), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ResponseHttp<bool>), (int)HttpStatusCode.BadRequest)]
    [ProducesResponseType(typeof(ResponseHttp<bool>), (int)HttpStatusCode.Unauthorized)]
    [ProducesResponseType(typeof(ResponseHttp<bool>), (int)HttpStatusCode.NotFound)]
    [ProducesResponseType(typeof(ResponseHttp<bool>), (int)HttpStatusCode.Conflict)]
    public async Task<IActionResult> UpdateMe(
        [FromBody] UpdateUserDTO dto,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        string userId = User.FindFirst(JwtRegisteredClaimNames.Sid)?.Value
            ?? throw new UnauthenticatedException();

        string workerId = $"http-{Guid.NewGuid():N}";

        UpdateUserCommand command = new(userId, dto);

        Result<bool> result = await updateUser.ExecuteAsync(
            inboxId,
            workerId,
            command,
            ct);

        if (result.IsFailure)
        {
            return StatusCode(
                result.StatusCode,
                new ResponseHttp<bool>(
                    Data: false,
                    Message: "Error updating user",
                    TraceId: inboxId,
                    Success: false,
                    Timestamp: DateTimeOffset.UtcNow,
                    Errors: result.Errors));
        }

        return StatusCode(
            StatusCodes.Status200OK,
            new ResponseHttp<bool>(
                Data: true,
                Message: "User updated successfully",
                TraceId: inboxId,
                Success: true,
                Timestamp: DateTimeOffset.UtcNow));
    }
    
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ResponseHttp<UserDTO>), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ResponseHttp<object>), (int)HttpStatusCode.BadRequest)]
    [ProducesResponseType(typeof(ResponseHttp<object>), (int)HttpStatusCode.Unauthorized)]
    [ProducesResponseType(typeof(ResponseHttp<UserDTO>), (int)HttpStatusCode.NotFound)]
    public async Task<IActionResult> FindById(
        [FromRoute] string id,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        string workerId = $"http-{Guid.NewGuid():N}";

        Result<UserModel> result = await findUserById.ExecuteAsync(inboxId, workerId, id, ct);

        if (result.IsFailure || result.Value == null)
        {
            return StatusCode(
                result.StatusCode,
                new ResponseHttp<UserDTO>(
                    Data: null!,
                    Message: "Error retrieving user",
                    TraceId: HttpContext.TraceIdentifier,
                    Success: false,
                    Timestamp: DateTimeOffset.UtcNow,
                    Errors: result.Errors));
        }
        
        return StatusCode(
            StatusCodes.Status200OK,
            new ResponseHttp<UserDTO>(
                Data: UserMapper.ToDto(result.Value),
                Message: "User retrieved successfully",
                TraceId: HttpContext.TraceIdentifier,
                Success: true,
                Timestamp: DateTimeOffset.UtcNow));
    }
    
    [HttpDelete]
    [ProducesResponseType(typeof(ResponseHttp<object>), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ResponseHttp<object>), (int)HttpStatusCode.BadRequest)]
    [ProducesResponseType(typeof(ResponseHttp<object>), (int)HttpStatusCode.Unauthorized)]
    [ProducesResponseType(typeof(ResponseHttp<object>), (int)HttpStatusCode.NotFound)]
    [ProducesResponseType(typeof(ResponseHttp<object>), (int)HttpStatusCode.Conflict)]
    public async Task<IActionResult> Delete(
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        string workerId = $"http-{Guid.NewGuid():N}";
        
        string userId = User.FindFirst(JwtRegisteredClaimNames.Sid)?.Value
                        ?? throw new UnauthenticatedException();

        Result<object> result = await deleteUserById.ExecuteAsync(
            inboxId,
            workerId,
            userId,
            ct);

        if (result.IsFailure)
        {
            return StatusCode(
                result.StatusCode,
                new ResponseHttp<object>(
                    Data: null!,
                    Message: "Error deleting user",
                    TraceId: inboxId,
                    Success: false,
                    Timestamp: DateTimeOffset.UtcNow,
                    Errors: result.Errors));
        }

        return StatusCode(
            StatusCodes.Status200OK,
            new ResponseHttp<object>(
                Data: result.Value!,
                Message: "User deleted successfully",
                TraceId: inboxId,
                Success: true,
                Timestamp: DateTimeOffset.UtcNow));
    }
    
    [HttpGet]
    [RequirePermissions("identity.users.list")]
    [ProducesResponseType(typeof(ResponseHttp<Page<UserDTO>>), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ResponseHttp<object>), (int)HttpStatusCode.BadRequest)]
    [ProducesResponseType(typeof(ResponseHttp<object>), (int)HttpStatusCode.Unauthorized)]
    public async Task<IActionResult> Find(
        [FromQuery] FilterUserDto dto,
        CancellationToken ct)
    {
        Page<UserModel> result = await findUsers.ExecuteAsync(dto, ct);

        Page<UserDTO> data = Page<UserDTO>.Create(
            result.Data.Select(UserMapper.ToDto).ToArray(),
            result.TotalCount,
            result.PageNumber,
            result.PageSize);

        return StatusCode(
            StatusCodes.Status200OK,
            new ResponseHttp<Page<UserDTO>>(
                Data: data,
                Message: "Users retrieved successfully",
                TraceId: HttpContext.TraceIdentifier,
                Success: true,
                Timestamp: DateTimeOffset.UtcNow));
    }
    
}