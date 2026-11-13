using Kairos.Api.Common.Exceptions;
using Kairos.Api.Config.Middleware.Exceptions.Abstractions;

namespace Kairos.Api.Config.Middleware.Exceptions.Handlers;

public sealed class ApplicationBootstrapperExceptionHandler(
    ExceptionResponseWriter writer,
    ILogger<ApplicationBootstrapperExceptionHandler> logger)
    : ApiExceptionHandler<ApplicationBootstrapperException>(writer, logger)
{
    protected override ValueTask<bool> HandleAsync(
        HttpContext context,
        ApplicationBootstrapperException exception,
        CancellationToken ct)
    {
        Logger.LogError(
            exception,
            """
            Application bootstrap failed.
            Seed: {SeedName}
            Stage: {Stage}
            Started: {StartedAt}
            Failed: {FailedAt}
            Elapsed: {Elapsed}
            Original exception: {OriginalExceptionType}
            """,
            exception.SeedName,
            exception.Stage,
            exception.BootstrapStartedAt,
            exception.FailedAt,
            exception.Elapsed,
            exception.OriginalExceptionType);

        return WriteResponseAsync(
            context,
            exception,
            StatusCodes.Status500InternalServerError,
            "Application initialization failed.",
            detailsError: "APPLICATION_BOOTSTRAP_FAILED",
            ct: ct);
    }
}