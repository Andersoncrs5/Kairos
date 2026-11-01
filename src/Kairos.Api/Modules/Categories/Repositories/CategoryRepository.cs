
using Kairos.Api.Common.Base.Model;
using Kairos.Api.Common.Base.Repository;
using Kairos.Api.Common.Enums;
using Kairos.Api.Common.Page;
using Kairos.Api.Config.DB;
using Kairos.Api.Modules.Categories.DTOs.Request;
using Kairos.Api.Modules.Categories.Models;
using Microsoft.EntityFrameworkCore;

namespace Kairos.Api.Modules.Categories.Repositories;

public sealed class CategoryRepository(AppDbContext context)
    : BaseRepositoryImpl<CategoryModel>(context), ICategoryRepository
{
    // ----------------------------------------------------------
    // Filtering
    // ----------------------------------------------------------

    public async Task<Page<CategoryModel>> FilterAsync(
        CategoryFilterDto filter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        IQueryable<CategoryModel> query = context.Categories
            .AsNoTracking();

        // ------------------------------------------------------
        // Identification: ID
        // ------------------------------------------------------

        if (!string.IsNullOrWhiteSpace(filter.Id))
        {
            string id = filter.Id.Trim();

            query = query.Where(category => category.Id == id);
        }

        // ------------------------------------------------------
        // Identification: Code
        // ------------------------------------------------------

        if (!string.IsNullOrWhiteSpace(filter.Code))
        {
            string code = filter.Code.Trim();
            string pattern = $"%{code}%";

            query = filter.CodeIgnoreCase == true
                ? query.Where(category =>
                    EF.Functions.ILike(category.Code, pattern))
                : query.Where(category =>
                    EF.Functions.Like(category.Code, pattern));
        }

        // ------------------------------------------------------
        // Identification: Name
        // ------------------------------------------------------

        if (!string.IsNullOrWhiteSpace(filter.Name))
        {
            string name = filter.Name.Trim();
            string pattern = $"%{name}%";

            query = filter.NameIgnoreCase == true
                ? query.Where(category =>
                    EF.Functions.ILike(category.Name, pattern))
                : query.Where(category =>
                    EF.Functions.Like(category.Name, pattern));
        }

        // ------------------------------------------------------
        // Description
        // ------------------------------------------------------

        if (!string.IsNullOrWhiteSpace(filter.Description))
        {
            string description = filter.Description.Trim();
            string pattern = $"%{description}%";

            query = filter.DescriptionIgnoreCase == true
                ? query.Where(category =>
                    category.Description != null &&
                    EF.Functions.ILike(category.Description, pattern))
                : query.Where(category =>
                    category.Description != null &&
                    EF.Functions.Like(category.Description, pattern));
        }

        // ------------------------------------------------------
        // Appearance: Color
        // ------------------------------------------------------

        if (!string.IsNullOrWhiteSpace(filter.Color))
        {
            string color = filter.Color.Trim();

            query = query.Where(category =>
                category.Color != null &&
                EF.Functions.ILike(category.Color, color));
        }

        // ------------------------------------------------------
        // Appearance: Icon
        // ------------------------------------------------------

        if (!string.IsNullOrWhiteSpace(filter.Icon))
        {
            string icon = filter.Icon.Trim();
            string pattern = $"%{icon}%";

            query = filter.IconIgnoreCase == true
                ? query.Where(category =>
                    category.Icon != null &&
                    EF.Functions.ILike(category.Icon, pattern))
                : query.Where(category =>
                    category.Icon != null &&
                    EF.Functions.Like(category.Icon, pattern));
        }

        // ------------------------------------------------------
        // Organization: DisplayOrder
        // ------------------------------------------------------

        if (filter.DisplayOrder.HasValue)
        {
            int displayOrder = filter.DisplayOrder.Value;

            query = query.Where(category =>
                category.DisplayOrder == displayOrder);
        }

        // ------------------------------------------------------
        // Version
        // ------------------------------------------------------

        if (filter.VersionMin.HasValue)
        {
            long versionMin = filter.VersionMin.Value;

            query = query.Where(category =>
                category.Version >= versionMin);
        }

        if (filter.VersionMax.HasValue)
        {
            long versionMax = filter.VersionMax.Value;

            query = query.Where(category =>
                category.Version <= versionMax);
        }

        // ------------------------------------------------------
        // CreatedAt
        // ------------------------------------------------------

        if (filter.CreatedAtMin.HasValue)
        {
            DateTimeOffset createdAtMin = filter.CreatedAtMin.Value;

            query = query.Where(category =>
                category.CreatedAt >= createdAtMin);
        }

        if (filter.CreatedAtMax.HasValue)
        {
            DateTimeOffset createdAtMax = filter.CreatedAtMax.Value;

            query = query.Where(category =>
                category.CreatedAt <= createdAtMax);
        }

        // ------------------------------------------------------
        // UpdatedAt
        // ------------------------------------------------------

        if (filter.UpdatedAtMin.HasValue)
        {
            DateTimeOffset updatedAtMin = filter.UpdatedAtMin.Value;

            query = query.Where(category =>
                category.UpdatedAt.HasValue &&
                category.UpdatedAt.Value >= updatedAtMin);
        }

        if (filter.UpdatedAtMax.HasValue)
        {
            DateTimeOffset updatedAtMax = filter.UpdatedAtMax.Value;

            query = query.Where(category =>
                category.UpdatedAt.HasValue &&
                category.UpdatedAt.Value <= updatedAtMax);
        }

        // ------------------------------------------------------
        // Status
        // ------------------------------------------------------

        if (filter.IsActive.HasValue)
        {
            bool isActive = filter.IsActive.Value;

            query = query.Where(category =>
                category.IsActive == isActive);
        }

        // ------------------------------------------------------
        // Total count (before pagination)
        // ------------------------------------------------------

        long totalCount = await query.LongCountAsync(
            cancellationToken);

        // ------------------------------------------------------
        // Pagination
        // ------------------------------------------------------

        int pageNumber = Math.Max(filter.Page ?? 1, 1);

        int pageSize = Math.Clamp(
            filter.PageSize ?? 20,
            1,
            100);

        // ------------------------------------------------------
        // Deterministic ordering
        // ------------------------------------------------------

        IOrderedQueryable<CategoryModel> orderedQuery =
            filter.ColumnOrder == ColumnOrderEnum.DESC
                ? query
                    .OrderByDescending(category => category.CreatedAt)
                    .ThenByDescending(category => category.Id)
                : query
                    .OrderBy(category => category.CreatedAt)
                    .ThenBy(category => category.Id);

        // ------------------------------------------------------
        // Offset
        // ------------------------------------------------------

        long offset = ((long)pageNumber - 1) * pageSize;

        if (offset > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(filter.Page),
                "The requested page exceeds the supported offset.");
        }

        // ------------------------------------------------------
        // Execute query
        // ------------------------------------------------------

        List<CategoryModel> items = await orderedQuery
            .Skip((int)offset)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return Page<CategoryModel>.Create(
            items,
            totalCount,
            pageNumber,
            pageSize);
    }

    // ----------------------------------------------------------
    // Get by Code
    // ----------------------------------------------------------

    public Task<CategoryModel?> GetByCodeAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        return context.Categories
            .AsNoTracking()
            .SingleOrDefaultAsync(
                category => category.Code == code,
                cancellationToken);
    }

    // ----------------------------------------------------------
    // Exists by Code
    // ----------------------------------------------------------

    public Task<bool> ExistsByCodeAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        return context.Categories
            .AnyAsync(
                category => category.Code == code,
                cancellationToken);
    }

    // ----------------------------------------------------------
    // Get by Name
    // ----------------------------------------------------------

    public Task<CategoryModel?> GetByNameAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return context.Categories
            .AsNoTracking()
            .SingleOrDefaultAsync(
                category => category.Name == name,
                cancellationToken);
    }

    // ----------------------------------------------------------
    // Exists by Name
    // ----------------------------------------------------------

    public Task<bool> ExistsByNameAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return context.Categories
            .AnyAsync(
                category => EF.Functions.ILike(category.Name, name),
                cancellationToken);
    }

    // ----------------------------------------------------------
    // Get active categories
    // ----------------------------------------------------------

    public async Task<IReadOnlyList<CategoryModel>> GetActiveAsync(
        CancellationToken cancellationToken = default)
    {
        return await context.Categories
            .AsNoTracking()
            .Where(category => category.IsActive)
            .OrderBy(category => category.DisplayOrder)
            .ThenBy(category => category.Name)
            .ThenBy(category => category.Id)
            .ToListAsync(cancellationToken);
    }
}
