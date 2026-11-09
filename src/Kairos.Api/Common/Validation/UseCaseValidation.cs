using System.ComponentModel.DataAnnotations;

namespace Kairos.Api.Common.Validation;

public static class UseCaseValidation
{
    public static bool TryValidate(
        object instance,
        string fallbackMessage,
        out string[] errors)
    {
        List<ValidationResult> results = [];

        if (Validator.TryValidateObject(
                instance,
                new ValidationContext(instance),
                results,
                validateAllProperties: true))
        {
            errors = [];
            return true;
        }

        errors = results
            .Select(result => result.ErrorMessage ?? fallbackMessage)
            .Distinct()
            .ToArray();

        return false;
    }

    public static bool TryParseUuid(string? value, out string normalized)
    {
        if (Guid.TryParse(value?.Trim(), out Guid guid))
        {
            normalized = guid.ToString();
            return true;
        }

        normalized = string.Empty;
        return false;
    }
}