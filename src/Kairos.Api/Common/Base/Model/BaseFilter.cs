using System.ComponentModel.DataAnnotations;

namespace Kairos.Api.Common.Base.Model;

public class BaseFilter : IValidatableObject
{
    public string? Id { get; set; }

    public long? VersionMin { get; set; }

    public long? VersionMax { get; set; }

    public DateTimeOffset? CreatedAtMin { get; set; }

    public DateTimeOffset? CreatedAtMax { get; set; }

    public DateTimeOffset? UpdatedAtMin { get; set; }

    public DateTimeOffset? UpdatedAtMax { get; set; }

    [Range(1, int.MaxValue)]
    public int? Page { get; set; } = 1;

    [Range(1, 100)]
    public int? PageSize { get; set; } = 20;

    [EnumDataType(typeof(ColumnOrderEnum))]
    public ColumnOrderEnum? ColumnOrder { get; set; }
        = ColumnOrderEnum.ASC;

    public IEnumerable<ValidationResult> Validate(
        ValidationContext validationContext)
    {
        if (VersionMin.HasValue &&
            VersionMax.HasValue &&
            VersionMin.Value > VersionMax.Value)
        {
            yield return new ValidationResult(
                "VersionMin cannot be greater than VersionMax.",
                [nameof(VersionMin), nameof(VersionMax)]);
        }

        if (CreatedAtMin.HasValue &&
            CreatedAtMax.HasValue &&
            CreatedAtMin.Value > CreatedAtMax.Value)
        {
            yield return new ValidationResult(
                "CreatedAtMin cannot be greater than CreatedAtMax.",
                [nameof(CreatedAtMin), nameof(CreatedAtMax)]);
        }

        if (UpdatedAtMin.HasValue &&
            UpdatedAtMax.HasValue &&
            UpdatedAtMin.Value > UpdatedAtMax.Value)
        {
            yield return new ValidationResult(
                "UpdatedAtMin cannot be greater than UpdatedAtMax.",
                [nameof(UpdatedAtMin), nameof(UpdatedAtMax)]);
        }
    }
}

public enum ColumnOrderEnum
{
    ASC,
    DESC
}