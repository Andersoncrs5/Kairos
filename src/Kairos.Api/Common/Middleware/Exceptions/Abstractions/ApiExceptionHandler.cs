using Microsoft.AspNetCore.Diagnostics;

namespace Kairos.Api.Common.Middleware.Exceptions.Abstractions;

public abstract class ApiExceptionHandler<TException>(
    ExceptionResponseWriter responseWriter,
    ILogger logger) : IExceptionHandler
    where TException : Exception
{
    protected ILogger Logger { get; } = logger;

    private readonly ExceptionResponseWriter _responseWriter =
        responseWriter;

    public ValueTask<bool> TryHandleAsync(
        HttpContext context,
        Exception exception,
        CancellationToken ct)
    {
        if (exception is not TException typedException)
            return ValueTask.FromResult(false);

        return HandleAsync(context, typedException, ct);
    }

    protected abstract ValueTask<bool> HandleAsync(
        HttpContext context,
        TException exception,
        CancellationToken ct);

    protected async ValueTask<bool> WriteResponseAsync(
        HttpContext context,
        Exception exception,
        int statusCode,
        string message,
        string? detailsError = null,
        IReadOnlyList<string>? errors = null,
        IReadOnlyDictionary<string, string[]>? errorValids = null,
        CancellationToken ct = default)
    {
        if (context.Response.HasStarted)
        {
            Logger.LogError(
                exception,
                "Cannot write exception response because the HTTP response has already started.");

            return false;
        }

        if (statusCode >= 500)
        {
            Logger.LogError(
                exception,
                "Unhandled server exception: {ExceptionType}",
                exception.GetType().Name);
        }
        else
        {
            Logger.LogWarning(
                "Handled exception: {ExceptionType}. Message: {Message}",
                exception.GetType().Name,
                message);
        }

        await _responseWriter.WriteAsync(
            context,
            statusCode,
            message,
            detailsError,
            errors,
            errorValids,
            ct);

        return true;
    }
}