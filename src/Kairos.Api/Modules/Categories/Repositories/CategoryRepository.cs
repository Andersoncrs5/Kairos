using Kairos.Api.Common.Base.Model;
using Kairos.Api.Common.Base.Repository;
using Kairos.Api.Common.Page;
using Kairos.Api.Config.DB;
using Kairos.Api.Modules.Categories.DTOs.Request;
using Kairos.Api.Modules.Categories.Models;
using Microsoft.EntityFrameworkCore;

namespace Kairos.Api.Modules.Categories.Repositories;

public sealed class CategoryRepository(AppDbContext context)
    : BaseRepositoryImpl<CategoryModel>(context), ICategoryRepository
{
    public async Task<Page<CategoryModel>> FilterAsync(
        CategoryFilterDto filter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var query = context
            .Set<CategoryModel>()
            .AsNoTracking();

        // Identificador
        if (!string.IsNullOrWhiteSpace(filter.Id))
        {
            query = query.Where(category =>
                category.Id == filter.Id);
        }

        // Versão
        if (filter.VersionMin.HasValue)
        {
            query = query.Where(category =>
                category.Version >= filter.VersionMin.Value);
        }

        if (filter.VersionMax.HasValue)
        {
            query = query.Where(category =>
                category.Version <= filter.VersionMax.Value);
        }

        // Data de criação
        if (filter.CreatedAtMin.HasValue)
        {
            query = query.Where(category =>
                category.CreatedAt >= filter.CreatedAtMin.Value);
        }

        if (filter.CreatedAtMax.HasValue)
        {
            query = query.Where(category =>
                category.CreatedAt <= filter.CreatedAtMax.Value);
        }

        // Data de atualização
        if (filter.UpdatedAtMin.HasValue)
        {
            query = query.Where(category =>
                category.UpdatedAt.HasValue &&
                category.UpdatedAt.Value >= filter.UpdatedAtMin.Value);
        }

        if (filter.UpdatedAtMax.HasValue)
        {
            query = query.Where(category =>
                category.UpdatedAt.HasValue &&
                category.UpdatedAt.Value <= filter.UpdatedAtMax.Value);
        }

        // Nome
        if (!string.IsNullOrWhiteSpace(filter.Name))
        {
            var name = filter.Name.Trim();
            var pattern = $"%{name}%";

            query = filter.NameIgnoreCase == true
                ? query.Where(category =>
                    EF.Functions.ILike(category.Name, pattern))
                : query.Where(category =>
                    EF.Functions.Like(category.Name, pattern));
        }

        // Descrição
        if (!string.IsNullOrWhiteSpace(filter.Description))
        {
            var description = filter.Description.Trim();
            var pattern = $"%{description}%";

            query = filter.DescriptionIgnoreCase == true
                ? query.Where(category =>
                    category.Description != null &&
                    EF.Functions.ILike(category.Description, pattern))
                : query.Where(category =>
                    category.Description != null &&
                    EF.Functions.Like(category.Description, pattern));
        }

        // Estado
        if (filter.IsActive.HasValue)
        {
            query = query.Where(category =>
                category.IsActive == filter.IsActive.Value);
        }

        // Total de registros após aplicar os filtros,
        // mas antes de aplicar a paginação.
        long totalCount = await query.LongCountAsync(
            cancellationToken);

        // Paginação
        int pageNumber = Math.Max(filter.Page ?? 1, 1);
        int pageSize = Math.Clamp(filter.PageSize ?? 20, 1, 100);

        // Ordenação determinística.
        query = filter.ColumnOrder == ColumnOrderEnum.DESC
            ? query
                .OrderByDescending(category => category.CreatedAt)
                .ThenByDescending(category => category.Id)
            : query
                .OrderBy(category => category.CreatedAt)
                .ThenBy(category => category.Id);

        long offset = ((long)pageNumber - 1) * pageSize;
        int skip = (int)Math.Min(offset, int.MaxValue);

        var items = await query
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return Page<CategoryModel>.Create(
            items,
            totalCount,
            pageNumber,
            pageSize);
    }
    
    public Task<CategoryModel?> GetByNameAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        return context.Set<CategoryModel>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                category => category.Name == name,
                cancellationToken);
    }

    public Task<bool> ExistsByNameAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        return context.Set<CategoryModel>()
            .AnyAsync(
                category => category.Name.ToLower() == name.ToLower(),
                cancellationToken);
    }

    public async Task<IReadOnlyList<CategoryModel>> GetActiveAsync(
        CancellationToken cancellationToken = default)
    {
        return await context.Set<CategoryModel>()
            .AsNoTracking()
            .Where(category => category.IsActive)
            .ToListAsync(cancellationToken);
    }
}