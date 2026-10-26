using System.ComponentModel.DataAnnotations;

namespace Kairos.Api.Modules.Identity.Auth.DTOs.Request;

public sealed record ChangePasswordUserDto(
    [Required(ErrorMessage = "Current password is required.")]
    string CurrentPassword,

    [Required(ErrorMessage = "New password is required.")]
    string NewPassword,

    [Required(ErrorMessage = "Password confirmation is required.")]
    string ConfirmPassword
) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!string.Equals(NewPassword, ConfirmPassword, StringComparison.Ordinal))
        {
            yield return new ValidationResult(
                "The password confirmation does not match.",
                [nameof(ConfirmPassword)]);
        }
    }
}