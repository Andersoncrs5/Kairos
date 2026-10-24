namespace Kairos.Api.Common.Res;

public sealed record ValidationErrorResponse
    : ResponseHttp<IDictionary<string, string[]>>
{
    public ValidationErrorResponse(
        IDictionary<string, string[]> data,
        string traceId,
        DateTimeOffset timestamp)
        : base(
            Data: data,
            Message: "Validation failed",
            TraceId: traceId,
            Success: false,
            Timestamp: timestamp)
    {
    }
}