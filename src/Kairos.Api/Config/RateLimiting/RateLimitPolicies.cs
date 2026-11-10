namespace Kairos.Api.Config.RateLimiting;

public static class RateLimitPolicies
{
    public const string AuthLogin = "auth-login";
    public const string AuthRegister = "auth-register";
    public const string AuthRefresh = "auth-refresh";
    public const string AuthLookup = "auth-lookup";

    public const string ChangePassword = "change-password";
    public const string AccountDelete = "account-delete";
    public const string ReservationCreate = "reservation-create";
    public const string Availability = "availability";
    public const string CheckIn = "check-in";
    public const string FeedbackCreate = "feedback-create";
    public const string Admin = "admin";
    public const string HeavyRead = "heavy-read";
}