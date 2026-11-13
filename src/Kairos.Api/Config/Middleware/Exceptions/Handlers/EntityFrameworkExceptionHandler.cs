using EntityFramework.Exceptions.Common;
using EntityFrameworkCore.Locking.Exceptions;
using Kairos.Api.Config.Middleware.Exceptions.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Npgsql;

namespace Kairos.Api.Config.Middleware.Exceptions.Handlers;

public sealed class EntityFrameworkExceptionHandler(
    ExceptionResponseWriter writer,
    ILogger<EntityFrameworkExceptionHandler> logger)
    : ApiExceptionHandler<DbUpdateException>(writer, logger)
{
    protected override ValueTask<bool> HandleAsync(
        HttpContext context,
        DbUpdateException exception,
        CancellationToken ct)
    {
        // Optimistic concurrency.
        if (exception is DbUpdateConcurrencyException)
        {
            const string message =
                "The record was modified by another operation. Reload and try again.";

            return WriteResponseAsync(
                context,
                exception,
                StatusCodes.Status409Conflict,
                message,
                errors: new[] { message },
                ct: ct);
        }

        PostgresException? postgresException =
            FindPostgresException(exception);

        // A required column received NULL.
        if (exception is CannotInsertNullException)
        {
            ColumnInfo? column = FindColumn(
                exception,
                postgresException?.ColumnName,
                (property, value) =>
                    !property.IsNullable && value is null);

            string message = column is not null
                ? $"The column '{column.ColumnName}' cannot be null."
                : "A required column cannot be null.";

            return WriteResponseAsync(
                context,
                exception,
                StatusCodes.Status400BadRequest,
                message,
                errorValids: CreateFieldError(column, message),
                errors: column is null ? new[] { message } : null,
                ct: ct);
        }

        // A value exceeds the configured column length.
        if (exception is MaxLengthExceededException)
        {
            ColumnInfo? column = FindColumn(
                exception,
                postgresException?.ColumnName,
                (property, value) =>
                {
                    int? maxLength = property.GetMaxLength();

                    return maxLength.HasValue &&
                        (value is string text && text.Length > maxLength ||
                         value is byte[] bytes && bytes.Length > maxLength);
                });

            string message = column is not null &&
                             column.MaxLength.HasValue
                ? $"The column '{column.ColumnName}' must not exceed {column.MaxLength} characters."
                : "A value exceeds the maximum length allowed for a column.";

            return WriteResponseAsync(
                context,
                exception,
                StatusCodes.Status400BadRequest,
                message,
                errorValids: CreateFieldError(column, message),
                errors: column is null ? new[] { message } : null,
                ct: ct);
        }

        // Unique constraint violation.
        if (exception is UniqueConstraintException uniqueException)
        {
            string[] properties =
                uniqueException.ConstraintProperties?.ToArray()
                ?? Array.Empty<string>();

            string message = properties.Length > 0
                ? $"A record with the same value already exists for: {string.Join(", ", properties)}."
                : "A record with these values already exists.";

            IReadOnlyDictionary<string, string[]>? fieldErrors =
                properties.Length == 0
                    ? null
                    : properties.Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToDictionary(
                            property => property,
                            _ => new[] { message },
                            StringComparer.OrdinalIgnoreCase);

            return WriteResponseAsync(
                context,
                exception,
                StatusCodes.Status409Conflict,
                message,
                errors: new[] { message },
                errorValids: fieldErrors,
                ct: ct);
        }

        // Foreign key constraint violation.
        if (exception is ReferenceConstraintException)
        {
            bool isDeleting = exception.Entries.Any(
                entry => entry.State == EntityState.Deleted);

            string message = isDeleting
                ? "This record has dependencies and cannot be deleted."
                : "The referenced record does not exist.";

            return WriteResponseAsync(
                context,
                exception,
                StatusCodes.Status409Conflict,
                message,
                errors: new[] { message },
                ct: ct);
        }

        // Numeric value outside the database column range.
        if (exception is NumericOverflowException)
        {
            const string message =
                "A numeric value exceeds the allowed range.";

            return WriteResponseAsync(
                context,
                exception,
                StatusCodes.Status400BadRequest,
                message,
                errors: new[] { message },
                ct: ct);
        }

        // Database deadlock.
        if (exception is DeadlockException)
        {
            const string message =
                "The operation could not be completed due to a database conflict. Please try again.";

            return WriteResponseAsync(
                context,
                exception,
                StatusCodes.Status503ServiceUnavailable,
                message,
                errors: new[] { message },
                ct: ct);
        }

        // Let the next registered handler process unknown DbUpdateExceptions.
        return ValueTask.FromResult(false);
    }

    private static PostgresException? FindPostgresException(
        Exception exception)
    {
        Exception? current = exception;

        while (current is not null)
        {
            if (current is PostgresException postgresException)
                return postgresException;

            current = current.InnerException;
        }

        return null;
    }

    private static ColumnInfo? FindColumn(
        DbUpdateException exception,
        string? databaseColumnName,
        Func<IProperty, object?, bool> predicate)
    {
        List<ColumnInfo> candidates = new();

        foreach (var entry in exception.Entries)
        {
            string? tableName = entry.Metadata.GetTableName();

            if (tableName is null)
                continue;

            StoreObjectIdentifier storeObject =
                StoreObjectIdentifier.Table(
                    tableName,
                    entry.Metadata.GetSchema());

            foreach (IProperty property in entry.Metadata.GetProperties())
            {
                string? columnName = property.GetColumnName(storeObject);

                if (columnName is null)
                    continue;

                if (databaseColumnName is not null &&
                    !string.Equals(
                        columnName,
                        databaseColumnName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                object? value = entry.Property(property.Name).CurrentValue;

                if (!predicate(property, value))
                    continue;

                candidates.Add(new ColumnInfo(
                    PropertyName: property.Name,
                    ColumnName: columnName,
                    MaxLength: property.GetMaxLength()));
            }
        }

        // Only infer a column when the result is unambiguous.
        return candidates.Count == 1
            ? candidates[0]
            : null;
    }

    private static IReadOnlyDictionary<string, string[]>? CreateFieldError(
        ColumnInfo? column,
        string message)
    {
        if (column is null)
            return null;

        return new Dictionary<string, string[]>
        {
            [column.PropertyName] = new[] { message }
        };
    }

    private sealed record ColumnInfo(
        string PropertyName,
        string ColumnName,
        int? MaxLength);
}