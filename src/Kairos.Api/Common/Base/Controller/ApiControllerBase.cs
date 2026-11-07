using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Kairos.Api.Common.Exceptions;
using Kairos.Api.Common.Res;
using Kairos.Api.Common.Result;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;

namespace Kairos.Api.Common.Base.Controller;


[ApiController]
[Produces("application/json")]
[ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status400BadRequest)]
[ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status403Forbidden)]
[ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status409Conflict)]
[ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status500InternalServerError)]
public abstract class ApiControllerBase : ControllerBase
{
    // ---------------------------------------------------------------
    // Worker ID used by the Inbox transaction
    // ---------------------------------------------------------------

    protected static string NewWorkerId() => $"http-{Guid.NewGuid():N}";

    // ---------------------------------------------------------------
    // Responses
    // ---------------------------------------------------------------

    protected ObjectResult SuccessResponse<TData>(
        TData? data,
        string message,
        string inboxId,
        int statusCode = StatusCodes.Status200OK)
    {
        return StatusCode(
            statusCode,
            new ResponseHttp<TData>(
                Data: data,
                Message: message,
                TraceId: inboxId,
                Success: true,
                Timestamp: DateTimeOffset.UtcNow));
    }

    protected ObjectResult FailureResponse<TData>(
        int statusCode,
        IReadOnlyList<string> errors,
        string message,
        string inboxId)
    {
        return StatusCode(
            statusCode,
            new ResponseHttp<TData>(
                Data: default,
                Message: message,
                TraceId: inboxId,
                Success: false,
                Timestamp: DateTimeOffset.UtcNow,
                Errors: errors));
    }

    /// <summary>
    /// Converts a use case Result into the standard HTTP response:
    /// failure (or null value) -> error response with the Result status code;
    /// success -> the mapped value with the given success status code.
    /// </summary>
    protected ObjectResult ToResponse<TValue, TData>(
        Result<TValue> result,
        Func<TValue, TData> map,
        string successMessage,
        string errorMessage,
        string inboxId,
        int successStatusCode = StatusCodes.Status200OK)
    {
        if (result.IsFailure || result.Value is null)
        {
            return FailureResponse<TData>(
                result.StatusCode,
                result.Errors,
                errorMessage,
                inboxId);
        }

        return SuccessResponse(
            map(result.Value),
            successMessage,
            inboxId,
            successStatusCode);
    }

    // ---------------------------------------------------------------
    // Authenticated user
    // ---------------------------------------------------------------

    protected string GetAuthenticatedUserId(string? inboxId = null)
    {
        string? userId =
            User.FindFirstValue(JwtRegisteredClaimNames.Sid);

        if (string.IsNullOrWhiteSpace(userId))
        {
            userId = User.FindFirstValue("sub");
        }

        if (!string.IsNullOrWhiteSpace(userId))
        {
            return userId.Trim();
        }

        throw new UserNotAuthenticatedException(
            text: "The authenticated user ID could not be extracted from the JWT.",
            inboxId: inboxId ?? Request.Headers["X-Inbox-Id"].ToString(),
            traceId: HttpContext.TraceIdentifier,
            path: Request.Path.Value,
            httpMethod: Request.Method,
            endpoint: HttpContext.GetEndpoint()?.DisplayName,
            authenticationScheme: JwtBearerDefaults.AuthenticationScheme,
            requiredClaims:
            [
                JwtRegisteredClaimNames.Sid,
                "sub"
            ],
            metadata: new Dictionary<string, string?>
            {
                ["Controller"] = RouteData.Values["controller"]?.ToString(),
                ["Action"] = RouteData.Values["action"]?.ToString(),
                ["RemoteIpAddress"] =
                    HttpContext.Connection.RemoteIpAddress?.ToString()
            });
    }
}