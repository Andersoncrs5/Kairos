using System.ComponentModel.DataAnnotations;
using Asp.Versioning;
using Kairos.Api.Common.Base.Controller;
using Kairos.Api.Common.Res;
using Kairos.Api.Common.Result;
using Kairos.Api.Config.DB.Transaction;
using Kairos.Api.Modules.Identity.UserPreference.DTOs.Commands;
using Kairos.Api.Modules.Identity.UserPreference.DTOs.Requests;
using Kairos.Api.Modules.Identity.UserPreference.Models;
using Kairos.Api.Modules.Identity.UserPreference.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kairos.Api.Modules.Identity.UserPreference.Controllers;

[Route("api/v{version:apiVersion}/user-preferences")]
[ApiVersion("1.0")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public sealed class UserPreferenceController(
    IFindUserPreferenceUseCase findUserPreferenceUseCase,
    IInboxTransactionUseCase<UpdateUserPreferenceCommand, UserPreferenceModel, IUpdateUserPreferenceUseCase> updateUserPreferenceUseCase)
    : ApiControllerBase
{
    // ---------------------------------------------------------------
    // Find my preferences
    // ---------------------------------------------------------------

    /// <summary>
    /// Retrieves the preferences of the authenticated user.
    /// </summary>
    /// <remarks>
    /// Users can only retrieve their own preferences.
    /// The user is identified through the authenticated JWT.
    /// Requires the X-Inbox-Id header.
    /// </remarks>
    [HttpGet("{userId}")]
    [ProducesResponseType(typeof(ResponseHttp<UserPreferenceModel>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Find(
        string userId,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        
        Result<UserPreferenceModel> result = await findUserPreferenceUseCase.ExecuteAsync(userId, ct);

        return ToResponse(
            result,
            preference => preference,
            "User preferences retrieved successfully",
            "Error retrieving user preferences",
            inboxId);
    }
    
    // ---------------------------------------------------------------
    // Find my preferences
    // ---------------------------------------------------------------

    /// <summary>
    /// Retrieves the preferences of the authenticated user.
    /// </summary>
    /// <remarks>
    /// Users can only retrieve their own preferences.
    /// The user is identified through the authenticated JWT.
    /// Requires the X-Inbox-Id header.
    /// </remarks>
    [HttpGet("me")]
    [ProducesResponseType(typeof(ResponseHttp<UserPreferenceModel>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> FindMine(
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        string userId = GetAuthenticatedUserId(inboxId);

        Result<UserPreferenceModel> result =
            await findUserPreferenceUseCase.ExecuteAsync(
                userId,
                ct);

        return ToResponse(
            result,
            preference => preference,
            "User preferences retrieved successfully",
            "Error retrieving user preferences",
            inboxId);
    }

    // ---------------------------------------------------------------
    // Update my preferences
    // ---------------------------------------------------------------

    /// <summary>
    /// Updates the preferences of the authenticated user.
    /// </summary>
    /// <remarks>
    /// Users can only update their own preferences.
    /// The user identifier is obtained through the authenticated JWT,
    /// preventing users from modifying another user's preferences.
    /// The operation is processed through the Inbox transaction service.
    /// Requires the X-Inbox-Id header.
    /// </remarks>
    [HttpPut("me")]
    [ProducesResponseType(typeof(ResponseHttp<UserPreferenceModel>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateMine(
        [FromBody] UpdateUserPreferenceDTO Dto,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        UpdateUserPreferenceCommand command = new UpdateUserPreferenceCommand()
        {
            UserId = GetAuthenticatedUserId(inboxId),
            Dto = Dto
        };
        
        Result<UserPreferenceModel> result = await updateUserPreferenceUseCase.ExecuteAsync(inboxId, NewWorkerId(), command, ct);

        return ToResponse(
            result,
            preference => preference,
            "User preferences updated successfully",
            "Error updating user preferences",
            inboxId);
    }
}