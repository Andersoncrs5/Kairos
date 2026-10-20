using Kairos.Api.Common.Base.Repository;
using Kairos.Api.Modules.Categories.Models;

namespace Kairos.Api.Modules.Categories.Repositories;

public interface ICategoryRepository
    : IBaseRepository<CategoryModel>
{
    Task<CategoryModel?> GetByNameAsync(
        string name,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsByNameAsync(
        string name,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CategoryModel>> GetActiveAsync(
        CancellationToken cancellationToken = default);
}