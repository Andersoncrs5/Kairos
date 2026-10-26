using Kairos.Api.Common.Page;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Identity.Users.DTOs.Request;
using Kairos.Api.Modules.Identity.Users.Models;
using Kairos.Api.Modules.Identity.Users.Services.Interface;

namespace Kairos.Api.Modules.Identity.Users.Services.Providers;

public sealed class FindUsersUseCase(IUnitOfWork uow) : IFindUsersUseCase
{
    public async Task<Page<UserModel>> ExecuteAsync(FilterUserDto dto, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        return await uow.UserRepository.Find(dto, ct);
    }
}