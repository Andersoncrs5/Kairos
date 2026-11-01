using System.Linq.Expressions;
using Kairos.Api.Common.Base.Model;

namespace Kairos.Api.Common.Base.Repository;

public interface IBaseRepository<TEntity>
    where TEntity : BaseModel
{
    Task<TEntity?> GetByIdAsync(
        string id,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TEntity>> GetAllAsync(
        CancellationToken cancellationToken = default);

    Task AddAsync(
        TEntity entity,
        CancellationToken cancellationToken = default);

    Task AddAllAsync(
        IEnumerable<TEntity> entities,
        CancellationToken cancellationToken = default);

    void Update(TEntity entity);

    Task UpdateAllAsync(
        IEnumerable<TEntity> entities,
        CancellationToken cancellationToken = default);

    void Delete(TEntity entity);

    Task<int> DeleteByIdAndCountAsync(
        string id,
        CancellationToken cancellationToken = default);

    Task<int> DeleteAllAsync(
        CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(
        string id,
        CancellationToken cancellationToken = default);

    Task<TEntity?> FindByIdWithForUpdateAsync(
        string id,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default);

    Task<TEntity?> FindByIdWithForUpdateSkipLockedAsync(
        string id,
        CancellationToken cancellationToken = default);

    Task<TEntity?> FindByIdWithForShareAsync(
        string id,
        CancellationToken cancellationToken = default);

    Task<TEntity?> FindByIdWithForNoKeyUpdateAsync(
        string id,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TEntity>> FindByIdsWithForUpdateAsync(
        IEnumerable<string> ids,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TEntity>> FindBatchWithForUpdateSkipLockedAsync(
        Expression<Func<TEntity, bool>> predicate,
        int batchSize,
        CancellationToken cancellationToken = default);
}