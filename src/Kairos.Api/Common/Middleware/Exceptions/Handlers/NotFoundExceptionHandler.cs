using Kairos.Api.Common.Exceptions;
using Kairos.Api.Common.Middleware.Exceptions.Abstractions;

namespace Kairos.Api.Common.Middleware.Exceptions.Handlers;

public sealed class NotFoundExceptionHandler(
    ExceptionResponseWriter writer,
    ILogger<NotFoundExceptionHandler> logger)
    : ApiExceptionHandler<NotFoundException>(writer, logger)
{
    protected override ValueTask<bool> HandleAsync(
        HttpContext context,
        NotFoundException exception,
        CancellationToken ct)
    {
        return WriteResponseAsync(
            context,
            exception,
            StatusCodes.Status404NotFound,
            exception.Message,
            ct: ct);
    }
}