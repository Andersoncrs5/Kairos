using Kairos.Api.Common.Base.Model;

namespace Kairos.Api.Common.Base.Repository;

public static class BaseFilterExtensions
{
    public static IQueryable<T> ApplyBaseFilter<T>(
        this IQueryable<T> query,
        BaseFilter dto)
        where T : BaseModel
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(dto);

        if (!string.IsNullOrWhiteSpace(dto.Id))
        {
            string id = dto.Id.Trim();
            query = query.Where(x => x.Id == id);
        }

        if (dto.VersionMin.HasValue)
        {
            long versionMin = dto.VersionMin.Value;
            query = query.Where(x => x.Version >= versionMin);
        }

        if (dto.VersionMax.HasValue)
        {
            long versionMax = dto.VersionMax.Value;
            query = query.Where(x => x.Version <= versionMax);
        }

        if (dto.CreatedAtMin.HasValue)
        {
            DateTimeOffset createdAtMin = dto.CreatedAtMin.Value;
            query = query.Where(x => x.CreatedAt >= createdAtMin);
        }

        if (dto.CreatedAtMax.HasValue)
        {
            DateTimeOffset createdAtMax = dto.CreatedAtMax.Value;
            query = query.Where(x => x.CreatedAt <= createdAtMax);
        }

        if (dto.UpdatedAtMin.HasValue)
        {
            DateTimeOffset updatedAtMin = dto.UpdatedAtMin.Value;
            query = query.Where(x =>
                x.UpdatedAt.HasValue &&
                x.UpdatedAt.Value >= updatedAtMin);
        }

        if (dto.UpdatedAtMax.HasValue)
        {
            DateTimeOffset updatedAtMax = dto.UpdatedAtMax.Value;
            query = query.Where(x =>
                x.UpdatedAt.HasValue &&
                x.UpdatedAt.Value <= updatedAtMax);
        }

        return query;
    }
}