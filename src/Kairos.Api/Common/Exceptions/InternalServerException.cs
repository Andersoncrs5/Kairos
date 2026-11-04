using System.Collections.ObjectModel;

namespace Kairos.Api.Common.Exceptions;

public sealed class InternalServerException : Exception
{
    public const int DefaultStatusCode = 500;
    public const string DefaultErrorCode = "INTERNAL_SERVER_ERROR";

    public string ErrorCode { get; }
    public int StatusCode { get; }
    public string Text { get; }

    public string? InboxId { get; }
    public string? TraceId { get; }
    public string? Path { get; }
    public string? HttpMethod { get; }
    public string? Endpoint { get; }

    public string? OriginalExceptionType { get; }

    public IReadOnlyDictionary<string, string?> Metadata { get; }

    public DateTimeOffset OccurredAt { get; }

    public InternalServerException(
        string? text = null,
        string? inboxId = null,
        string? traceId = null,
        string? path = null,
        string? httpMethod = null,
        string? endpoint = null,
        IReadOnlyDictionary<string, string?>? metadata = null,
        DateTimeOffset? occurredAt = null,
        Exception? innerException = null)
        : base(NormalizeText(text), innerException)
    {
        ErrorCode = DefaultErrorCode;
        StatusCode = DefaultStatusCode;

        Text = Message;

        InboxId = Normalize(inboxId);
        TraceId = Normalize(traceId);
        Path = Normalize(path);
        HttpMethod = Normalize(httpMethod);
        Endpoint = Normalize(endpoint);

        OriginalExceptionType = innerException?.GetType().FullName;

        Dictionary<string, string?> metadataCopy = metadata is null
            ? new Dictionary<string, string?>()
            : metadata.ToDictionary(
                entry => entry.Key,
                entry => entry.Value,
                StringComparer.OrdinalIgnoreCase);

        Metadata = new ReadOnlyDictionary<string, string?>(
            metadataCopy);

        OccurredAt = occurredAt ?? DateTimeOffset.UtcNow;
    }

    private static string NormalizeText(string? text)
    {
        return string.IsNullOrWhiteSpace(text)
            ? "An unexpected internal server error occurred."
            : text.Trim();
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}