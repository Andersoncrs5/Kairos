using Kairos.Api.Common.Middleware.Exceptions.Abstractions;

namespace Kairos.Api.Common.Middleware.Exceptions.Handlers;

public sealed class UnhandledExceptionHandler(
    ExceptionResponseWriter writer,
    ILogger<UnhandledExceptionHandler> logger)
    : ApiExceptionHandler<Exception>(writer, logger)
{
    protected override ValueTask<bool> HandleAsync(
        HttpContext context,
        Exception exception,
        CancellationToken ct)
    {
        return WriteResponseAsync(
            context,
            exception,
            StatusCodes.Status500InternalServerError,
            "An unexpected internal server error occurred.",
            detailsError: "INTERNAL_SERVER_ERROR",
            ct: ct);
    }
}