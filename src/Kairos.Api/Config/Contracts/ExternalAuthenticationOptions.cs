namespace Kairos.Api.Config.Contracts;

public sealed class ExternalAuthenticationOptions
{
    public const string SectionName = "ExternalAuthentication";

    public ExternalProviderOptions Google { get; set; } = new();

    public ExternalProviderOptions GitHub { get; set; } = new();

    public ExternalProviderOptions Discord { get; set; } = new();

    public FacebookProviderOptions Facebook { get; set; } = new();

    public ExternalProviderOptions LinkedIn { get; set; } = new();

    public MicrosoftProviderOptions Microsoft { get; set; } = new();
}

public class ExternalProviderOptions
{
    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ClientId) &&
        !string.IsNullOrWhiteSpace(ClientSecret);
}

public sealed class FacebookProviderOptions : ExternalProviderOptions
{
    /// <summary>
    /// Facebook never says whether the email is verified. Keep false unless
    /// you accept treating the email it returns as verified.
    /// </summary>
    public bool TrustEmail { get; set; }
}

public sealed class MicrosoftProviderOptions : ExternalProviderOptions
{
    /// <summary>
    /// "consumers" (personal accounts only, default), a specific tenant id
    /// or domain (single tenant), "organizations" or "common".
    /// </summary>
    public string Tenant { get; set; } = "consumers";
}