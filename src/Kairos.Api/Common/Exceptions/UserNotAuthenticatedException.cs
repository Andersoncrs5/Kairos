using System.Collections.ObjectModel;

namespace Kairos.Api.Common.Exceptions;

public sealed class UserNotAuthenticatedException : Exception
{
    public const int DefaultStatusCode = 401;

    public const string DefaultErrorCode = "USER_NOT_AUTHENTICATED";

    public string ErrorCode { get; }

    public int StatusCode { get; }

    /// <summary>
    /// Additional human-readable context about the exception.
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// Inbox identifier associated with the operation.
    /// </summary>
    public string? InboxId { get; }

    /// <summary>
    /// HTTP request trace identifier.
    /// </summary>
    public string? TraceId { get; }

    /// <summary>
    /// HTTP request path.
    /// </summary>
    public string? Path { get; }

    /// <summary>
    /// HTTP request method.
    /// </summary>
    public string? HttpMethod { get; }

    /// <summary>
    /// ASP.NET Core endpoint display name.
    /// </summary>
    public string? Endpoint { get; }

    /// <summary>
    /// Authentication scheme used by the request.
    /// </summary>
    public string? AuthenticationScheme { get; }

    /// <summary>
    /// Claims expected to identify the authenticated user.
    /// </summary>
    public IReadOnlyCollection<string> RequiredClaims { get; }

    /// <summary>
    /// Additional contextual information.
    /// </summary>
    public IReadOnlyDictionary<string, string?> Metadata { get; }

    /// <summary>
    /// Date and time when the exception was created.
    /// </summary>
    public DateTimeOffset OccurredAt { get; }

    public UserNotAuthenticatedException(
        string text,
        string? inboxId = null,
        string? traceId = null,
        string? path = null,
        string? httpMethod = null,
        string? endpoint = null,
        string? authenticationScheme = null,
        IEnumerable<string>? requiredClaims = null,
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

        AuthenticationScheme =
            Normalize(authenticationScheme);

        RequiredClaims = Array.AsReadOnly(
            (requiredClaims ?? ["sid", "sub"])
            .Where(claim => !string.IsNullOrWhiteSpace(claim))
            .Select(claim => claim.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray());

        Dictionary<string, string?> metadataCopy =
            metadata is null
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
            ? "The authenticated user could not be identified."
            : text.Trim();
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}