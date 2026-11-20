using System.ComponentModel.DataAnnotations;
using Asp.Versioning;
using Kairos.Api.Common.Res;
using Kairos.Api.Common.Result;
using Kairos.Api.Config.DB.Transaction;
using Kairos.Api.Config.RateLimiting;
using Kairos.Api.Modules.Identity.Auth.DTOs.Response;
using Kairos.Api.Modules.Identity.Passkeys.DTOs.Commands;
using Kairos.Api.Modules.Identity.Passkeys.DTOs.Request;
using Kairos.Api.Modules.Identity.Passkeys.DTOs.Response;
using Kairos.Api.Modules.Identity.Passkeys.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Kairos.Api.Modules.Identity.Passkeys.Controller;

/// <summary>
/// Passkeys (WebAuthn): passwordless, phishing-resistant sign-in.
/// </summary>
[ApiController]
[Route("api/v{version:apiVersion}/auth/passkeys")]
[ApiVersion("1.0")]
[Produces("application/json")]
public class PasskeysController(
    ICreatePasskeyRegistrationOptionsUseCase registrationOptions,
    IInboxTransactionUseCase<CompletePasskeyRegistrationCommand, PasskeyDTO, ICompletePasskeyRegistrationUseCase> completeRegistration,
    ICreatePasskeyLoginOptionsUseCase loginOptions,
    IInboxTransactionUseCase<CompletePasskeyLoginCommand, Tokens, ICompletePasskeyLoginUseCase> completeLogin,
    IListPasskeysUseCase listPasskeys,
    IInboxTransactionUseCase<DeletePasskeyCommand, bool, IDeletePasskeyUseCase> deletePasskey)
    : ControllerBase
{
    /// <summary>Step 1 of registering a passkey (authenticated).</summary>
    [HttpPost("register/options")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [ProducesResponseType(typeof(ResponseHttp<PasskeyOptionsResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> RegisterOptions(CancellationToken ct)
    {
        string? userId = CurrentUserId();

        if (string.IsNullOrWhiteSpace(userId))
            return Unauthenticated();

        Result<PasskeyOptionsResponse> result =
            await registrationOptions.ExecuteAsync(userId, ct);

        return Respond(
            result,
            "Passkey registration options created",
            "Could not start the passkey registration");
    }

    /// <summary>Step 2 of registering a passkey (authenticated).</summary>
    [HttpPost("register/complete")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [ProducesResponseType(typeof(ResponseHttp<PasskeyDTO>), StatusCodes.Status201Created)]
    public async Task<IActionResult> RegisterComplete(
        [FromBody] CompletePasskeyRegistrationDto dto,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        string? userId = CurrentUserId();

        if (string.IsNullOrWhiteSpace(userId))
            return Unauthenticated();

        string workerId = $"http-{Guid.NewGuid():N}";

        CompletePasskeyRegistrationCommand command = new(
            userId,
            dto.FlowId,
            dto.Name,
            dto.Credential);

        Result<PasskeyDTO> result =
            await completeRegistration.ExecuteAsync(inboxId, workerId, command, ct);

        return Respond(
            result,
            "Passkey registered",
            "Could not register the passkey",
            StatusCodes.Status201Created);
    }

    /// <summary>Step 1 of signing in with a passkey (anonymous).</summary>
    [HttpPost("login/options")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.AuthLogin)]
    [ProducesResponseType(typeof(ResponseHttp<PasskeyOptionsResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> LoginOptions(CancellationToken ct)
    {
        Result<PasskeyOptionsResponse> result = await loginOptions.ExecuteAsync(ct);

        return Respond(
            result,
            "Passkey login options created",
            "Could not start the passkey login");
    }

    /// <summary>Step 2 of signing in with a passkey (anonymous). Returns the same tokens as Login.</summary>
    [HttpPost("login/complete")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.AuthLogin)]
    [ProducesResponseType(typeof(ResponseHttp<Tokens>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseHttp<Tokens>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> LoginComplete(
        [FromBody] CompletePasskeyLoginDto dto,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        string workerId = $"http-{Guid.NewGuid():N}";

        CompletePasskeyLoginCommand command = new(dto.FlowId, dto.Credential);

        Result<Tokens> result =
            await completeLogin.ExecuteAsync(inboxId, workerId, command, ct);

        return Respond(result, "Welcome", "Passkey login failed");
    }

    /// <summary>Lists the passkeys of the authenticated user.</summary>
    [HttpGet]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [ProducesResponseType(typeof(ResponseHttp<IReadOnlyList<PasskeyDTO>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        string? userId = CurrentUserId();

        if (string.IsNullOrWhiteSpace(userId))
            return Unauthenticated();

        Result<IReadOnlyList<PasskeyDTO>> result =
            await listPasskeys.ExecuteAsync(userId, ct);

        return Respond(result, "Passkeys found", "Could not list the passkeys");
    }

    /// <summary>Removes one passkey of the authenticated user.</summary>
    [HttpDelete("{id}")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [ProducesResponseType(typeof(ResponseHttp<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseHttp<bool>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(
        string id,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        string? userId = CurrentUserId();

        if (string.IsNullOrWhiteSpace(userId))
            return Unauthenticated();

        string workerId = $"http-{Guid.NewGuid():N}";

        DeletePasskeyCommand command = new(userId, id);

        Result<bool> result =
            await deletePasskey.ExecuteAsync(inboxId, workerId, command, ct);

        return Respond(result, "Passkey removed", "Could not remove the passkey");
    }

    private string? CurrentUserId() =>
        User.FindFirst(JwtRegisteredClaimNames.Sid)?.Value;

    private IActionResult Unauthenticated()
    {
        return Unauthorized(new ResponseHttp<object?>(
            Data: null,
            Message: "User is not authenticated",
            TraceId: HttpContext.TraceIdentifier,
            Success: false,
            Timestamp: DateTimeOffset.UtcNow,
            Errors: ["Invalid token claims"]));
    }

    private IActionResult Respond<T>(
        Result<T> result,
        string successMessage,
        string failureMessage,
        int successStatus = StatusCodes.Status200OK)
    {
        if (result.IsFailure)
        {
            return StatusCode(
                result.StatusCode,
                new ResponseHttp<T>(
                    Data: default,
                    Message: failureMessage,
                    TraceId: HttpContext.TraceIdentifier,
                    Success: false,
                    Timestamp: DateTimeOffset.UtcNow,
                    Errors: result.Errors));
        }

        return StatusCode(
            successStatus,
            new ResponseHttp<T>(
                Data: result.Value,
                Message: successMessage,
                TraceId: HttpContext.TraceIdentifier,
                Success: true,
                Timestamp: DateTimeOffset.UtcNow));
    }
}