namespace Kairos.Api.Config.Contracts;

public sealed class ApiBehaviorOptions
{
    public const string SectionName = "ApiBehavior";

    public bool SuppressMapClientErrors { get; set; } = true;

    public bool SuppressModelStateInvalidFilter { get; set; }
}