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
}