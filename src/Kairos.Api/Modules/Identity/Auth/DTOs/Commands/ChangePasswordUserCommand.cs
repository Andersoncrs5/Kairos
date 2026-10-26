using System.ComponentModel.DataAnnotations;

namespace Kairos.Api.Modules.Identity.Auth.DTOs.Commands;

public sealed record ChangePasswordUserCommand(
    [property: Required(ErrorMessage = "User ID is required.")]
    string UserId,

    [property: Required(ErrorMessage = "Current password is required.")]
    string CurrentPassword,

    [property: Required(ErrorMessage = "New password is required.")]
    string NewPassword,

    [property: Required(ErrorMessage = "Password confirmation is required.")]
    string ConfirmPassword
) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(
        ValidationContext validationContext)
    {
        if (!string.IsNullOrWhiteSpace(CurrentPassword) &&
            !string.IsNullOrWhiteSpace(NewPassword) &&
            string.Equals(
                CurrentPassword,
                NewPassword,
                StringComparison.Ordinal))
        {
            yield return new ValidationResult(
                "The new password must be different from the current password.",
                [nameof(NewPassword)]);
        }

        if (!string.IsNullOrWhiteSpace(NewPassword) &&
            !string.IsNullOrWhiteSpace(ConfirmPassword) &&
            !string.Equals(
                NewPassword,
                ConfirmPassword,
                StringComparison.Ordinal))
        {
            yield return new ValidationResult(
                "The password confirmation does not match.",
                [nameof(ConfirmPassword)]);
        }
    }
}