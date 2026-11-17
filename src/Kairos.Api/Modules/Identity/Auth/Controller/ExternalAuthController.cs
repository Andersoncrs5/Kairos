using System.Security.Claims;
using Asp.Versioning;
using Kairos.Api.Common.Res;
using Kairos.Api.Common.Result;
using Kairos.Api.Config.Authentication;
using Kairos.Api.Config.DB.Transaction;
using Kairos.Api.Modules.Identity.Auth.DTOs.Commands;
using Kairos.Api.Modules.Identity.Auth.DTOs.Response;
using Kairos.Api.Modules.Identity.Auth.Services.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kairos.Api.Modules.Identity.Auth.Controller;

/// <summary>
/// Browser-based social login (Google / GitHub). These routes live outside
/// /api on purpose: a provider redirect cannot send the X-Inbox-Id header
/// that RequireInboxIdMiddleware demands on every /api request.
/// </summary>
[ApiController]
[ApiVersionNeutral]
[ApiExplorerSettings(IgnoreApi = true)]
[AllowAnonymous]
[Route("auth/external")]
public class ExternalAuthController(
    IAuthenticationSchemeProvider schemeProvider,
    ITransactionUseCase<ExternalLoginCommand, Tokens, IExternalLoginUseCase>
        externalLogin)
    : ControllerBase
{
    private const string ProviderItemKey = "kairos.provider";
    private const string CallbackPath = "/auth/external/callback";

    private static readonly Dictionary<string, string> Providers =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["google"] = ExternalAuthenticationConfig.GoogleScheme,
            ["github"] = ExternalAuthenticationConfig.GitHubScheme,
            ["discord"] = ExternalAuthenticationConfig.DiscordScheme,
            ["facebook"] = ExternalAuthenticationConfig.FacebookScheme,
            ["linkedin"] = ExternalAuthenticationConfig.LinkedInScheme,
            ["microsoft"] = ExternalAuthenticationConfig.MicrosoftScheme
        };

    /// <summary>Starts the login: redirects the browser to the provider.</summary>
    [HttpGet("{provider}")]
    public async Task<IActionResult> StartExternalLogin(string provider)
    {
        if (!Providers.TryGetValue(provider, out string? scheme) ||
            await schemeProvider.GetSchemeAsync(scheme) is null)
        {
            return Fail(
                StatusCodes.Status404NotFound,
                "Provider not available",
                $"Provider '{provider}' is not configured.");
        }

        AuthenticationProperties properties = new()
        {
            RedirectUri = CallbackPath
        };

        // Remember which provider started the flow; the callback trusts
        // this value (it is protected inside the OAuth state).
        properties.Items[ProviderItemKey] = scheme;

        return Challenge(properties, scheme);
    }

    /// <summary>Finishes the login and returns the Kairos tokens.</summary>
    [HttpGet("callback")]
    public async Task<IActionResult> Callback(CancellationToken ct)
    {
        AuthenticateResult authentication =
            await HttpContext.AuthenticateAsync(
                ExternalAuthenticationConfig.ExternalScheme);

        // The temporary cookie has done its job.
        await HttpContext.SignOutAsync(
            ExternalAuthenticationConfig.ExternalScheme);

        // This response carries tokens: never cache it.
        Response.Headers.CacheControl = "no-store";

        if (!authentication.Succeeded ||
            authentication.Principal is null ||
            authentication.Properties is null ||
            !authentication.Properties.Items.TryGetValue(
                ProviderItemKey, out string? provider) ||
            string.IsNullOrWhiteSpace(provider))
        {
            return Fail(
                StatusCodes.Status401Unauthorized,
                "External login failed",
                "The external sign-in session is missing or expired. " +
                "Start again.");
        }

        ClaimsPrincipal principal = authentication.Principal;

        string? providerKey =
            principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrWhiteSpace(providerKey))
        {
            return Fail(
                StatusCodes.Status401Unauthorized,
                "External login failed",
                "The provider did not return an account identifier.");
        }

        string? email = principal.FindFirst(ClaimTypes.Email)?.Value;

        bool emailVerified = string.Equals(
            principal.FindFirst(
                ExternalAuthenticationConfig.EmailVerifiedClaim)?.Value,
            "true",
            StringComparison.OrdinalIgnoreCase);

        string? fullName =
            principal.FindFirst(ClaimTypes.Name)?.Value
            ?? principal.FindFirst(
                ExternalAuthenticationConfig.UsernameClaim)?.Value;

        ExternalLoginCommand command = new(
            provider,
            providerKey,
            email,
            emailVerified,
            fullName);

        Result<Tokens> result = await externalLogin.ExecuteAsync(command, ct);

        if (result.IsFailure)
        {
            return StatusCode(
                result.StatusCode,
                new ResponseHttp<Tokens>(
                    Data: null,
                    Message: "External login failed",
                    TraceId: HttpContext.TraceIdentifier,
                    Success: false,
                    Timestamp: DateTimeOffset.UtcNow,
                    Errors: result.Errors));
        }

        return StatusCode(
            StatusCodes.Status200OK,
            new ResponseHttp<Tokens>(
                Data: result.Value,
                Message: "Welcome",
                TraceId: HttpContext.TraceIdentifier,
                Success: true,
                Timestamp: DateTimeOffset.UtcNow));
    }

    private ObjectResult Fail(int statusCode, string message, string error)
    {
        return StatusCode(
            statusCode,
            new ResponseHttp<object?>(
                Data: null,
                Message: message,
                TraceId: HttpContext.TraceIdentifier,
                Success: false,
                Timestamp: DateTimeOffset.UtcNow,
                Errors: [error]));
    }
}