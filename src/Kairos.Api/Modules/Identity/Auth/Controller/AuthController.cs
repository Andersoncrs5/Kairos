using System.ComponentModel.DataAnnotations;
using System.Net;
using Asp.Versioning;
using Kairos.Api.Common.Res;
using Kairos.Api.Common.Result;
using Kairos.Api.Config.DB.Transaction;
using Kairos.Api.Modules.Identity.Auth.DTOs.Request;
using Kairos.Api.Modules.Identity.Auth.DTOs.Response;
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
    IInboxTransactionUseCase<CreateUserDTO, Tokens, IRegisterUserUseCase> registerUser,
    IInboxTransactionUseCase<LoginUserDTO, Tokens, ILoginUserUseCase> loginUser
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

     /// <summary>
    /// Authenticates an existing user and returns authentication tokens.
    /// </summary>
    /// <remarks>
    /// Authenticates the user using the submitted email and password.
    ///
    /// Invalid credentials return a generic authentication error to avoid
    /// disclosing whether the specified account exists.
    ///
    /// Repeated failed login attempts can temporarily lock the account.
    /// The request is processed through the Inbox transaction service,
    /// and the response includes a trace identifier for request tracking.
    /// </remarks>
    /// <param name="dto">
    /// Login credentials containing the user's email and password.
    /// </param>
    /// <param name="inboxId">
    /// Identifier of the Inbox message provided in the X-Inbox-Id header.
    /// </param>
    /// <param name="ct">
    /// Cancellation token for the HTTP request.
    /// </param>
    /// <response code="200">
    /// Authentication succeeded and access and refresh tokens were returned.
    /// </response>
    /// <response code="400">
    /// The request contains invalid login data.
    /// </response>
    /// <response code="401">
    /// Authentication failed because the credentials are invalid.
    /// </response>
    /// <response code="409">
    /// The Inbox message is unavailable for processing.
    /// </response>
    /// <response code="429">
    /// Authentication was rejected because the account is temporarily locked.
    /// </response>
    /// <response code="500">
    /// An internal error prevented authentication.
    /// </response>
    [HttpPost("Login")]
    [ProducesResponseType(typeof(ResponseHttp<Tokens>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseHttp<Tokens>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseHttp<Tokens>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseHttp<Tokens>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ResponseHttp<Tokens>), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ResponseHttp<Tokens>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Login(
        [FromBody] LoginUserDTO dto,
        [FromHeader(Name = "X-Inbox-Id"), Required]
        string inboxId,
        CancellationToken ct)
    {
        string workerId = $"http-{Guid.NewGuid():N}";

        Result<Tokens> result = await loginUser.ExecuteAsync(inboxId, workerId, dto, ct);

        if (result.IsFailure)
        {
            return StatusCode(result.StatusCode, new ResponseHttp<Tokens>(
                Data: null,
                Message: "Login failed",
                TraceId: HttpContext.TraceIdentifier,
                Success: false,
                Timestamp: DateTimeOffset.UtcNow,
                Errors: result.Errors)
            );
        }

        return StatusCode(StatusCodes.Status200OK, new ResponseHttp<Tokens>(
            Data: result.Value,
            Message: "Welcome again",
            TraceId: HttpContext.TraceIdentifier,
            Success: true,
            Timestamp: DateTimeOffset.UtcNow)
        );
    }
    
}