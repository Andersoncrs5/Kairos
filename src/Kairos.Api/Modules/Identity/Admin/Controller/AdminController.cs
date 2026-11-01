using System.ComponentModel.DataAnnotations;
using System.Net;
using Asp.Versioning;
using Kairos.Api.Common.Res;
using Kairos.Api.Common.Result;
using Kairos.Api.Config.Authentication;
using Kairos.Api.Config.DB.Transaction;
using Kairos.Api.Modules.Identity.Admin.DTOs.Request;
using Kairos.Api.Modules.Identity.Users.DTOs.Commands;
using Kairos.Api.Modules.Identity.Users.Services.Interface;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kairos.Api.Modules.Identity.Admin.Controller;

/// <summary>
/// Provides administrative operations for identity management.
/// </summary>
[ApiController]
[Route("api/v{version:apiVersion}/admin")]
[ApiVersion("1.0")]
[Produces("application/json")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public sealed class AdminController(
    IInboxTransactionUseCase<SetUserActiveStatusCommand, bool, ISetUserActiveStatusUseCase> setUserActiveStatus
) : ControllerBase
{
    // ----------------------------------------------------------
    // Users
    // ----------------------------------------------------------

    /// <summary>
    /// Activates or deactivates a user account.
    /// </summary>
    /// <remarks>
    /// Changes the account activation status of an existing user.
    ///
    /// The request requires an X-Inbox-Id header to identify
    /// the incoming message.
    ///
    /// The operation executes through the Inbox transaction
    /// service to ensure transactional consistency and
    /// idempotent message processing.
    ///
    /// When an account is deactivated, its refresh token
    /// is revoked.
    /// </remarks>
    /// <param name="userId">
    /// Identifier of the user whose account status will change.
    /// </param>
    /// <param name="request">
    /// Desired account activation status.
    /// </param>
    /// <param name="inboxId">
    /// Identifier of the Inbox message supplied through X-Inbox-Id.
    /// </param>
    /// <param name="ct">
    /// Cancellation token for the HTTP request.
    /// </param>
    /// <response code="200">
    /// The user account status was successfully updated.
    /// </response>
    /// <response code="400">
    /// The request contains invalid data.
    /// </response>
    /// <response code="401">
    /// Authentication is required.
    /// </response>
    /// <response code="403">
    /// The authenticated user does not have permission to perform this operation.
    /// </response>
    /// <response code="404">
    /// The specified user was not found.
    /// </response>
    /// <response code="409">
    /// The Inbox message is unavailable for processing.
    /// </response>
    /// <response code="500">
    /// An internal error prevented the account status update.
    /// </response>
    [HttpPatch("Users/{userId}/Status")]
    [RequirePermissions("identity.users.status.update")]
    [ProducesResponseType(typeof(ResponseHttp<bool>), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ResponseHttp<bool>), (int)HttpStatusCode.BadRequest)]
    [ProducesResponseType(typeof(ResponseHttp<bool>), (int)HttpStatusCode.Unauthorized)]
    [ProducesResponseType(typeof(ResponseHttp<bool>), (int)HttpStatusCode.Forbidden)]
    [ProducesResponseType(typeof(ResponseHttp<bool>), (int)HttpStatusCode.NotFound)]
    [ProducesResponseType(typeof(ResponseHttp<bool>), (int)HttpStatusCode.Conflict)]
    [ProducesResponseType(typeof(ResponseHttp<bool>), (int)HttpStatusCode.InternalServerError)]
    public async Task<IActionResult> SetUserActiveStatus(
        [FromRoute, Required] string userId,
        [FromBody] SetUserActiveStatusRequest request,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        string workerId = $"http-{Guid.NewGuid():N}";

        var command = new SetUserActiveStatusCommand
        {
            UserId = userId,
            IsActive = request.IsActive
        };

        Result<bool> result = await setUserActiveStatus.ExecuteAsync(inboxId, workerId, command, ct);

        if (result.IsFailure)
        {
            return StatusCode(result.StatusCode, new ResponseHttp<bool>(
                Data: false,
                Message: "Error updating user account status",
                TraceId: inboxId,
                Success: false,
                Timestamp: DateTimeOffset.UtcNow,
                Errors: result.Errors)
            );
        }

        return StatusCode(StatusCodes.Status200OK, new ResponseHttp<bool>(
            Data: result.Value,
            Message: request.IsActive
                ? "User account activated successfully"
                : "User account deactivated successfully",
            TraceId: inboxId,
            Success: true,
            Timestamp: DateTimeOffset.UtcNow)
        );
    }
}


