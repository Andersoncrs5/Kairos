namespace Kairos.Api.Modules.Identity.Admin.DTOs.Request;

/// <summary>
/// Request for changing a user's account activation status.
/// </summary>
public sealed record SetUserActiveStatusRequest(
    bool IsActive);