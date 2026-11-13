using Kairos.Api.Common.Exceptions;
using Kairos.Api.Config.Middleware.Exceptions.Abstractions;

namespace Kairos.Api.Config.Middleware.Exceptions.Handlers;

public sealed class UnauthenticatedExceptionHandler(
    ExceptionResponseWriter writer,
    ILogger<UnauthenticatedExceptionHandler> logger)
    : ApiExceptionHandler<UnauthenticatedException>(writer, logger)
{
    protected override ValueTask<bool> HandleAsync(
        HttpContext context,
        UnauthenticatedException exception,
        CancellationToken ct)
    {
        return WriteResponseAsync(
            context,
            exception,
            StatusCodes.Status401Unauthorized,
            exception.Message,
            ct: ct);
    }
}