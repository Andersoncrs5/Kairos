using Kairos.Api.Common.Base.Repository;
using Kairos.Api.Common.Page;
using Kairos.Api.Modules.Rooms.Equipment.DTOs.Request;
using Kairos.Api.Modules.Rooms.Equipment.Model;

namespace Kairos.Api.Modules.Rooms.Equipment.Repositories;

public interface IEquipmentRepository
    : IBaseRepository<EquipmentModel>
{
    Task<Page<EquipmentModel>> Find(
        FilterEquipmentDto dto,
        CancellationToken ct = default);

    Task<EquipmentModel?> GetByCodeAsync(
        string code,
        CancellationToken ct = default);

    Task<bool> ExistsByCodeAsync(
        string code,
        CancellationToken ct = default);

    Task<EquipmentModel?> GetByNameAsync(
        string name,
        CancellationToken ct = default);

    Task<bool> ExistsByNameAsync(
        string name,
        CancellationToken ct = default);

    Task<IReadOnlyList<EquipmentModel>> GetActiveAsync(
        CancellationToken ct = default);
}