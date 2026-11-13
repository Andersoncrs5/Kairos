using Kairos.Api.Common.Exceptions;
using Kairos.Api.Config.Middleware.Exceptions.Abstractions;

namespace Kairos.Api.Config.Middleware.Exceptions.Handlers;

public sealed class UserNotAuthenticatedExceptionHandler(
    ExceptionResponseWriter writer,
    ILogger<UserNotAuthenticatedExceptionHandler> logger)
    : ApiExceptionHandler<UserNotAuthenticatedException>(writer, logger)
{
    protected override ValueTask<bool> HandleAsync(
        HttpContext context,
        UserNotAuthenticatedException exception,
        CancellationToken ct)
    {
        return WriteResponseAsync(
            context,
            exception,
            exception.StatusCode,
            exception.Text,
            detailsError: exception.ErrorCode,
            ct: ct);
    }
}