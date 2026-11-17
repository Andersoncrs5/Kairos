using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using Kairos.Api.Common.Res;
using Kairos.Api.Config.Contracts;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OAuth;

namespace Kairos.Api.Config.Authentication;

public static class ExternalAuthenticationConfig
{
    public const string ExternalScheme = "External";
    public const string GoogleScheme = "Google";
    public const string GitHubScheme = "GitHub";
    public const string DiscordScheme = "Discord";
    public const string FacebookScheme = "Facebook";
    public const string LinkedInScheme = "LinkedIn";
    public const string MicrosoftScheme = "Microsoft";

    public const string EmailVerifiedClaim = "email_verified";
    public const string UsernameClaim = "urn:kairos:username";

    public static IServiceCollection AddExternalAuthenticationConfig(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ExternalAuthenticationOptions options =
            configuration
                .GetSection(ExternalAuthenticationOptions.SectionName)
                .Get<ExternalAuthenticationOptions>()
            ?? new ExternalAuthenticationOptions();

        EnsureComplete("Google", options.Google);
        EnsureComplete("GitHub", options.GitHub);
        EnsureComplete("Discord", options.Discord);
        EnsureComplete("Facebook", options.Facebook);
        EnsureComplete("LinkedIn", options.LinkedIn);
        EnsureComplete("Microsoft", options.Microsoft);

        AuthenticationBuilder authentication = services.AddAuthentication();

        // Short-lived cookie that only carries the provider's identity
        // from the OAuth handler to our callback endpoint.
        authentication.AddCookie(ExternalScheme, cookie =>
        {
            cookie.Cookie.Name = "kairos.external";
            cookie.Cookie.HttpOnly = true;
            cookie.Cookie.SameSite = SameSiteMode.Lax;
            cookie.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            cookie.ExpireTimeSpan = TimeSpan.FromMinutes(5);
        });

        // A provider is only registered when it has credentials: an OAuth
        // handler with an empty ClientId throws on every request.
        if (options.Google.IsConfigured)
            AddGoogleProvider(authentication, options.Google);

        if (options.GitHub.IsConfigured)
            AddGitHubProvider(authentication, options.GitHub);

        if (options.Discord.IsConfigured)
            AddDiscordProvider(authentication, options.Discord);

        if (options.Facebook.IsConfigured)
            AddFacebookProvider(authentication, options.Facebook);

        if (options.LinkedIn.IsConfigured)
            AddLinkedInProvider(authentication, options.LinkedIn);

        if (options.Microsoft.IsConfigured)
            AddMicrosoftProvider(authentication, options.Microsoft);

        return services;
    }

    // ------------------------------------------------------------------
    // Providers
    // ------------------------------------------------------------------

    private static void AddGoogleProvider(
        AuthenticationBuilder authentication,
        ExternalProviderOptions provider)
    {
        authentication.AddGoogle(GoogleScheme, google =>
        {
            ConfigureCommon(google, provider);

            google.ClaimActions.MapJsonKey(
                EmailVerifiedClaim, "email_verified");
        });
    }

    private static void AddGitHubProvider(
        AuthenticationBuilder authentication,
        ExternalProviderOptions provider)
    {
        authentication.AddOAuth(GitHubScheme, github =>
        {
            ConfigureCommon(github, provider);

            github.CallbackPath = "/signin-github";
            github.AuthorizationEndpoint =
                "https://github.com/login/oauth/authorize";
            github.TokenEndpoint =
                "https://github.com/login/oauth/access_token";
            github.UserInformationEndpoint = "https://api.github.com/user";

            github.Scope.Add("read:user");
            github.Scope.Add("user:email");

            github.ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "id");
            github.ClaimActions.MapJsonKey(ClaimTypes.Name, "name");
            github.ClaimActions.MapJsonKey(UsernameClaim, "login");
            github.ClaimActions.MapJsonKey(ClaimTypes.Email, "email");

            github.Events.OnCreatingTicket = LoadGitHubVerifiedEmailAsync;
        });
    }

    private static void AddDiscordProvider(
        AuthenticationBuilder authentication,
        ExternalProviderOptions provider)
    {
        authentication.AddOAuth(DiscordScheme, discord =>
        {
            ConfigureCommon(discord, provider);

            discord.CallbackPath = "/signin-discord";
            discord.AuthorizationEndpoint =
                "https://discord.com/oauth2/authorize";
            discord.TokenEndpoint = "https://discord.com/api/oauth2/token";
            discord.UserInformationEndpoint =
                "https://discord.com/api/users/@me";

            discord.Scope.Add("identify");
            discord.Scope.Add("email");

            discord.ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "id");
            discord.ClaimActions.MapJsonKey(ClaimTypes.Name, "global_name");
            discord.ClaimActions.MapJsonKey(UsernameClaim, "username");
            discord.ClaimActions.MapJsonKey(ClaimTypes.Email, "email");

            // Discord says whether the account's email was verified.
            discord.ClaimActions.MapCustomJson(
                EmailVerifiedClaim,
                user => ReadBool(user, "verified") ? "true" : null);
        });
    }

    private static void AddFacebookProvider(
        AuthenticationBuilder authentication,
        FacebookProviderOptions provider)
    {
        authentication.AddFacebook(FacebookScheme, facebook =>
        {
            ConfigureCommon(facebook, provider);

            // Facebook has no "email verified" flag. Without TrustEmail no
            // verified claim is emitted, so a first-time Facebook login
            // cannot create or link an account.
            if (provider.TrustEmail)
            {
                facebook.ClaimActions.MapCustomJson(
                    EmailVerifiedClaim,
                    user => ReadString(user, "email") is not null
                        ? "true"
                        : null);
            }
        });
    }

    private static void AddLinkedInProvider(
        AuthenticationBuilder authentication,
        ExternalProviderOptions provider)
    {
        authentication.AddOAuth(LinkedInScheme, linkedIn =>
        {
            ConfigureCommon(linkedIn, provider);

            // "Sign In with LinkedIn using OpenID Connect"
            linkedIn.CallbackPath = "/signin-linkedin";
            linkedIn.AuthorizationEndpoint =
                "https://www.linkedin.com/oauth/v2/authorization";
            linkedIn.TokenEndpoint =
                "https://www.linkedin.com/oauth/v2/accessToken";
            linkedIn.UserInformationEndpoint =
                "https://api.linkedin.com/v2/userinfo";

            linkedIn.Scope.Add("openid");
            linkedIn.Scope.Add("profile");
            linkedIn.Scope.Add("email");

            linkedIn.ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "sub");
            linkedIn.ClaimActions.MapJsonKey(ClaimTypes.Name, "name");
            linkedIn.ClaimActions.MapJsonKey(ClaimTypes.Email, "email");

            // email / email_verified are optional in LinkedIn's response.
            linkedIn.ClaimActions.MapCustomJson(
                EmailVerifiedClaim,
                user => ReadBool(user, "email_verified") ? "true" : null);
        });
    }

    private static void AddMicrosoftProvider(
        AuthenticationBuilder authentication,
        MicrosoftProviderOptions provider)
    {
        string tenant = string.IsNullOrWhiteSpace(provider.Tenant)
            ? "consumers"
            : provider.Tenant.Trim();

        // Entra ID can send emails nobody verified (the "nOAuth" attack).
        // Only personal accounts ("consumers") and a single tenant you
        // control are trusted; multi-tenant sign-in is never trusted.
        bool trustEmail =
            !tenant.Equals("common", StringComparison.OrdinalIgnoreCase) &&
            !tenant.Equals("organizations", StringComparison.OrdinalIgnoreCase);

        authentication.AddMicrosoftAccount(MicrosoftScheme, microsoft =>
        {
            ConfigureCommon(microsoft, provider);

            microsoft.AuthorizationEndpoint =
                $"https://login.microsoftonline.com/{tenant}/oauth2/v2.0/authorize";
            microsoft.TokenEndpoint =
                $"https://login.microsoftonline.com/{tenant}/oauth2/v2.0/token";

            if (trustEmail)
            {
                microsoft.ClaimActions.MapCustomJson(
                    EmailVerifiedClaim,
                    user =>
                        ReadString(user, "mail") is not null ||
                        ReadString(user, "userPrincipalName") is not null
                            ? "true"
                            : null);
            }
        });
    }

    // ------------------------------------------------------------------
    // Shared helpers
    // ------------------------------------------------------------------

    private static void ConfigureCommon(
        OAuthOptions options,
        ExternalProviderOptions provider)
    {
        options.SignInScheme = ExternalScheme;
        options.ClientId = provider.ClientId;
        options.ClientSecret = provider.ClientSecret;
        options.SaveTokens = false;

        // The provider returns with a top-level GET, so Lax is enough and
        // also works over plain http during local development.
        options.CorrelationCookie.SameSite = SameSiteMode.Lax;
        options.CorrelationCookie.SecurePolicy =
            CookieSecurePolicy.SameAsRequest;

        options.Events.OnRemoteFailure = HandleRemoteFailure;
    }

    private static void EnsureComplete(
        string provider,
        ExternalProviderOptions options)
    {
        bool hasId = !string.IsNullOrWhiteSpace(options.ClientId);
        bool hasSecret = !string.IsNullOrWhiteSpace(options.ClientSecret);

        if (hasId != hasSecret)
        {
            throw new InvalidOperationException(
                $"ExternalAuthentication:{provider} requires both " +
                "ClientId and ClientSecret.");
        }
    }

    private static string? ReadString(JsonElement user, string key)
    {
        return user.TryGetProperty(key, out JsonElement value) &&
               value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static bool ReadBool(JsonElement user, string key)
    {
        return user.TryGetProperty(key, out JsonElement value) &&
               value.ValueKind == JsonValueKind.True;
    }

    private static async Task LoadGitHubVerifiedEmailAsync(
        OAuthCreatingTicketContext context)
    {
        // The profile was already loaded and mapped by the OAuth handler.
        // The email on /user can be null or unverified, so only the primary
        // email that GitHub marks as verified is trusted.
        if (context.Identity is null)
            return;

        CancellationToken ct = context.HttpContext.RequestAborted;

        using HttpRequestMessage request = new(
            HttpMethod.Get,
            "https://api.github.com/user/emails");

        request.Headers.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", context.AccessToken);

        using HttpResponseMessage response =
            await context.Backchannel.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode)
            return;

        using JsonDocument emails = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(ct));

        if (emails.RootElement.ValueKind != JsonValueKind.Array)
            return;

        foreach (JsonElement item in emails.RootElement.EnumerateArray())
        {
            bool primary =
                item.TryGetProperty("primary", out JsonElement p) &&
                p.ValueKind == JsonValueKind.True;

            bool verified =
                item.TryGetProperty("verified", out JsonElement v) &&
                v.ValueKind == JsonValueKind.True;

            if (!primary || !verified ||
                !item.TryGetProperty("email", out JsonElement e) ||
                e.GetString() is not { Length: > 0 } email)
            {
                continue;
            }

            foreach (Claim existing in
                     context.Identity.FindAll(ClaimTypes.Email).ToList())
            {
                context.Identity.RemoveClaim(existing);
            }

            context.Identity.AddClaim(new Claim(ClaimTypes.Email, email));
            context.Identity.AddClaim(new Claim(EmailVerifiedClaim, "true"));
            break;
        }
    }

    private static async Task HandleRemoteFailure(
        RemoteFailureContext context)
    {
        // Typical case: the user clicked "Cancel" on the consent screen.
        context.HandleResponse();

        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.Headers.CacheControl = "no-store";

        await context.Response.WriteAsJsonAsync(
            new ResponseHttp<object?>(
                Data: null,
                Message: "External sign-in failed",
                TraceId: context.HttpContext.TraceIdentifier,
                Success: false,
                Timestamp: DateTimeOffset.UtcNow,
                Errors:
                [
                    "The external provider denied or could not complete " +
                    "the sign-in."
                ]),
            context.HttpContext.RequestAborted);
    }
}