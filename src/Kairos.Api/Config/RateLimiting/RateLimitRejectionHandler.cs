using System.Globalization;
using System.Threading.RateLimiting;
using Kairos.Api.Common.Res;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Net.Http.Headers;

namespace Kairos.Api.Config.RateLimiting;

public static class RateLimitRejectionHandler
{
    public static async ValueTask HandleAsync(
        OnRejectedContext context,
        CancellationToken ct)
    {
        HttpContext http = context.HttpContext;

        int? retryAfterSeconds = null;

        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan retryAfter))
        {
            retryAfterSeconds = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds));

            http.Response.Headers[HeaderNames.RetryAfter] =
                retryAfterSeconds.Value.ToString(CultureInfo.InvariantCulture);
        }

        string policy =
            http.GetEndpoint()?.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName
            ?? "global";

        http.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("Kairos.Api.RateLimiting")
            .LogWarning(
                "Rate limit exceeded. Policy={Policy} Method={Method} Path={Path} Client={Client}",
                policy,
                http.Request.Method,
                http.Request.Path.Value,
                RateLimitPartitionKeys.UserOrIp(http));

        http.Response.StatusCode = StatusCodes.Status429TooManyRequests;

        ResponseHttp<object?> body = new(
            Data: null,
            Message: "Too many requests.",
            TraceId: http.TraceIdentifier,
            Success: false,
            Timestamp: DateTimeOffset.UtcNow,
            DetailsError: retryAfterSeconds is null
                ? "Rate limit exceeded."
                : $"Rate limit exceeded. Retry in {retryAfterSeconds} seconds.",
            Path: http.Request.Path.ToString(),
            ApiVersion: 1,
            Errors: ["Too many requests. Try again later."]);

        await http.Response.WriteAsJsonAsync(body, ct);
    }
}