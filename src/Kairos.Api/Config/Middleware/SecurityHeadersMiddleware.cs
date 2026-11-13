using Microsoft.Net.Http.Headers;

namespace Kairos.Api.Config.Middleware;

public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            IHeaderDictionary headers = context.Response.Headers;

            headers[HeaderNames.XContentTypeOptions] = "nosniff";
            headers[HeaderNames.XFrameOptions] = "DENY";
            headers["Referrer-Policy"] = "no-referrer";
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";

            // CSP restritivo só para a API. O Swagger UI (HTML) precisa de scripts inline.
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                headers[HeaderNames.ContentSecurityPolicy] =
                    "default-src 'none'; frame-ancestors 'none'";
            }

            return Task.CompletedTask;
        });

        return next(context);
    }
}