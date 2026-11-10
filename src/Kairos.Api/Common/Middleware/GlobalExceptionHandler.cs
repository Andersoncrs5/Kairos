// Common/Middleware/GlobalExceptionHandler.cs
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Kairos.Api.Common.Middleware;

public sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger,
    IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext context, Exception exception, CancellationToken ct)
    {
        var (status, title) = exception switch
        {
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict,
                "O registro foi alterado por outra pessoa. Recarregue e tente de novo."),

            DbUpdateException { InnerException: PostgresException { SqlState: "23503" } } =>
                (StatusCodes.Status409Conflict,
                    "Este registro possui dependências e não pode ser removido."),

            DbUpdateException { InnerException: PostgresException { SqlState: "23505" } } =>
                (StatusCodes.Status409Conflict,
                    "Já existe um registro com esses dados."),

            // OperationCanceledException => (StatusCodes.Status499ClientClosedRequest(), "Requisição cancelada."),

            _ => (StatusCodes.Status500InternalServerError,
                "Erro interno inesperado.")
        };

        if (status >= 500)
            logger.LogError(exception, "Unhandled exception");
        else
            logger.LogWarning(exception, "Handled exception: {Title}", title);

        context.Response.StatusCode = status;

        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            Exception = exception,
            ProblemDetails = new ProblemDetails { Status = status, Title = title }
        });
    }
}