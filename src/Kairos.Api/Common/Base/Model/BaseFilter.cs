using System.ComponentModel.DataAnnotations;

namespace Kairos.Api.Common.Base.Model;

public class BaseFilter
{
    public required string Id { get; set; }
    
    public long VersionMin { get; set; }
    public long VersionMax { get; set; }
    
    public required DateTimeOffset CreatedAtMin { get; set; }
    public required DateTimeOffset CreatedAtMax { get; set; }
    
    public DateTimeOffset? UpdatedAtMin { get; set; }
    public DateTimeOffset? UpdatedAtMax { get; set; }
    
    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    [Range(1, 100)]
    public int PageSize { get; init; } = 20;
    
    public ColumnOrderEnum ColumnOrder { get; set; } = ColumnOrderEnum.ASC;
}

public enum ColumnOrderEnum
{
    ASC,
    DESC
}