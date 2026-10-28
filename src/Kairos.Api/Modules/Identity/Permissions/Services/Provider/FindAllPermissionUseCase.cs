using Kairos.Api.Common.Page;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Identity.Permissions.DTOs.Request;
using Kairos.Api.Modules.Identity.Permissions.Models;
using Kairos.Api.Modules.Identity.Permissions.Services.Interface;

namespace Kairos.Api.Modules.Identity.Permissions.Services.Provider;

public sealed class FindAllPermissionUseCase(IUnitOfWork uow)
    : IFindAllPermissionUseCase
{
    public async Task<Page<PermissionsModel>> ExecuteAsync(
        PermissionFilterDto dto,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        return await uow.PermissionRepository.Find(dto, ct);
    }
}