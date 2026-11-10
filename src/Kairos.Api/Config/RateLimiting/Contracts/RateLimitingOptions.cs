namespace Kairos.Api.Config.RateLimiting.Contracts;

public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    public bool Enabled { get; set; } = true;

    public string[] ExemptPaths { get; set; } = ["/health", "/swagger"];

    public RateLimitRule Global { get; set; } = new() { PermitLimit = 300, WindowSeconds = 60 };
    public RateLimitRule GlobalWrite { get; set; } = new() { PermitLimit = 60, WindowSeconds = 60 };

    public RateLimitRule AuthLogin { get; set; } = new() { PermitLimit = 10, WindowSeconds = 60 };
    public RateLimitRule AuthRegister { get; set; } = new() { PermitLimit = 5, WindowSeconds = 3600 };
    public RateLimitRule AuthRefresh { get; set; } = new() { PermitLimit = 60, WindowSeconds = 60 };
    public RateLimitRule AuthLookup { get; set; } = new() { PermitLimit = 20, WindowSeconds = 60 };

    public RateLimitRule ChangePassword { get; set; } = new() { PermitLimit = 5, WindowSeconds = 900 };
    public RateLimitRule AccountDelete { get; set; } = new() { PermitLimit = 3, WindowSeconds = 3600 };
    public RateLimitRule ReservationCreate { get; set; } = new() { PermitLimit = 10, WindowSeconds = 60 };
    public RateLimitRule Availability { get; set; } = new() { PermitLimit = 60, WindowSeconds = 60 };
    public RateLimitRule CheckIn { get; set; } = new() { PermitLimit = 20, WindowSeconds = 60 };
    public RateLimitRule FeedbackCreate { get; set; } = new() { PermitLimit = 10, WindowSeconds = 3600 };
    public RateLimitRule Admin { get; set; } = new() { PermitLimit = 60, WindowSeconds = 60 };
    public RateLimitRule HeavyRead { get; set; } = new() { PermitLimit = 60, WindowSeconds = 60 };
}

public sealed class RateLimitRule
{
    public int PermitLimit { get; set; } = 60;

    public int WindowSeconds { get; set; } = 60;

    public int SegmentsPerWindow { get; set; } = 6;

    public bool IsValid =>
        PermitLimit > 0 && WindowSeconds > 0 && SegmentsPerWindow > 0;
}