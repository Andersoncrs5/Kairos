using System.ComponentModel.DataAnnotations;
using Kairos.Api.Modules.Identity.UserPreference.DTOs.Requests;

namespace Kairos.Api.Modules.Identity.UserPreference.DTOs.Commands;

public sealed class UpdateUserPreferenceCommand
{
    [Required(ErrorMessage = "User ID is required.")]
    public required string UserId { get; set; }

    [Required(ErrorMessage = "Preference data is required.")]
    public required UpdateUserPreferenceDTO Dto { get; set; }
}