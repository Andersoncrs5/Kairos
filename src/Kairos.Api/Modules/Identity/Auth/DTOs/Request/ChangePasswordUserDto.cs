using System.ComponentModel.DataAnnotations;

namespace Kairos.Api.Modules.Identity.Auth.DTOs.Request;

public sealed record ChangePasswordUserDto(
    [Required(ErrorMessage = "Current password is required.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Current password must be between 8 and 100 characters.")]
    string CurrentPassword,

    [Required(ErrorMessage = "New password is required.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "New password must be between 8 and 100 characters.")]
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

        if (string.Equals(CurrentPassword, NewPassword, StringComparison.Ordinal))
        {
            yield return new ValidationResult(
                "The new password must be different from the current password.",
                [nameof(NewPassword)]);
        }
    }
}