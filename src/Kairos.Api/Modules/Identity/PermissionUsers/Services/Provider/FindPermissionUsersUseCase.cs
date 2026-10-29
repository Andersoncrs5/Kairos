using Kairos.Api.Common.Page;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Identity.PermissionUsers.DTOs.Request;
using Kairos.Api.Modules.Identity.PermissionUsers.Models;
using Kairos.Api.Modules.Identity.PermissionUsers.Services.Interface;

namespace Kairos.Api.Modules.Identity.PermissionUsers.Services.Provider;

public sealed class FindPermissionUsersUseCase(IUnitOfWork uow)
    : IFindPermissionUsersUseCase
{
    public async Task<Page<PermissionUserModel>> ExecuteAsync(
        FilterPermissionUserDTO dto,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        return await uow.PermissionUserRepository.FilterAsync(dto, ct);
    }
}