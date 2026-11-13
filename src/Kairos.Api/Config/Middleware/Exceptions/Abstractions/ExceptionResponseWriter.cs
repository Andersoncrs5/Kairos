using System.Collections.Immutable;
using System.Diagnostics;
using Kairos.Api.Common.Res;

namespace Kairos.Api.Config.Middleware.Exceptions.Abstractions;

public sealed class ExceptionResponseWriter
{
    public async Task WriteAsync(
        HttpContext context,
        int statusCode,
        string message,
        string? detailsError = null,
        IReadOnlyList<string>? errors = null,
        IReadOnlyDictionary<string, string[]>? errorValids = null,
        CancellationToken ct = default)
    {
        string inboxId = context.Request.Headers["X-Inbox-Id"].ToString();

        string traceId = !string.IsNullOrWhiteSpace(inboxId)
            ? inboxId
            : Activity.Current?.Id ?? context.TraceIdentifier;

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        ResponseHttp<object> response = new(
            Data: null,
            Message: message,
            TraceId: traceId,
            Success: false,
            Timestamp: DateTimeOffset.UtcNow,
            DetailsError: detailsError,
            Path: context.Request.Path,
            ApiVersion: 1,
            Errors: errors,
            ErrorValids: errorValids.ToImmutableDictionary());

        await context.Response.WriteAsJsonAsync(
            response,
            cancellationToken: ct);
    }
}