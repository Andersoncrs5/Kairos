namespace Kairos.Api.Common.Base.Model;

public class BaseModel
{
    public required string Id { get; set; }
    public long Version { get; set; }
    public required DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}