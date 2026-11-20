namespace Kairos.Api.Config.Contracts;

public sealed class PasskeyOptions
{
    public const string SectionName = "Passkeys";

    public string ServerDomain { get; set; } = string.Empty;

    public string ServerName { get; set; } = "Kairos";
    public string[] Origins { get; set; } = [];

    public int FlowLifetimeSeconds { get; set; } = 300;
}