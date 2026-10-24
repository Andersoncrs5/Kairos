using Kairos.Api.Common.Res;
using Microsoft.AspNetCore.Mvc;

namespace Kairos.Api.Common.Middleware;

public sealed class RequireInboxIdMiddleware(RequestDelegate next)
{
    private const string HeaderName = "X-Inbox-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        if (HttpMethods.IsOptions(context.Request.Method))
        {
            await next(context);
            return;
        }

        if (context.Request.Path.StartsWithSegments("/api") &&
            string.IsNullOrWhiteSpace(context.Request.Headers[HeaderName]))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;

            ResponseHttp<object?> response = new ResponseHttp<object?>(
                Data: null,
                Message: "The header required was not informado.",
                TraceId: context.TraceIdentifier,
                Success: false,
                Timestamp: DateTimeOffset.UtcNow,
                DetailsError: $"The header '{HeaderName}' is required.",
                Path: context.Request.Path.ToString(),
                ApiVersion: 1,
                Errors: [$"Informe o header '{HeaderName}'."]);

            await context.Response.WriteAsJsonAsync(response, context.RequestAborted);

            return;
        }

        await next(context);
    }
}