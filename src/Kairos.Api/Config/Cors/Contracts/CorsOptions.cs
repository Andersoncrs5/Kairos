namespace Kairos.Api.Config.Cors.Contracts;

public sealed class CorsOptions
{
    public const string SectionName = "Cors";

    public const string PolicyName = "KairosCorsPolicy";

    public string[] AllowedOrigins { get; set; } =
    [
        "http://localhost:3000"
    ];

    public string[] AllowedMethods { get; set; } =
    [
        "GET",
        "POST",
        "PUT",
        "PATCH",
        "DELETE",
        "OPTIONS"
    ];

    public string[] AllowedHeaders { get; set; } =
    [
        "Accept",
        "Authorization",
        "Content-Type",
        "X-Inbox-Id"
    ];

    public string[] ExposedHeaders { get; set; } = [];

    public bool AllowCredentials { get; set; } = false;

    public int PreflightMaxAgeSeconds { get; set; } = 600;
}