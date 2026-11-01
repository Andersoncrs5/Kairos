using System.Linq.Expressions;
using EntityFrameworkCore.Locking;
using EntityFrameworkCore.Locking.PostgreSQL; 
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

    public async Task AddAllAsync(
        IEnumerable<TEntity> entities,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entities);

        await _dbSet.AddRangeAsync(entities, cancellationToken);
    }

    public void Update(TEntity entity)
    {
        _dbSet.Update(entity);
    }

    public Task UpdateAllAsync(
        IEnumerable<TEntity> entities,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entities);
        cancellationToken.ThrowIfCancellationRequested();

        _dbSet.UpdateRange(entities);

        return Task.CompletedTask;
    }

    public void Delete(TEntity entity)
    {
        _dbSet.Remove(entity);
    }

    public Task<int> DeleteByIdAndCountAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        return _dbSet
            .Where(entity => entity.Id == id)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public Task<int> DeleteAllAsync(
        CancellationToken cancellationToken = default)
    {
        return _dbSet.ExecuteDeleteAsync(cancellationToken);
    }

    public Task<bool> ExistsAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        return _dbSet.AnyAsync(
            entity => entity.Id == id,
            cancellationToken);
    }
    
    public async Task<TEntity?> FindByIdWithForUpdateAsync(
        string id,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        EnsureTransaction();

        IQueryable<TEntity> query = _dbSet.Where(entity => entity.Id == id);

        query = timeout is null
            ? query.ForUpdate()
            : query.ForUpdate(LockBehavior.Wait, timeout.Value);

        return await query.FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<TEntity?> FindByIdWithForUpdateSkipLockedAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        EnsureTransaction();

        return await _dbSet
            .Where(entity => entity.Id == id)
            .ForUpdate(LockBehavior.SkipLocked)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<TEntity?> FindByIdWithForShareAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        EnsureTransaction();

        return await _dbSet
            .Where(entity => entity.Id == id)
            .ForShare()
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<TEntity?> FindByIdWithForNoKeyUpdateAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        EnsureTransaction();

        return await _dbSet
            .Where(entity => entity.Id == id)
            .ForNoKeyUpdate()
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TEntity>> FindByIdsWithForUpdateAsync(
        IEnumerable<string> ids,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);
        EnsureTransaction();

        string[] distinctIds = ids.Distinct().ToArray();

        if (distinctIds.Length == 0)
        {
            return [];
        }

        IQueryable<TEntity> query = _dbSet
            .Where(entity => distinctIds.Contains(entity.Id))
            .OrderBy(entity => entity.Id);

        query = timeout is null
            ? query.ForUpdate()
            : query.ForUpdate(LockBehavior.Wait, timeout.Value);

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TEntity>> FindBatchWithForUpdateSkipLockedAsync(
        Expression<Func<TEntity, bool>> predicate,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);
        EnsureTransaction();

        return await _dbSet
            .Where(predicate)
            .OrderBy(entity => entity.CreatedAt)
            .Take(batchSize)
            .ForUpdate(LockBehavior.SkipLocked)
            .ToListAsync(cancellationToken);
    }

    private void EnsureTransaction()
    {
        if (context.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "Locks pessimistas exigem uma transação ativa. " +
                "Chame este método dentro do TransactionUseCase/UnitOfWork.");
        }
    }
}