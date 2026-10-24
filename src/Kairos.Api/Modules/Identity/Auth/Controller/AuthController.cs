using System.ComponentModel.DataAnnotations;
using System.Net;
using Asp.Versioning;
using Kairos.Api.Common.Res;
using Kairos.Api.Common.Result;
using Kairos.Api.Config.DB.Transaction;
using Kairos.Api.Modules.Identity.Auth.Models;
using Kairos.Api.Modules.Identity.Auth.Services.Interfaces;
using Kairos.Api.Modules.Identity.Users.DTOs.Request;
using Microsoft.AspNetCore.Mvc;

namespace Kairos.Api.Modules.Identity.Auth.Controller;

/// <summary>
/// Provides endpoints for authentication and user registration.
/// </summary>
[ApiController]
[Route("api/v{version:apiVersion}/auth")]
[ApiVersion("1.0")]
[Produces("application/json")]
public class AuthController(
    IInboxTransactionUseCase<CreateUserDTO, Tokens, IRegisterUserUseCase> registerUser
) : ControllerBase
{
    
    /// <summary>
    /// Registers a new user and returns authentication tokens.
    /// </summary>
    /// <remarks>
    /// The request requires an X-Inbox-Id header to identify the
    /// incoming message. The operation executes through the Inbox
    /// transaction service to coordinate message processing and
    /// user registration.
    ///
    /// The response includes a trace identifier for request tracking.
    /// </remarks>
    /// <param name="dto">
    /// User registration information, including username, email
    /// and password.
    /// </param>
    /// <param name="inboxId">
    /// Identifier of the Inbox message supplied through X-Inbox-Id.
    /// </param>
    /// <param name="ct">Cancellation token for the HTTP request.</param>
    /// <response code="201">
    /// The user was successfully registered and authentication
    /// tokens were generated.
    /// </response>
    /// <response code="400">
    /// The registration request contains invalid data.
    /// </response>
    /// <response code="409">
    /// The username or email is already registered, or the Inbox
    /// message is unavailable for processing.
    /// </response>
    /// <response code="500">
    /// An internal error prevented user registration.
    /// </response>
    [HttpPost("Register")]
    [ProducesResponseType(typeof(ResponseHttp<Tokens>), (int)HttpStatusCode.Created)]
    [ProducesResponseType(typeof(ResponseHttp<Tokens>), (int)HttpStatusCode.BadRequest)]
    [ProducesResponseType(typeof(ResponseHttp<Tokens>), (int)HttpStatusCode.Conflict)]
    [ProducesResponseType(typeof(ResponseHttp<Tokens>), (int)HttpStatusCode.InternalServerError)]
    public async Task<IActionResult> Create(
        [FromBody] CreateUserDTO dto,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        string workerId = $"http-{Guid.NewGuid():N}";

        Result<Tokens> result = await registerUser.ExecuteAsync(inboxId, workerId, dto, ct);

        if (result.IsFailure)
        {
            return StatusCode(result.StatusCode, new ResponseHttp<Tokens>(
                    Data: null,
                    Message: "Error creating user",
                    TraceId: HttpContext.TraceIdentifier,
                    Success: false,
                    Timestamp: DateTimeOffset.UtcNow,
                    Errors: result.Errors)
            );
        }

        return StatusCode(StatusCodes.Status201Created, new ResponseHttp<Tokens>(
                Data: result.Value,
                Message: "User created",
                TraceId: HttpContext.TraceIdentifier,
                Success: true,
                Timestamp: DateTimeOffset.UtcNow)
        );
    }
    
}