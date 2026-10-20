using Kairos.Api.Common.Base.Repository;
using Kairos.Api.Config.DB;
using Kairos.Api.Modules.Categories.Models;
using Microsoft.EntityFrameworkCore;

namespace Kairos.Api.Modules.Categories.Repositories;

public sealed class CategoryRepository(AppDbContext context)
    : BaseRepositoryImpl<CategoryModel>(context), ICategoryRepository
{
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
                category => category.Name == name,
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