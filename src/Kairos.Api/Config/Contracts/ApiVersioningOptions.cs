namespace Kairos.Api.Config.Contracts;

public sealed class ApiVersioningOptions
{
    public const string SectionName = "ApiVersioning";

    public int DefaultMajorVersion { get; set; } = 1;

    public int DefaultMinorVersion { get; set; } = 0;

    public bool AssumeDefaultVersionWhenUnspecified { get; set; } = true;

    public bool ReportApiVersions { get; set; } = true;

    public string GroupNameFormat { get; set; } = "'v'VVV";

    public bool SubstituteApiVersionInUrl { get; set; } = true;
}