using System.ComponentModel.DataAnnotations;

namespace Kairos.Api.Modules.Identity.Auth.DTOs.Request;

public sealed record RefreshTokenDto(
    [Required(ErrorMessage = "Refresh token is required.")]
    string RefreshToken
);