using Kairos.Api.Common.Base;
using Kairos.Api.Common.Base.Model;
using Kairos.Api.Config.DB;
using Microsoft.EntityFrameworkCore;

namespace Kairos.Api.Common.Base.Repository;

public class BaseRepositoryImpl<TEntity>(AppDbContext context)
    : IBaseRepository<TEntity>
    where TEntity : BaseModel
{
    private readonly DbSet<TEntity> _dbSet = context.Set<TEntity>();

    public async Task<TEntity?> GetByIdAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        return await _dbSet.FindAsync(
            new object[] { id },
            cancellationToken);
    }

    public async Task<IReadOnlyList<TEntity>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(
        TEntity entity,
        CancellationToken cancellationToken = default)
    {
        await _dbSet.AddAsync(entity, cancellationToken);
    }

    public void Update(TEntity entity)
    {
        _dbSet.Update(entity);
    }

    public void Delete(TEntity entity)
    {
        _dbSet.Remove(entity);
    }

    public Task<bool> ExistsAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        return _dbSet.AnyAsync(
            entity => entity.Id == id,
            cancellationToken);
    }
}