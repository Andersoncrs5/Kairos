using Kairos.Api.Common.Exceptions;
using Kairos.Api.Config.Middleware.Exceptions.Abstractions;

namespace Kairos.Api.Config.Middleware.Exceptions.Handlers;

public sealed class InternalServerExceptionHandler(
    ExceptionResponseWriter writer,
    ILogger<InternalServerExceptionHandler> logger)
    : ApiExceptionHandler<InternalServerException>(writer, logger)
{
    protected override ValueTask<bool> HandleAsync(
        HttpContext context,
        InternalServerException exception,
        CancellationToken ct)
    {
        Logger.LogError(
            exception,
            "Internal server exception. Code: {ErrorCode}. Metadata: {@Metadata}",
            exception.ErrorCode,
            exception.Metadata);

        return WriteResponseAsync(
            context,
            exception,
            exception.StatusCode,
            "An unexpected internal server error occurred.",
            detailsError: exception.ErrorCode,
            ct: ct);
    }
}