namespace Kairos.Api.Common.Result;

public sealed class Result<T>
{
    private Result(
        bool isSuccess,
        T? value,
        int statusCode,
        IEnumerable<string>? errors)
    {
        if (isSuccess && statusCode is < 200 or >= 300)
        {
            throw new ArgumentOutOfRangeException(
                nameof(statusCode),
                "Resultados de sucesso devem usar um status HTTP 2xx.");
        }

        if (!isSuccess && statusCode is >= 200 and < 300)
        {
            throw new ArgumentOutOfRangeException(
                nameof(statusCode),
                "Resultados de falha não podem usar um status HTTP 2xx.");
        }

        var normalizedErrors = errors?
            .Where(error => !string.IsNullOrWhiteSpace(error))
            .ToArray() ?? [];

        if (isSuccess && normalizedErrors.Length > 0)
        {
            throw new ArgumentException(
                "Um resultado de sucesso não pode conter erros.",
                nameof(errors));
        }

        if (!isSuccess && normalizedErrors.Length == 0)
        {
            throw new ArgumentException(
                "Um resultado de falha deve conter pelo menos um erro.",
                nameof(errors));
        }

        IsSuccess = isSuccess;
        Value = value;
        StatusCode = statusCode;
        Errors = Array.AsReadOnly(normalizedErrors);
    }

    public T? Value { get; }

    public int StatusCode { get; }

    public IReadOnlyList<string> Errors { get; }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public string? FirstError => Errors.FirstOrDefault();

    public static Result<T> Success(
        T value,
        int statusCode = 200)
    {
        return new Result<T>(
            true,
            value,
            statusCode,
            null);
    }
    
    public static Result<T> Ok(int statusCode = 204)
    {
        return new Result<T>(
            true,
            default,
            statusCode,
            null);
    }

    public static Result<T> Created(T value)
    {
        return Success(value, 201);
    }

    public static Result<T> Failure(
        string error,
        int statusCode = 400)
    {
        return Failure([error], statusCode);
    }
    
    
    public static Result<T> BadRequest(
        string error,
        int statusCode = 400)
    {
        return Failure([error], statusCode);
    }

    public static Result<T> Failure(
        IEnumerable<string> errors,
        int statusCode = 400)
    {
        ArgumentNullException.ThrowIfNull(errors);

        return new Result<T>(
            false,
            default,
            statusCode,
            errors);
    }

    public static Result<T> ValidationFailure(
        IEnumerable<string> errors)
    {
        return Failure(errors, 400);
    }

    public static Result<T> NotFound(
        string error = "Recurso não encontrado.")
    {
        return Failure(error, 404);
    }

    public static Result<T> Unauthorized(
        string error = "Autenticação necessária.")
    {
        return Failure(error, 401);
    }

    public static Result<T> Forbidden(
        string error = "Você não tem permissão para realizar esta operação.")
    {
        return Failure(error, 403);
    }

    public static Result<T> Conflict(
        string error)
    {
        return Failure(error, 409);
    }
}