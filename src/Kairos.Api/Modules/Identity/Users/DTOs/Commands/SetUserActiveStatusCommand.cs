using System.ComponentModel.DataAnnotations;

namespace Kairos.Api.Modules.Identity.Users.DTOs.Commands;

public sealed class SetUserActiveStatusCommand
{
    [Required]
    public string UserId { get; init; } = string.Empty;

    public bool IsActive { get; init; }
}