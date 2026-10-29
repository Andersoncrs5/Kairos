using Kairos.Api.Common.Base.Repository;
using Kairos.Api.Common.Page;
using Kairos.Api.Modules.Categories.DTOs.Request;
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

    Task<Page<CategoryModel>> FilterAsync(
        CategoryFilterDto filter,
        CancellationToken cancellationToken = default);
}